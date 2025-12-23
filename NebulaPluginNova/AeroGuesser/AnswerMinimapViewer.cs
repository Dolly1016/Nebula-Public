using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Text;

namespace Nebula.AeroGuesser;

internal class AnswerMinimapViewer : AbstractMinimapViewer
{
    internal AnswerMinimapViewer(Transform parent) : base(parent)
    {
    }

    static readonly private Image goalPinImage = SpriteLoader.FromResource("Nebula.Resources.MapPinGoal.png", 100f);
    List<SpriteRenderer> renderers = [];
    private IFunctionalValue<float> alphaQ = Arithmetic.FloatZero;
    private IFunctionalValue<float> hideQ = Arithmetic.FloatZero;
    private bool reshowing = false;
    public void ShowAnswer(byte mapId, Vector2 position, AeroPlayerOneQuizStatus[] playerStatus)
    {
        reshowing = false;
        renderers.Do(r =>
        {
            if(r) GameObject.Destroy(r.gameObject);
        });
        renderers.Clear();
        this.SetMap(mapId);

        hideQ = Arithmetic.FloatZero;
        alphaQ = Arithmetic.Decel(0f, 1f, 0.6f);

        Holder.SetActive(true);

        var coroutineManager = Holder.GetComponent<ScriptBehaviour>();

        var goalPin = this.CreatePin(RealToLocalPos(position));
        goalPin.sprite = goalPinImage.GetSprite();
        goalPin.transform.localScale = new(0f, 0f, 1f);
        renderers.Add(goalPin);
        coroutineManager.StartCoroutine(Effects.Bloop(0.6f, goalPin.transform, duration: 0.7f));

        var orderedPlayers = playerStatus.Where(status => status.selectedMap == mapId).OrderBy(status => status.selectedPosition.Distance(position)).ToArray();
        for(int i = 0;i<orderedPlayers.Length;i++)
        {
            var status = orderedPlayers[i];
            var playerPin = this.CreatePin(RealToLocalPos(status.selectedPosition), status.PlayerId);
            playerPin.transform.localScale = new(0f, 0f, 1f);
            renderers.Add(playerPin);
            var distance = position.Distance(status.selectedPosition);
            coroutineManager.StartCoroutine(Effects.Bloop(1.3f + 0.2f * i, playerPin.transform, 0.45f, 0.2f));
        }
    }

    public void HideForcibly()
    {
        Holder.SetActive(false);
    }

    public void Hide(bool temporary)
    {
        if(temporary) alphaQ = Arithmetic.Decel(alphaQ.Value, 0f, 0.4f);
        else hideQ = Arithmetic.Decel(hideQ.Value, 1f, 0.4f);
    }
    public void Reshow()
    {
        reshowing = true;
        alphaQ = Arithmetic.Decel(alphaQ.Value, 1f, 0.4f);
    }
    protected override void OnSetMap(byte mapId, bool changed) {}

    protected override void OnSetUp() {
        var script = Holder.AddComponent<ScriptBehaviour>();
        script.InvokeUpdateOnEnabed = true;
        script.UpdateHandler += () =>
        {
            float alpha = alphaQ.Value;
            MinimapRenderer.color = MinimapRenderer.color.SetAlpha(alpha);
            Color color = Color.white.AlphaMultiplied(alpha);
            renderers.Do(r => r.color = color);

            float scale = reshowing ? 0.82f : Mathn.Lerp(1.2f, 1f, alpha);
            Holder.transform.localScale = new(scale, scale, 1f);
            MapScaler.transform.localPosition = new(0f, (reshowing ? 0.5f :  0f) + hideQ.Value * 6f, 0f);
        };
    }

}

