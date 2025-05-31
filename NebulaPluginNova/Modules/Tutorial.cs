using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Compat;
using Virial.Media;
using Virial.Text;
using static Sentry.MeasurementUnit;

namespace Nebula.Modules;

file interface TutorialParameter
{
    float Duration { get; }
    bool CanClickToClose { get; }
    Virial.Media.GUIWidget Widget { get; }
    Func<UnityEngine.Vector2> Position { get; }
    Func<bool> ShowWhile { get; }
    bool HideWhileMinigameOpen { get; }
    bool HideWhileSomeUiOpen { get; }
    DataEntry<bool>? RelatedEntry { get; }
}

public class TutorialBuilder : TutorialParameter
{
    private float duration = 7f;
    private bool canClickToClose = true;
    private Virial.Media.GUIWidget widget = GUIEmptyWidget.Default;
    private Func<UnityEngine.Vector2> position;
    private Func<bool> showWhile = () => true;
    private bool hideWhileMinigameOpen = true, hideWhileSomeUiOpen = true;

    private DataEntry<bool>? relatedEntry = null;

    float TutorialParameter.Duration => duration;
    bool TutorialParameter.CanClickToClose => canClickToClose;
    Virial.Media.GUIWidget TutorialParameter.Widget => GUI.API.VerticalHolder(GUIAlignment.Left, widget, 
        GUI.API.VerticalMargin(0.15f), 
        GUI.API.Text(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), GUI.API.TextComponent(new(0.8f,0.8f,0.8f), canClickToClose ? "tutorial.guide.canClose" : "tutorial.guide.cannotClose").Size(0.8f)));
    Func<UnityEngine.Vector2> TutorialParameter.Position => position;
    Func<bool> TutorialParameter.ShowWhile => showWhile;
    bool TutorialParameter.HideWhileMinigameOpen => hideWhileMinigameOpen;
    bool TutorialParameter.HideWhileSomeUiOpen => hideWhileSomeUiOpen;
    DataEntry<bool>? TutorialParameter.RelatedEntry => relatedEntry;

    public TutorialBuilder Duration(float duration)
    {
        this.duration = duration;
        return this;
    }

    public TutorialBuilder CannotClickToClose()
    {
        canClickToClose = false;
        return this;
    }

    private Virial.Media.GUIWidget GetSimpleTextWidget(string text) => GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), text);
    public TutorialBuilder AsSimpleTextWidget(string text)
    {
        widget = GetSimpleTextWidget(text);
        return this;
    }

    public TutorialBuilder AsGraphicalWidget(Virial.Media.Image image, FuzzySize size, string caption)
    {
        widget = GUI.API.HorizontalHolder(GUIAlignment.Left,
            GUI.API.Image(GUIAlignment.Left, image, size),
            GUI.API.HorizontalMargin(0.15f),
            GetSimpleTextWidget(caption)
            );
        return this;
    }

    public TutorialBuilder AsSimpleTitledTextWidget(string title, string text)
    {
        widget = GUI.API.VerticalHolder(GUIAlignment.Left,
            GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), title),
            GetSimpleTextWidget(text)
            );
        return this;
    }

    public TutorialBuilder AsSimpleTitledTextWidget(string id)
    {
        widget = GUI.API.VerticalHolder(GUIAlignment.Left,
            GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "tutorial.variations." + id + ".title"),
            GetSimpleTextWidget(Language.Translate("tutorial.variations." + id + ".caption"))
            );
        return this;
    }

    public TutorialBuilder AsSimpleTitledOnceTextWidget(string id)
    {
        BindHistory(id);
        AsSimpleTitledTextWidget(id);
        return this;
    }

    public TutorialBuilder ShowWhile(Func<bool> showWhile)
    {
        this.showWhile = showWhile;
        return this;
    }

    public TutorialBuilder ShowEvenIfSomeUiOpen()
    {
        this.hideWhileSomeUiOpen = false;
        return this;
    }

    public TutorialBuilder ShowEvenIfMinigameOpen()
    {
        this.hideWhileMinigameOpen = false;
        return this;
    }

    public TutorialBuilder BindHistory(string label)
    {
        this.relatedEntry = Tutorial.GetTutorialProgress(label);
        return this;
    }

    public TutorialBuilder UnbindHistory()
    {
        this.relatedEntry = null;
        return this;
    }

    public TutorialBuilder(Func<UnityEngine.Vector3> worldPosition, bool forUiTutorial)
    {
        this.position = forUiTutorial ?
            () => UnityHelper.WorldToScreenPoint(worldPosition.Invoke(), LayerExpansion.GetUILayer()) :
            () => UnityHelper.WorldToScreenPoint(NebulaGameManager.Instance!.WideCamera.ConvertToWideCameraPos(worldPosition.Invoke()), LayerExpansion.GetObjectsLayer());
    }

    public TutorialBuilder() : this(() => HudManager.Instance.transform.position + new UnityEngine.Vector3(0f, 4f), true) { }
}

public static class Tutorial
{
    private static DataSaver TutorialDataSaver = new DataSaver("Tutorial");
    private static Image HintIcon = SpriteLoader.FromResource("Nebula.Resources.HintIcon.png", 100f);
    public static DataEntry<bool> GetTutorialProgress(string label)
    {
        if(TutorialDataSaver.TryGetEntry(label, out var entry))
        {
            return (entry as DataEntry<bool>)!;
        }
        return new BooleanDataEntry(label, TutorialDataSaver, false, shouldWrite: false);
    }

    public static void ShowTutorial(TutorialBuilder builder)
    {
        var parameters = builder as TutorialParameter;

        //表示済みのチュートリアルは表示しない。
        if (parameters.RelatedEntry?.Value ?? false) return;

        float left = parameters.Duration;

        Func<bool> predicate = () => parameters.ShowWhile.Invoke() && left > 0f &&
        (!parameters.HideWhileMinigameOpen || !(Minigame.Instance && Minigame.Instance.amOpening)) &&
        (!parameters.HideWhileSomeUiOpen || !NebulaInput.SomeUiIsActive);

        bool isShownAlready = false;

        NebulaManager.Instance.RegisterStaticPopup(()=>!(left > 0f) || (!isShownAlready && (parameters.RelatedEntry?.Value ?? false)), predicate, () => (
            parameters.Widget,
            parameters.Position,
            () =>
            {
                if (parameters.RelatedEntry != null) parameters.RelatedEntry.Value = true;
                isShownAlready = true;

                NebulaManager.Instance.MouseOverPopup.Parameters.CanPileCursor = true;

                if(parameters.CanClickToClose) NebulaManager.Instance.MouseOverPopup.Parameters.OnClick = () => { left = 0f; VanillaAsset.PlaySelectSE(); };
                NebulaManager.Instance.MouseOverPopup.Parameters.RelatedPredicate = predicate;
            }
        ), 
        () =>
        {
            if (!NebulaManager.Instance.MouseOverPopup.Piled) left -= Time.deltaTime;
            return left / parameters.Duration;
        },
        HintIcon
        );
    }

    public static void WaitAndShowTutorial(Func<bool> waitWhile, TutorialBuilder builder)
    {
        NebulaManager.Instance.StartCoroutine(ManagedEffects.Wait(waitWhile, () => ShowTutorial(builder)).WrapToIl2Cpp());
    }
}
