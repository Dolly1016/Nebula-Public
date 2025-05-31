using Nebula.Modules.Cosmetics.EmoteVariations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Nebula.Modules.Cosmetics;

internal interface IEmote {
    IEnumerator CoPlayEmote(PlayerControl player);
    GUIWidgetSupplier LocalIconSupplier { get; }
    bool CanPlayInLobby => true;
}

internal abstract class AbstractEmote : IEmote 
{
    abstract protected Image IconSprite { get; }
    public abstract IEnumerator CoPlayEmote(PlayerControl player);
    public GUIWidgetSupplier LocalIconSupplier => new Modules.GUIWidget.NoSGUIImage(GUIAlignment.Center, IconSprite, new(null, 0.45f))
    {
        PostBuilder = renderer =>
        {
            renderer.material = PlayerControl.LocalPlayer.cosmetics.currentBodySprite.BodySprite.material;
        }
    };
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
[NebulaRPCHolder]
static internal class EmoteManager
{
    private static readonly Dictionary<string, IEmote> allEmotes = [];

    public static IEnumerable<KeyValuePair<string, IEmote>> AllEmotes => allEmotes;
    static EmoteManager()
    {
        allEmotes["Good"] = new GoodEmote();
        allEmotes["Bad"] = new BadEmote();
        allEmotes["Dance"] = new DanceEmote();
        allEmotes["Greeting"] = new GreetingEmote();
    }

    private static RemoteProcess<(byte playerId, string emoteId)> RpcShowEmote = new("ShowEmote", (message, _) =>
    {
        var player = Helpers.GetPlayer(message.playerId);
        if (player && allEmotes.TryGetValue(message.emoteId, out var emote))
        {
            NebulaManager.Instance.StartCoroutine(emote.CoPlayEmote(player!).WrapToIl2Cpp());
        }
    });
    static private float lastSend = 0f;
    static public void SendLocalEmote(string emoteId)
    {
        if (lastSend + 1.5f > Time.time)
        {
            DebugScreen.Push(Language.Translate("ui.error.emote.cooldown"), 3f);
        }
        else
        {
            RpcShowEmote.Invoke((PlayerControl.LocalPlayer.PlayerId, emoteId));
            lastSend = Time.time;
        }
    }
}
