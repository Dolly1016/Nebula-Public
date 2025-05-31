using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.Cosmetics.EmoteVariations;


internal class DanceEmote : AbstractEmote, IEmote
{
    private static Image icon = SpriteLoader.FromResource("Nebula.Resources.DanceEmoteIcon.png", 100f);
    protected override Image IconSprite => icon;
    private const string AttrTag = "DanceEmote";
    public override IEnumerator CoPlayEmote(PlayerControl player)
    {
        SizeModulator sizeModulator = new(Vector2.one, 10000f, false, 100, AttrTag, false, false);
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, sizeModulator, true));

        yield return ManagedEffects.Lerp(0.15f, p => sizeModulator.Size = new(1f, 1f + p * 0.1f));
        float halfPi = Mathf.PI * 0.5f;
        float p = 0f;
        for(int i = 0; i < 3; i++)
        {
            p = 0f;
            while (p < halfPi)
            {
                float sin = Mathf.Sin(p);
                sizeModulator.Size = new(1f + sin * 0.35f, 1.1f - sin * 0.3f);
                yield return null;
                p += Time.deltaTime * 5.5f;
            }
            p = 0f;
            while (p < halfPi)
            {
                float sin = Mathf.Sin(halfPi - p);
                sizeModulator.Size = new(1f + sin * 0.35f, 1.1f - sin * 0.3f);
                yield return null;
                p += Time.deltaTime * 12f;
            }
        }
        yield return ManagedEffects.Lerp(0.15f, p => sizeModulator.Size = new(1f, 1.1f - p * 0.1f));

        sizeModulator.Size = new(1f, 1f);
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, AttrTag));
    }

    bool IEmote.CanPlayInLobby => false;
}

