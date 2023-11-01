using AmongUs.GameOptions;
using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;

namespace Nebula.Utilities;

[NebulaRPCHolder]
public static class AmongUsUtil
{
    public static void SetCamTarget(MonoBehaviour? target = null) => HudManager.Instance.PlayerCam.Target = target ?? PlayerControl.LocalPlayer;
    public static UiElement CurrentUiElement => ControllerManager.Instance.CurrentUiState.CurrentSelection;
    public static bool InMeeting => MeetingHud.Instance == null && ExileController.Instance == null;
    public static byte CurrentMapId => GameOptionsManager.Instance.CurrentGameOptions.MapId;
    private static string[] mapName = new string[] { "skeld", "mira", "polus", "undefined", "airship", "fungle" };
    public static string ToMapName(byte mapId) => mapName[mapId];
    public static string ToDisplayString(SystemTypes room, byte? mapId = null) => Language.Translate("location." + mapName[mapId ?? CurrentMapId] + "." + Enum.GetName(typeof(SystemTypes), room)!.HeadLower());
    public static float VanillaKillCoolDown => GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
    public static float VanillaKillDistance => GameManager.Instance.LogicOptions.GetKillDistance();
    public static bool InCommSab => PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer);
    public static PoolablePlayer PoolablePrefab => HudManager.Instance.IntroPrefab.PlayerPrefab;
    public static PoolablePlayer GetPlayerIcon(GameData.PlayerOutfit outfit, Transform parent,Vector3 position,Vector3 scale,bool flip = false)
    {
        var player = GameObject.Instantiate(PoolablePrefab);

        player.transform.SetParent(parent);

        player.name = outfit.PlayerName;
        player.SetFlipX(flip);
        player.transform.localPosition = position;
        player.transform.localScale = scale;
        player.UpdateFromPlayerOutfit(outfit, PlayerMaterial.MaskType.None, false, true);
        player.ToggleName(false);
        player.SetNameColor(Color.white);

        return player;
    }

    public static PoolablePlayer SetAlpha(this PoolablePlayer player, float alpha)
    {
        foreach (SpriteRenderer r in player.gameObject.GetComponentsInChildren<SpriteRenderer>())
            r.color = new Color(r.color.r, r.color.g, r.color.b, alpha);
        return player;
    }

    public static float GetAlpha(this PoolablePlayer player)=> player.cosmetics.currentBodySprite.BodySprite.color.a;

    public static PoolablePlayer GetPlayerIcon(GameData.PlayerOutfit outfit, Transform parent, Vector3 position, Vector2 scale, float nameScale,Vector3 namePos,bool flip = false)
    {
        var player = GetPlayerIcon(outfit, parent, position, scale, flip);

        player.ToggleName(true);
        player.SetNameScale(Vector3.one * nameScale);
        player.SetNamePosition(namePos);
        player.SetName(outfit.PlayerName);

        return player;
    }
    public static void PlayCustomFlash(Color color, float fadeIn, float fadeOut, float maxAlpha = 0.5f)
    {
        float duration = fadeIn + fadeOut;

        var flash = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
        flash.color = color;
        flash.enabled = true;
        flash.gameObject.active = true;

        HudManager.Instance.StartCoroutine(Effects.Lerp(duration, new Action<float>((p) =>
        {
            if (p < (fadeIn / duration))
            {
                if (flash != null)
                    flash.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(maxAlpha * p / (fadeIn / duration)));
            }
            else
            {
                if (flash != null)
                    flash.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(maxAlpha * (1 - p) / (fadeOut / duration)));
            }
            if (p == 1f && flash != null)
            {
                flash.enabled = false;
                GameObject.Destroy(flash.gameObject);
            }
        })));
    }

    public static void PlayFlash(Color color)
    {
        PlayCustomFlash(color, 0.375f, 0.375f);
    }

    public static void PlayQuickFlash(Color color)
    {
        PlayCustomFlash(color, 0.1f, 0.4f);
    }

    class CleanBodyMessage
    {
        public TranslatableTag? RelatedTag = null;
        public byte SourceId =  byte.MaxValue;
        public byte TargetId;
    }

    static RemoteProcess<CleanBodyMessage> RpcCleanDeadBodyDef = new RemoteProcess<CleanBodyMessage>(
        "CleanDeadBody",
        (writer, message) => { 
            writer.Write(message.SourceId);
            writer.Write(message.TargetId);
            writer.Write(message.RelatedTag?.Id ?? -1);
        },
        (reader)=> {
            return new() { SourceId = reader.ReadByte(), TargetId = reader.ReadByte(), RelatedTag = TranslatableTag.ValueOf(reader.ReadInt32()) };
        },
        (message, _) =>
        {
            foreach (var d in Helpers.AllDeadBodies()) if (d.ParentId == message.TargetId) GameObject.Destroy(d.gameObject);

            if (message.SourceId != byte.MaxValue)
                NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.CreanBody, message.SourceId, 1 << message.TargetId) { RelatedTag = message.RelatedTag });
        }
        );

    static public void RpcCleanDeadBody(byte bodyId,byte sourceId=byte.MaxValue,TranslatableTag? relatedTag = null)
    {
        RpcCleanDeadBodyDef.Invoke(new() { TargetId = bodyId, SourceId = sourceId, RelatedTag = relatedTag });
    }

    public static PlayerModInfo? GetHolder(this DeadBody body)
    {
        return NebulaGameManager.Instance?.AllPlayerInfo().FirstOrDefault((p) => p.HoldingDeadBody.HasValue && p.HoldingDeadBody.Value == body.ParentId);
    }


    static private SpriteLoader lightMaskSprite = SpriteLoader.FromResource("Nebula.Resources.LightMask.png", 100f);
    public static SpriteRenderer GenerateCustomLight(Vector2 position,Sprite? lightSprite = null)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Light", null, (Vector3)position + new Vector3(0, 0, -50f), LayerExpansion.GetDrawShadowsLayer());
        renderer.sprite = lightSprite ?? lightMaskSprite.GetSprite();
        renderer.material.shader = NebulaAsset.MultiplyBackShader;

        return renderer;
    }

    private static SpriteLoader footprintSprite = SpriteLoader.FromResource("Nebula.Resources.Footprint.png", 100f);
    public static void GenerateFootprint(Vector2 pos,Color color, float duration,bool canSeeInShadow = false)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Footprint", null, new Vector3(pos.x, pos.y, pos.y / 1000f + 0.001f), canSeeInShadow ? LayerExpansion.GetObjectsLayer() : LayerExpansion.GetDefaultLayer());
        renderer.sprite = footprintSprite.GetSprite();
        renderer.color = color;
        renderer.transform.eulerAngles = new Vector3(0f, 0f, (float)System.Random.Shared.NextDouble() * 360f);
        
        IEnumerator CoShowFootprint()
        {
            yield return new WaitForSeconds(duration);
            while (renderer.color.a > 0f)
            {
                Color col = renderer.color;
                col.a = Mathf.Clamp01(col.a - Time.deltaTime * 0.8f);
                renderer.color = col;
                yield return null;
            }
            GameObject.Destroy(renderer.gameObject);
        }

        NebulaManager.Instance.StartCoroutine(CoShowFootprint().WrapToIl2Cpp());
    }

    public static PlayerControl SpawnDummy()
    {
        var playerControl = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
        var i = playerControl.PlayerId = (byte)GameData.Instance.GetAvailableId();

        playerControl.isDummy = true;

        GameData.Instance.AddPlayer(playerControl);

        playerControl.transform.position = PlayerControl.LocalPlayer.transform.position;
        playerControl.GetComponent<DummyBehaviour>().enabled = true;
        playerControl.isDummy = true;
        playerControl.SetName(AccountManager.Instance.GetRandomName());
        playerControl.SetColor(i);
        playerControl.GetComponent<UncertifiedPlayer>().Certify();

        AmongUsClient.Instance.Spawn(playerControl, -2, InnerNet.SpawnFlags.None);
        GameData.Instance.RpcSetTasks(playerControl.PlayerId, new byte[0]);

        return playerControl;
    }

    public static readonly string[] AllVanillaOptions =
    {
        "vanilla.map",
        "vanilla.impostors",
        "vanilla.killDistance",
        "vanilla.numOfEmergencyMeetings",
        "vanilla.emergencyCoolDown",
        "vanilla.discussionTime",
        "vanilla.votingTime",
        "vanilla.numOfCommonTasks",
        "vanilla.numOfShortTasks",
        "vanilla.numOfLongTasks",
        "vanilla.visualTasks",
        "vanilla.confirmImpostor",
        "vanilla.anonymousVotes"
    };

    public static void ChangeOptionAs(string name,string value)
    {
        switch (name)
        {
            case "vanilla.map":
                GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, (byte)Array.IndexOf(mapName, value.HeadLower()));
                break;
            case "vanilla.impostors":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumImpostors, int.Parse(value));
                break;
            case "vanilla.killDistance":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.KillDistance, int.Parse(value));
                break;
            case "vanilla.numOfEmergencyMeetings":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumEmergencyMeetings, int.Parse(value));
                break;
            case "vanilla.emergencyCoolDown":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.EmergencyCooldown, int.Parse(value));
                break;
            case "vanilla.discussionTime":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.DiscussionTime, int.Parse(value));
                break;
            case "vanilla.votingTime":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.VotingTime, int.Parse(value));
                break;
            case "vanilla.numOfCommonTasks":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumCommonTasks, int.Parse(value));
                break;
            case "vanilla.numOfShortTasks":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumShortTasks, int.Parse(value));
                break;
            case "vanilla.numOfLongTasks":
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumLongTasks, int.Parse(value));
                break;
            case "vanilla.visualTasks":
                GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.VisualTasks, bool.Parse(value));
                break;
            case "vanilla.confirmImpostor":
                GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.ConfirmImpostor, bool.Parse(value));
                break;
            case "vanilla.anonymousVotes":
                GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.AnonymousVotes, bool.Parse(value));
                break;
        }
    }

    public static string GetOptionAsString(string name)
    {
        switch (name)
        {
            case "vanilla.map":
                return mapName[GameOptionsManager.Instance.CurrentGameOptions.GetByte(ByteOptionNames.MapId)].HeadUpper();
            case "vanilla.impostors":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumImpostors).ToString();
            case "vanilla.killDistance":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance).ToString();
            case "vanilla.numOfEmergencyMeetings":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumEmergencyMeetings).ToString();
            case "vanilla.emergencyCoolDown":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.EmergencyCooldown).ToString();
            case "vanilla.discussionTime":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.DiscussionTime).ToString();
            case "vanilla.votingTime":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.VotingTime).ToString();
            case "vanilla.numOfCommonTasks":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumCommonTasks).ToString();
            case "vanilla.numOfShortTasks":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumShortTasks).ToString();
            case "vanilla.numOfLongTasks":
                return GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumLongTasks).ToString();
            case "vanilla.visualTasks":
                return GameOptionsManager.Instance.CurrentGameOptions.GetBool(BoolOptionNames.VisualTasks).ToString();
            case "vanilla.confirmImpostor":
                return GameOptionsManager.Instance.CurrentGameOptions.GetBool(BoolOptionNames.ConfirmImpostor).ToString();
            case "vanilla.anonymousVotes":
                return GameOptionsManager.Instance.CurrentGameOptions.GetBool(BoolOptionNames.AnonymousVotes).ToString();
        }

        return "Invalid";
    }
}
