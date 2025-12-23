using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game.Minimap;
using Virial.Text;

namespace Nebula.AeroGuesser;

internal interface IMinimapViewerInteraction : IWithAnswerPhase
{
    float MapQ { get; }
    void BackToMapSelection();
    void ClickMap(byte mapId, Vector2 position, bool isFixed);
}
internal class MinimapViewer : AbstractMinimapViewer
{
    GameObject backButton;
    GameObject determineButton;
    GameObject mapClick;

    SpriteRenderer myPinRenderer;

    IMinimapViewerInteraction interaction;
    IFunctionalValue<float> afterDetermineQ = Arithmetic.FloatZero;

    internal MinimapViewer(Transform parent, IMinimapViewerInteraction minimapViewerInteraction) : base(parent)
    {
        this.interaction = minimapViewerInteraction;
    }

    (byte mapId, Vector2 position)? currentSelection = null;

    protected override void OnSetUp()
    {
        Holder.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += () => Holder.transform.localPosition = new((afterDetermineQ.Value > 0f ? 0f : interaction.MapQ) * 12f, -interaction.AnswerQ * 4f, -10f);

        //戻るボタン
        var backButtonWidget = GUI.API.RawButton(Virial.Media.GUIAlignment.Center, AttributeAsset.SmallWideButton, "<< " + Language.Translate("aeroGuesser.ui.back"), _ => interaction.BackToMapSelection());
        backButton = backButtonWidget.Instantiate(new(10f, 10f), out _)!;
        backButton.transform.SetParent(Holder.transform);
        backButton.transform.localPosition = new(-4.6f, -2f, -1f);

        //確定ボタン
        var enterButtonWidget = GUI.API.RawButton(Virial.Media.GUIAlignment.Center, AttributeAsset.MarketplaceTabNonMaskedButton, Language.Translate("aeroGuesser.ui.determine"), _ => {
            if (currentSelection.HasValue)
            {
                interaction.ClickMap(currentSelection.Value.mapId, currentSelection.Value.position, true);
                OnDetermine();
            }
        });
        determineButton = enterButtonWidget.Instantiate(new(10f, 10f), out _)!;
        determineButton.transform.SetParent(Holder.transform);
        determineButton.transform.localPosition = new(4.4f, -2.6f, -1f);

        //ウィンドウ内にカーソルがある間のみ拡大する
        float scalerQ = 0f;
        MapScaler.AddComponent<ScriptBehaviour>().UpdateHandler += () =>
        {
            float determineQ = afterDetermineQ.Value;
            float goalQ = UnityHelper.MouseCursorIsInWindow(72) ? 1f : 0f;
            scalerQ -= (scalerQ - goalQ).Delta(6.5f, 0.004f);
            MapScaler.transform.localPosition = new(determineQ * 3.55f, Mathn.Lerp(-1.2f + scalerQ * 1f, -1.7f, determineQ), 0f);
            var scale = Mathn.Lerp(0.6f + 0.4f * scalerQ, 0.4f, determineQ) * (1f - interaction.AnswerQ * 0.8f);
            MapScaler.transform.localScale = new(scale, scale, 1f);
            if (MinimapRenderer) MinimapRenderer.color = VanillaAsset.MapBlue.AlphaMultiplied(scale);
        };

        mapClick = UnityHelper.CreateObject("Button", MapScaler.transform, new(0f, 0f, -0.5f));
        var button = mapClick.SetUpButton(true);
        button.OnClick.AddListener(() =>
        {
            ShowPin(Input.mousePosition);
            interaction.ClickMap(currentSelection!.Value.mapId, currentSelection!.Value.position, false);
            SetAsSelectPhase();
        });
        var collider = mapClick.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new(8.6f, 6f);

        myPinRenderer = CreatePin(Vector2.zero, PlayerControl.LocalPlayer.PlayerId);
        myPinRenderer.gameObject.SetActive(false);
    }

    protected override void OnSetMap(byte mapId, bool changed)
    {
        if (!changed) return;

        HidePin();
        if (currentSelection.HasValue && currentSelection.Value.mapId == mapId) ShowPinFromRealPos(currentSelection.Value.position);
    }


    private void ShowPin(Vector2 screenPos)
    {
        var localPos = ScreenToLocalPos(screenPos);
        ShowPinFromLocalPos(localPos);
    }

    private void ShowPinFromLocalPos(Vector2 localPos)
    {
        myPinRenderer.gameObject.SetActive(true);
        myPinRenderer.transform.localPosition = localPos.AsVector3(-5f);
        myPinRenderer.transform.localScale = new(0.5f, 0.5f, 1f);
        var realPos = LocalToRealPos(localPos);
        currentSelection = (CurrentMapId, realPos);
    }

    private void ShowPinFromRealPos(Vector2 realPos)
    {
        var localPos = RealToLocalPos(realPos);
        ShowPinFromLocalPos(localPos);
    }

    public void HidePin()
    {
        myPinRenderer.gameObject.SetActive(false);
    }
    
    public void ResetSelection()
    {
        HidePin();
        currentSelection = null;
    }

    public void SetAsSelectPhase()
    {
        backButton.SetActive(true);
        mapClick.SetActive(true);
        determineButton.SetActive(currentSelection.HasValue);
        afterDetermineQ = Arithmetic.Constant(0f);
    }

    private void OnDetermine()
    {
        mapClick.SetActive(false);
        afterDetermineQ = Arithmetic.Decel(0f, 1f, 0.34f);
        determineButton.SetActive(false);
        backButton.SetActive(false);
    }

    public void DisableToClick()
    {
        mapClick.SetActive(false);
    }
}
