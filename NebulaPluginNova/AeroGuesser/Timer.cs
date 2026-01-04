using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Nebula.AeroGuesser;

internal class Timer
{
    GameObject holderObj;
    AspectPosition aspectPosition;
    SpriteRenderer[] renderers;
    bool isActive = true;
    static private float[] xPos = [-0.38f, -0.16f, -0.1f, 0.15f, 0.37f, 0.49f];
    static private MultiImage NumberImage = DividedSpriteLoader.FromResource("Nebula.Resources.Timer.png", 100f, 1, 12);
    static private Image BackImage = SpriteLoader.FromResource("Nebula.Resources.TimerBack.png", 100f);
    public Timer(float z)
    {
        holderObj = UnityHelper.CreateObject("ModTimer", HudManager.Instance.transform, Vector3.zero);
        aspectPosition = holderObj.AddComponent<AspectPosition>();
        aspectPosition.Alignment = AspectPosition.EdgeAlignments.Top;
        aspectPosition.DistanceFromEdge = new(0f, -1f, z);
        aspectPosition.updateAlways = true;
        renderers = xPos.Select(x => UnityHelper.CreateObject<SpriteRenderer>("Number", holderObj.transform, new(x, 0f, -0.01f))).ToArray();
        renderers[2].sprite = NumberImage.GetSprite(10);
        renderers[5].sprite = NumberImage.GetSprite(11);
        var backRenderer = UnityHelper.CreateObject<SpriteRenderer>("Background", holderObj.transform, new(0f, 0.3f, 0f));
        backRenderer.sprite = BackImage.GetSprite();
        SetTime(0);

        float y = -1f;
        int lastCount = 0;
        var script = holderObj.AddComponent<ScriptBehaviour>();
        script.UpdateHandler += () =>
        {
            float goalY = isActive ? 0.3f : -1f;
            y -= (y - goalY).Delta(5f, 0.001f);
            aspectPosition.DistanceFromEdge = new(0f, y, z);
            int count = (int?)(timer?.Value + 1f) ?? 0;
            if(count != lastCount)
            {
                lastCount = count;
                if (count == 10 && isActive) NebulaAsset.PlayNamedSE(NebulaAudioClip.AeroGuesserCountdown, "Countdown", false);
                
            }
            SetTime(count);
        };
    }

    IFunctionalValue<float>? timer = null;
    public void SetTimer(IFunctionalValue<float> timer)
    {
        this.timer = timer;
    }

    public void SetActive(bool active) => isActive = active;

    private void SetTime(int time)
    {
        renderers[4].sprite = NumberImage.GetSprite(time % 10);
        time /= 10;
        renderers[3].sprite = NumberImage.GetSprite(time % 6);
        time /= 6;
        renderers[1].sprite = NumberImage.GetSprite(time % 10);
        time /= 10;
        renderers[0].sprite = NumberImage.GetSprite(time % 10);
    }
}
