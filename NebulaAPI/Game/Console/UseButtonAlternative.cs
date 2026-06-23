using DiscordConnect;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Media;

namespace Virial.Game.Console;

public class UseButtonAlternative
{
    private static List<UseButtonAlternative> alternatives = [];
    internal static IEnumerable<UseButtonAlternative> AllAlternatives => alternatives;
    internal int ImageId { get; private set; }
    internal ImageNames ImageNames => (ImageNames)ImageId;
    public Image Image { get; private set; }
    internal Func<bool> CanUse { get; private set; }
    internal Action<SystemConsole> OnUsed { get; private set; }
    internal bool RunsOriginalProcess { get; private set; }
    private UnityEngine.Color? Color { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="image"></param>
    /// <param name="canUse"></param>
    /// <param name="onUsed"></param>
    /// <param name="runsOriginalProcess">バニラのミニゲーム開始処理を実行するか。trueの場合の実装がかなり雑で、実際の使用アクションの成否にかかわらずコールバックを呼び出している。</param>
    internal UseButtonAlternative(Image image, Func<bool> canUse, Action<SystemConsole> onUsed, bool runsOriginalProcess, UnityEngine.Color? color = null)
    {
        this.Image = image;
        this.ImageId = 128 + alternatives.Count;
        this.OnUsed = onUsed;
        this.CanUse = canUse;
        this.RunsOriginalProcess = runsOriginalProcess;
        this.Color = color;
        alternatives.Add(this);
    }

    static internal void ApplyTo(UseButton button)
    {
        var useSetting = button.fastUseSettings[ImageNames.UseButton];
        UseButtonSettings GenerateSettings(Sprite sprite) => new()
        {
            FontMaterial = useSetting.FontMaterial,
            ButtonType = ImageNames.UseButton,
            Image = sprite,
            Text = useSetting.Text
        };

        foreach (var alt in AllAlternatives) button.fastUseSettings[(ImageNames)(alt.ImageId)] = GenerateSettings(alt.Image.GetSprite());
    }

    static private bool TryGetAlternative(ImageNames image, [MaybeNullWhen(false)]out UseButtonAlternative found)
    {
        int index = (int)image - 128;
        if (index < 0 || alternatives.Count <= index)
        {
            found = null;
            return false;
        }
        found = alternatives[index];
        return true;
    }

    static private bool OriginalCanUse(SystemConsole console)
    {
        console.CanUse(NebulaAPI.AmongUs.LocalPlayer.Data, out var canUse, out _);
        if (!canUse) return false;
        return true;
    }

    static internal bool CheckCanUse(SystemConsole console)
    {
        if (TryGetAlternative(console.UseIcon, out var alt)) return alt.CanUse.Invoke();
        return true;
    }

    static internal bool PreUseConsole(SystemConsole console)
    {
        if(TryGetAlternative(console.UseIcon, out var alt))
        {
            if (!alt.CanUse.Invoke()) return false;

            if (!alt.RunsOriginalProcess)
            {
                if (!OriginalCanUse(console)) return false;
                alt.OnUsed.Invoke(console);
                return false;
            }
            return true;
        }
        return true;
    }

    static internal void PostUseConsole(SystemConsole console)
    {
        if (TryGetAlternative(console.UseIcon, out var alt))
        {
            if (alt.RunsOriginalProcess && alt.CanUse.Invoke())
            {
                alt.OnUsed.Invoke(console);
            }
        }
    }

    internal static bool SetOutline(SystemConsole console, bool on, bool mainTarget)
    {
        if (TryGetAlternative(console.UseIcon, out var alt))
        {
            if (alt.Color.HasValue)
            {
                var image = console.Image;
                if (image)
                {
                    var material = image.material;
                    material.SetFloat("_Outline", (float)(on ? 1 : 0));
                    material.SetColor("_OutlineColor", alt.Color.Value);
                    material.SetColor("_AddColor", mainTarget ? alt.Color.Value : UnityEngine.Color.clear);
                }

                return false;
            }
        }
        return true;
    }
}
