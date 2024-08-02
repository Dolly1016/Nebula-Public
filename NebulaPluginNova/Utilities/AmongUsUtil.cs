using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Game.Statistics;
using UnityEngine;
using static Il2CppSystem.Xml.XmlWellFormedWriter.AttributeValueCache;

namespace Nebula.Utilities;

file static class IgnoreShadowHelpers
{
    static public void SetIgnoreShadow(bool ignore = true, bool showNameText = true)
    {
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo()) p.Unbox().UpdateVisibility(false, ignore, showNameText);
    }

    static public void ResetIgnoreShadow()
    {
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo()) p.Unbox().UpdateVisibility(false, !NebulaGameManager.Instance.WideCamera.DrawShadow);
    }
}
file class IgnoreShadowScope : IDisposable
{
    public IgnoreShadowScope(bool showNameText = true)
    {
        IgnoreShadowHelpers.SetIgnoreShadow(showNameText: showNameText);
    }

    void IDisposable.Dispose()
    {
        IgnoreShadowHelpers.ResetIgnoreShadow();
    }
}

public class IgnoreShadowCamera : MonoBehaviour
{
    static IgnoreShadowCamera() => ClassInjector.RegisterTypeInIl2Cpp<IgnoreShadowCamera>();
    public bool ShowNameText = true;
    void OnPreRender() => IgnoreShadowHelpers.SetIgnoreShadow(true, ShowNameText);
}

public class CustomIgnoreShadowCamera : MonoBehaviour
{
    static CustomIgnoreShadowCamera() => ClassInjector.RegisterTypeInIl2Cpp<CustomIgnoreShadowCamera>();
    public Func<bool>? IgnoreShadow { get; set; } = null;
    void OnPreRender() => IgnoreShadowHelpers.SetIgnoreShadow(IgnoreShadow?.Invoke() ?? false);
}

public class ResetIgnoreShadowCamera : MonoBehaviour
{
    static ResetIgnoreShadowCamera() => ClassInjector.RegisterTypeInIl2Cpp<ResetIgnoreShadowCamera>();
    void OnPostRender() => IgnoreShadowHelpers.ResetIgnoreShadow();
}


[NebulaRPCHolder]
public static class AmongUsUtil
{
    public static IDisposable IgnoreShadow(bool showNameText = true) => new IgnoreShadowScope(showNameText);

    public static MonoBehaviour CurrentCamTarget => HudManager.Instance.PlayerCam.Target;
    public static void SetCamTarget(MonoBehaviour? target = null)
    {
        if(CurrentCamTarget == PlayerControl.LocalPlayer) PlayerControl.LocalPlayer.NetTransform.Halt();

        HudManager.Instance.PlayerCam.Target = target ?? PlayerControl.LocalPlayer;
    }
    public static void ToggleCamTarget(MonoBehaviour? target1 = null, MonoBehaviour? target2 = null)
    {
        target1 ??= PlayerControl.LocalPlayer;
        target2 ??= PlayerControl.LocalPlayer;
        SetCamTarget(CurrentCamTarget == target1 ? target2 : target1);
    }

    public static float GetShadowSize()
    {
        var shadowCollab = Camera.main.GetComponentInChildren<ShadowCollab>();
        return shadowCollab.ShadowCamera.orthographicSize;
    }
    public static void ChangeShadowSize(float orthographicSize = 3f)
    {
        var shadowCollab = Camera.main.GetComponentInChildren<ShadowCollab>();
        shadowCollab.ShadowCamera.orthographicSize = orthographicSize;
        shadowCollab.ShadowQuad.transform.localScale = new Vector3(orthographicSize * Camera.main.aspect, orthographicSize) * 2f;
    }

    public static UiElement CurrentUiElement => ControllerManager.Instance.CurrentUiState.CurrentSelection;
    public static bool InMeeting => MeetingHud.Instance == null && ExileController.Instance == null;
    public static byte CurrentMapId => GameOptionsManager.Instance.CurrentGameOptions.MapId;
    private static string[] mapName = new string[] { "skeld", "mira", "polus", "undefined", "airship", "fungle" };
    public static string ToMapName(byte mapId) => mapName[mapId];
    public static string ToDisplayString(SystemTypes room, byte? mapId = null) => Language.Translate("location." + mapName[mapId ?? CurrentMapId] + "." + Enum.GetName(typeof(SystemTypes), room)!.HeadLower());
    public static float VanillaKillCoolDown => GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
    public static float VanillaKillDistance => GameManager.Instance.LogicOptions.GetKillDistance();
    public static bool InCommSab => PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer);
    public static bool InAnySab => PlayerTask.PlayerHasTaskOfType<SabotageTask>(PlayerControl.LocalPlayer);
    public static PoolablePlayer PoolablePrefab => HudManager.Instance.IntroPrefab.PlayerPrefab;
    public static PoolablePlayer GetPlayerIcon(NetworkedPlayerInfo.PlayerOutfit outfit, Transform? parent,Vector3 position,Vector3 scale,bool flip = false, bool includePet = true)
    {
        var player = GameObject.Instantiate(PoolablePrefab);

        if(parent != null)player.transform.SetParent(parent);

        player.name = outfit.PlayerName;
        player.SetFlipX(flip);
        player.transform.localPosition = position;
        player.transform.localScale = scale;
        player.UpdateFromPlayerOutfit(outfit, PlayerMaterial.MaskType.None, false, includePet);
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

    public static PoolablePlayer GetPlayerIcon(NetworkedPlayerInfo.PlayerOutfit outfit, Transform parent, Vector3 position, Vector2 scale, float nameScale,Vector3 namePos,bool flip = false)
    {
        var player = GetPlayerIcon(outfit, parent, position, scale, flip);

        player.ToggleName(true);
        player.SetNameScale(Vector3.one * nameScale);
        player.SetNamePosition(namePos);
        player.SetName(outfit.PlayerName);

        return player;
    }
    public static void PlayCustomFlash(Color color, float fadeIn, float fadeOut, float maxAlpha = 0.5f, float maxDuration = 0f)
    {
        float duration = fadeIn + fadeOut;

        var flash = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
        flash.color = color.AlphaMultiplied(0f);
        flash.enabled = true;
        flash.gameObject.active = true;

        IEnumerator CoPlayFlash()
        {
            float t;
            
            t = 0f;
            while(t < fadeIn)
            {
                flash.color = color.AlphaMultiplied(Mathf.Clamp01(maxAlpha * t / fadeIn));
                t += Time.deltaTime;
                yield return null;
            }
            flash.color = color.AlphaMultiplied(Mathf.Clamp01(maxAlpha));

            yield return Effects.Wait(maxDuration);

            t = 0f;
            while (t < fadeOut)
            {
                flash.color = color.AlphaMultiplied(Mathf.Clamp01(maxAlpha * (1f - t / fadeIn)));
                t += Time.deltaTime;
                yield return null;
            }

            flash.enabled = false;
            GameObject.Destroy(flash.gameObject);
        }

        NebulaManager.Instance.StartCoroutine(CoPlayFlash().WrapToIl2Cpp());
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
                NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.CleanBody, message.SourceId, 1 << message.TargetId) { RelatedTag = message.RelatedTag });
        }
        );

    static public void RpcCleanDeadBody(byte bodyId,byte sourceId=byte.MaxValue,TranslatableTag? relatedTag = null)
    {
        RpcCleanDeadBodyDef.Invoke(new() { TargetId = bodyId, SourceId = sourceId, RelatedTag = relatedTag });
    }

    internal static GamePlayer? GetHolder(this DeadBody body)
    {
        return NebulaGameManager.Instance?.AllPlayerInfo().FirstOrDefault((p) => p.HoldingAnyDeadBody && p.HoldingDeadBody?.PlayerId == body.ParentId);
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

        var playerInfo = GameData.Instance.AddDummy(playerControl);
        
        playerControl.transform.position = PlayerControl.LocalPlayer.transform.position;
        playerControl.GetComponent<DummyBehaviour>().enabled = true;
        playerControl.isDummy = true;
        playerControl.SetName(AccountManager.Instance.GetRandomName());
        playerControl.SetColor(i);
        playerControl.SetHat(CosmeticsLayer.EMPTY_HAT_ID, i);
        playerControl.SetVisor(CosmeticsLayer.EMPTY_VISOR_ID, i);
        playerControl.SetSkin(CosmeticsLayer.EMPTY_SKIN_ID, i);
        playerControl.SetPet(CosmeticsLayer.EMPTY_PET_ID, i);
        playerControl.GetComponent<UncertifiedPlayer>().Certify();

        AmongUsClient.Instance.Spawn(playerControl, -2, InnerNet.SpawnFlags.None);
        playerInfo.RpcSetTasks(new byte[0]);

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

    static public int NumOfShortTasks => GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumShortTasks);
    static public int NumOfCommonTasks => GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumCommonTasks);
    static public int NumOfLongTasks => GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumLongTasks);
    static public int NumOfAllTasks => NumOfShortTasks + NumOfLongTasks + NumOfCommonTasks;

    public static bool IsInGameScene => HudManager.InstanceExists;

    public static R? GetRolePrefab<R>() where R : RoleBehaviour
    {
        foreach (RoleBehaviour role in RoleManager.Instance.AllRoles)
        {
            R? r = role.TryCast<R>();
            if (r != null)
            {
                return r;
            }
        }
        return null;
    }

    public static void SetEmergencyCoolDown(float coolDown, bool addVanillaCoolDown, bool addExtraCoolDown = true)
    {
        if (addVanillaCoolDown) coolDown += (float)GameManager.Instance.LogicOptions.GetEmergencyCooldown();

        if (addExtraCoolDown && (NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.IsDead) ?? 0) < GeneralConfigurations.EarlyExtraEmergencyCoolDownCondOption)
            coolDown += GeneralConfigurations.EarlyExtraEmergencyCoolDownOption;

        ShipStatus.Instance.EmergencyCooldown = coolDown;
    }

    public static void AddLobbyNotification(string message,UnityEngine.Color? color, Sprite? sprite = null,bool playSound = true)
    {
        var notifier = HudManager.Instance.Notifier;

        LobbyNotificationMessage newMessage = GameObject.Instantiate<LobbyNotificationMessage>(notifier.notificationMessageOrigin, Vector3.zero, Quaternion.identity, notifier.transform);
        newMessage.transform.localPosition = new Vector3(0f, 0f, -2f);
        newMessage.SetUp(message, sprite ?? notifier.settingsChangeSprite, color ?? notifier.settingsChangeColor, (Il2CppSystem.Action)(()=> notifier.OnMessageDestroy(newMessage)));
        notifier.ShiftMessages();
        notifier.AddMessageToQueue(newMessage);


        if (playSound) SoundManager.Instance.PlaySoundImmediate(notifier.settingsChangeSound, false, 1f, 1f, null);
    }

    public static (GameObject obj, NoisemakerArrow arrow) InstantiateNoisemakerArrow(Vector2 targetPos, bool withSound = false, float? hue = null)
    {
        var noisemaker = AmongUsUtil.GetRolePrefab<NoisemakerRole>();
        if (noisemaker != null)
        {
            if (withSound && Constants.ShouldPlaySfx())
            {
                SoundManager.Instance.PlayDynamicSound("NoisemakerAlert", noisemaker.deathSound, false, (DynamicSound.GetDynamicsFunction)((source, dt) =>
                {
                    if (!PlayerControl.LocalPlayer)
                    {
                        source.volume = 0f;
                        return;
                    }
                    source.volume = 1f;
                    Vector2 truePosition = PlayerControl.LocalPlayer.GetTruePosition();
                    source.volume = SoundManager.GetSoundVolume(targetPos, truePosition, 7f, 50f, 0.5f);
                }), SoundManager.Instance.SfxChannel);
                VibrationManager.Vibrate(1f, PlayerControl.LocalPlayer.GetTruePosition(), 7f, 1.2f, VibrationManager.VibrationFalloff.None, null, false);
            }
            GameObject gameObject = GameObject.Instantiate<GameObject>(noisemaker.deathArrowPrefab, Vector3.zero, Quaternion.identity);
            var deathArrow = gameObject.GetComponent<NoisemakerArrow>();
            deathArrow.SetDuration(3f);
            deathArrow.gameObject.SetActive(true);
            deathArrow.target = targetPos;

            if (hue.HasValue)
            {
                deathArrow.GetComponentsInChildren<SpriteRenderer>().Do(renderer =>
                {
                    renderer.material = new Material(NebulaAsset.HSVShader);
                    renderer.sharedMaterial.SetFloat("_Hue", 314);
                });
            }
            return (gameObject, deathArrow);
        }

        return (null, null)!;
    }

    public static void Ping(Vector2[] pos, bool smallenNearPing, bool playSE = true, float pitch = 1f)
    {
        if (!HudManager.InstanceExists) return;

        var prefab = GameManagerCreator.Instance.HideAndSeekManagerPrefab.PingPool.Prefab.CastFast<PingBehaviour>();

        PingBehaviour[] pings = new PingBehaviour[pos.Length];
        int i = 0;
        foreach (var p in pos)
        {
            var ping = GameObject.Instantiate(prefab);
            ping.target = p;
            ping.AmSeeker = smallenNearPing;
            ping.UpdatePosition();
            ping.gameObject.SetActive(true);

            ping.image.enabled = true;
            if (playSE) SoundManager.Instance.PlaySound(ping.soundOnEnable, false, 1f, null).pitch = pitch;

            pings[i++] = ping;
        }

        IEnumerator GetEnumarator()
        {
            yield return new WaitForSeconds(2f);

            foreach (var p in pings) GameObject.Destroy(p.gameObject);
        }

        HudManager.Instance.StartCoroutine(GetEnumarator().WrapToIl2Cpp());
    }
}
