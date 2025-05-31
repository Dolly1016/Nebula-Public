using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Map;
using Nebula.Modules.Cosmetics;
using Virial;
using Virial.Game;
using Virial.Text;

namespace Nebula.Utilities;

file static class IgnoreShadowHelpers
{
    static public void SetIgnoreShadow(bool ignore = true, bool showNameText = true)
    {
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo) p.Unbox().UpdateVisibility(false, ignore, showNameText);
    }

    static public void ResetIgnoreShadow()
    {
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo) p.Unbox().UpdateVisibility(false, !NebulaGameManager.Instance.WideCamera.DrawShadow);
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
    public static bool IsPiled(this PassiveUiElement uiElem)
    {
        var currentOver = PassiveButtonManager.Instance.currentOver;
        if (!currentOver || !uiElem) return false;
        return currentOver.GetInstanceID() == uiElem.GetInstanceID();
    }

    public static string GetRoomName(UnityEngine.Vector2 position, bool detail = false, bool shortName = false)
    {
        var mapData = MapData.GetCurrentMapData();
        foreach (var entry in ShipStatus.Instance.FastRooms)
        {
            if (entry.value.roomArea.OverlapPoint(position))
            {
                if (detail)
                {
                    var overrideRoom = mapData.GetOverrideMapRooms(entry.Key, position);
                    if (overrideRoom != null) return ToDisplayLocationString(overrideRoom, null, shortName);
                }
                return AmongUsUtil.ToDisplayString(entry.Key, null, shortName);
            }
        }

        var additionalRoom = mapData.GetAdditionalMapRooms(position, detail);
        if(additionalRoom != null) return ToDisplayLocationString(additionalRoom, null, shortName);

        return Language.Translate("location.outside");
    }

    public static void SetHighlight(Renderer renderer, bool on, Color? color = null)
    {
        if (on)
        {
            renderer.material.SetFloat("_Outline", 1f);
            renderer.material.SetColor("_OutlineColor", color ?? Color.yellow);
            renderer.material.SetColor("_AddColor", color ?? Color.yellow);
        }
        else
        {
            renderer.material.SetFloat("_Outline", 0f);
            renderer.material.SetColor("_OutlineColor", Color.clear);
            renderer.material.SetColor("_AddColor", Color.clear);
        }
    }

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

    public static ShadowCollab GetShadowCollab() => Camera.main.GetComponentInChildren<ShadowCollab>();
    public static float GetShadowSize() => GetShadowCollab().ShadowCamera.orthographicSize;
    
    public static Vector2 ConvertPosFromGameWorldToScreen(Vector2 pos)=> UnityHelper.WorldToScreenPoint(NebulaGameManager.Instance?.WideCamera.ConvertToWideCameraPos(pos) ?? Vector2.zero, LayerExpansion.GetObjectsLayer());

    public static void ChangeShadowSize(float orthographicSize = 3f)
    {
        var shadowCollab = GetShadowCollab();
        shadowCollab.ShadowCamera.orthographicSize = orthographicSize;
        shadowCollab.ShadowQuad.transform.localScale = new Vector3(orthographicSize * Camera.main.aspect, orthographicSize) * 2f;
    }

    public static UiElement CurrentUiElement => ControllerManager.Instance.CurrentUiState.CurrentSelection;
    public static bool InMeeting => MeetingHud.Instance == null && ExileController.Instance == null;
    public static byte CurrentMapId => GameOptionsManager.Instance.CurrentGameOptions.MapId;
    private static string[] mapName = new string[] { "skeld", "mira", "polus", "undefined", "airship", "fungle" };
    public static string ToMapName(byte mapId) => mapName[mapId];
    public static string ToDisplayLocationString(string room, byte? mapId = null, bool shortName = false)
    {
        string key = "location." + mapName[mapId ?? CurrentMapId] + "." + room;
        if (shortName)
        {
            string? shortResult = Language.Find(key + ".short");
            if (shortResult != null) return shortResult;
        }
        return Language.Translate(key);
    }
    public static string ToDisplayString(SystemTypes room, byte? mapId = null, bool shortName = false) => ToDisplayLocationString(Enum.GetName(typeof(SystemTypes), room)!.HeadLower(), mapId, shortName);
    public static float VanillaKillCoolDown => GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
    public static float VanillaKillDistance => GameManager.Instance.LogicOptions.GetKillDistance();
    public static bool InCommSab => PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer);
    public static bool InAnySab => PlayerTask.PlayerHasTaskOfType<SabotageTask>(PlayerControl.LocalPlayer);
    public static PoolablePlayer PoolablePrefab => HudManager.Instance.IntroPrefab.PlayerPrefab;
    public static PoolablePlayer GetPlayerIcon(OutfitCandidate outfit, Transform? parent, Vector3 position, Vector3 scale, bool flip = false, bool includePet = true)
        => GetPlayerIcon(outfit.Outfit.outfit, parent, position, scale, flip, includePet);
    public static PoolablePlayer GetPlayerIcon(OutfitDefinition outfit, Transform? parent, Vector3 position, Vector3 scale, bool flip = false, bool includePet = true)
        =>GetPlayerIcon(outfit.outfit, parent, position, scale, flip, includePet);
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
        var nosCosmeticsLayer = player.cosmetics.GetComponent<NebulaCosmeticsLayer>();
        nosCosmeticsLayer.SetSortingProperty(true, 1000f, 1000);
        player.cosmetics.nameText._SortingOrder = 2000;
        var renderers = player.cosmetics.nameText.GetComponentsInChildren<Renderer>();
        nosCosmeticsLayer.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += ()=>
        {
            renderers.Do(r =>
            {
                r.sortingGroupOrder = 2000;
                r.sortingOrder = 2000;
            });
        };
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

    public static SpriteRenderer GenerateFullscreen(Color color)
    {
        var flash = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
        flash.color = color;
        flash.enabled = true;
        flash.gameObject.active = true;
        return flash;
    }

    public static void PlayCustomFlash(Color color, float fadeIn, float fadeOut, float maxAlpha = 0.5f, float maxDuration = 0f)
    {
        float duration = fadeIn + fadeOut;

        var flash = GenerateFullscreen(color.AlphaMultiplied(Mathf.Clamp01(maxAlpha)));

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
                flash.color = color.AlphaMultiplied(Mathf.Clamp01(maxAlpha * (1f - t / fadeOut)));
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
        if (Helpers.CurrentMonth == 11)
        {
            var deadBodyPlayer = NebulaGameManager.Instance!.GetPlayer(bodyId);
            if (!(deadBodyPlayer?.MyKiller?.AmOwner ?? true) && NebulaGameManager.Instance!.CurrentTime - (deadBodyPlayer.DeathTime ?? 0f) < 5f) new StaticAchievementToken("freshWine");
        }
        RpcCleanDeadBodyDef.Invoke(new() { TargetId = bodyId, SourceId = sourceId, RelatedTag = relatedTag });
    }

    internal static GamePlayer? GetHolder(this DeadBody body)
    {
        return NebulaGameManager.Instance?.AllPlayerInfo.FirstOrDefault((p) => p.HoldingAnyDeadBody && p.HoldingDeadBody?.PlayerId == body.ParentId);
    }


    static private SpriteLoader lightMaskSprite = SpriteLoader.FromResource("Nebula.Resources.LightMask.png", 100f);
    public static SpriteRenderer GenerateCustomLight(Vector2 position,Sprite? lightSprite = null)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Light", null, (Vector3)position + new Vector3(0, 0, -10f), LayerExpansion.GetDrawShadowsLayer());
        renderer.sprite = lightSprite ?? lightMaskSprite.GetSprite();
        renderer.material.shader = NebulaAsset.MultiplyBackShader;
        new LightInfo(renderer);

        return renderer;
    }

    internal static CustomShadow GenerateCustomShadow(Vector2 position, Sprite shadowSprite, Color? shadowColor = null)
    {
        var shadowCam = NebulaGameManager.Instance!.WideCamera.SubShadowCam;
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Shadow", null, position.AsVector3(-5.001f), LayerExpansion.GetDrawShadowsLayer());
        renderer.sprite = shadowSprite;
        renderer.material.shader = NebulaAsset.CustomShadowShader;

        var mulRenderer = UnityHelper.CreateObject<SpriteRenderer>("Subrenderer", renderer.transform, new(0f, 0f, -0.001f), LayerExpansion.GetDefaultLayer());
        mulRenderer.sprite = shadowSprite;
        mulRenderer.material.shader = NebulaAsset.MultiplyShader;

        var defRenderer = UnityHelper.CreateObject<SpriteRenderer>("Defaultrenderer", renderer.transform, new(0f, 0f, 0.002f), LayerExpansion.GetObjectsLayer());
        defRenderer.sprite = shadowSprite;

        var assist = renderer.gameObject.AddComponent<CustomShadow>();
        assist.Renderer = renderer;
        assist.MulRenderer = mulRenderer;
        assist.DefaultRenderer = defRenderer;
        assist.ShadowCollab = AmongUsUtil.GetShadowCollab();
        renderer.material.SetColor("_ShadowColor", shadowColor ?? new(0.2745f, 0.2745f, 0.2745f));
        renderer.material.SetTexture("_ShadowTex", shadowCam.targetTexture);
        assist.SetBlend(1f);
        return assist;
    }

    private static SpriteLoader footprintSprite = SpriteLoader.FromResource("Nebula.Resources.Footprint.png", 100f);
    public static SpriteRenderer GenerateFootprint(Vector2 pos,Color color, float? duration, Func<bool>? canSeeIn = null)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Footprint", null, new Vector3(pos.x, pos.y, pos.y / 1000f + 0.001f), LayerExpansion.GetPlayersLayer());
        renderer.sprite = footprintSprite.GetSprite();
        renderer.color = color;
        renderer.transform.eulerAngles = new Vector3(0f, 0f, (float)System.Random.Shared.NextDouble() * 360f);
        
        IEnumerator CoDisappearFootprint()
        {
            if(canSeeIn == null) 
                yield return new WaitForSeconds(duration!.Value);
            else
            {
                float t = 0f;
                while(t < duration)
                {
                    renderer.enabled = canSeeIn.Invoke();
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            while (renderer.color.a > 0f)
            {
                Color col = renderer.color;
                col.a = Mathf.Clamp01(col.a - Time.deltaTime * 0.8f);
                renderer.color = col;

                renderer.enabled = canSeeIn?.Invoke() ?? true;

                yield return null;
            }
            GameObject.Destroy(renderer.gameObject);
        }

        if(duration.HasValue) NebulaManager.Instance.StartCoroutine(CoDisappearFootprint().WrapToIl2Cpp());

        return renderer;
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

    static public int NumOfImpostors => GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.NumImpostors);
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

        if (addExtraCoolDown && (NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.IsDead) ?? 0) < GeneralConfigurations.EarlyExtraEmergencyCoolDownCondOption)
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

    public static void SetHue(this GameObject rendererHolder, float hue)
    {
        rendererHolder.GetComponentsInChildren<Renderer>().Do(renderer =>
        {
            renderer.material = new Material(NebulaAsset.HSVShader);
            renderer.sharedMaterial.SetFloat("_Hue", hue);
        });
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

            if (hue.HasValue) deathArrow.gameObject.SetHue(hue.Value);
            
            return (gameObject, deathArrow);
        }

        return (null, null)!;
    }

    public static void Ping(Vector2[] pos, bool smallenNearPing, bool playSE = true, float pitch = 1f, Action<PingBehaviour>? postProcess = null)
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

            postProcess?.Invoke(ping);
        }

        IEnumerator GetEnumarator()
        {
            yield return new WaitForSeconds(2f);

            foreach (var p in pings) GameObject.Destroy(p.gameObject);
        }

        HudManager.Instance.StartCoroutine(GetEnumarator().WrapToIl2Cpp());
    }

    public static bool IsCustomServer()
    {
        return ServerManager.Instance?.CurrentRegion.TranslateName is StringNames.NoTranslation or null;
    }

    public static bool IsLocalServer()
    {
        return AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;
    }

    public static bool IsOnlineServer()
    {
        return AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;
    }

    public static void SetPlayerMaterial(Renderer renderer, Color mainColor, Color shadowColor, Color visorColor)
    {
        renderer.material.SetColor(PlayerMaterial.BackColor, shadowColor);
        renderer.material.SetColor(PlayerMaterial.BodyColor, mainColor);
        renderer.material.SetColor(PlayerMaterial.VisorColor, visorColor);
    }

    public static Vector2 GetCorner(float xCoeff, float yCoeff, Vector2 offset, Camera camera)
    {
        return new((camera.orthographicSize / (float)Screen.height * (float)Screen.width - -offset.x) * xCoeff, (camera.orthographicSize - offset.y) * yCoeff);
    }

    public static Vector2 GetCorner(float xCoeff, float yCoeff) => GetCorner(xCoeff, yCoeff, Vector2.zero, Camera.main);


    public static void PlayCinematicKill(GamePlayer killer, GamePlayer player, float delay, float view, CommunicableTextTag playerState, CommunicableTextTag? eventState, Func<(Vector3 position, GameObject showUpObj)> setUp)
    {
        player.VanillaPlayer.Visible = false;
        player.VanillaPlayer.NetTransform.Halt();
        player.VanillaPlayer.moveable = false;
        player.Unbox().WillDie = true;
        NebulaManager.Instance.StartDelayAction(1.3f + delay, () => player.VanillaPlayer.moveable = true);
        if (player.AmOwner && Minigame.Instance) Minigame.Instance.ForceClose();

        (var position, var showUpObj) = setUp.Invoke();

        if (player.AmOwner)
        {
            IEnumerator CoWaitAndKill()
            {
                float t = 0.7f;
                while (t > 0f)
                {
                    //会議が始まったらそのタイミングで死亡
                    if (MeetingHud.Instance)
                    {
                        killer.MurderPlayer(player, playerState, eventState, KillParameter.WithAssigningGhostRole | KillParameter.WithOverlay, KillCondition.TargetAlive);
                        yield break;
                    }

                    t -= Time.deltaTime;
                    yield return null;
                }


                killer.MurderPlayer(player, playerState, eventState, KillParameter.WithAssigningGhostRole, KillCondition.TargetAlive);

                showUpObj.transform.SetWorldZ(-100f);
                NebulaGameManager.Instance!.WideCamera.SetAttention(new SimpleAttention(10f, view, (Vector2)position - new Vector2(0.2f, 0f), FunctionalLifespan.GetTimeLifespan(2.2f)));

                var overlay = HudManager.Instance.KillOverlay;
                var overlayPrefab = UnityHelper.CreateObject<CustomKillOverlay>("overlayPrefab", null, Vector3.zero);
                overlay.ShowKillAnimation(overlayPrefab, new CustomKillOverlayData(overlayPrefab.gameObject).VanillaData);
            }
            NebulaManager.Instance.StartCoroutine(CoWaitAndKill().WrapToIl2Cpp());
            NebulaManager.Instance.StartDelayAction(2.45f + delay, () => showUpObj.transform.SetWorldZ(position.z));
        }
    }
}
