using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using static Nebula.Roles.Impostor.Thurifer;
using Virial.Game;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Map;

namespace Nebula.Modules;

[HarmonyPatch(typeof(UseButton), nameof(UseButton.Awake))]
public static class UseButtonSettingsPatch
{
    private static IDividedSpriteLoader useButtonIcon = DividedSpriteLoader.FromResource("Nebula.Resources.TeleporterButton.png", 100f, 4, 1);
    static void Postfix(UseButton __instance)
    {
        var useSetting = __instance.fastUseSettings[ImageNames.UseButton];
        UseButtonSettings GenerateSettings(Sprite sprite) => new()
        {
            FontMaterial = useSetting.FontMaterial,
            ButtonType = ImageNames.UseButton,
            Image = sprite,
            Text = useSetting.Text
        };

        for (int i = 0; i < 4; i++) __instance.fastUseSettings[(ImageNames)(128 + i)] = GenerateSettings(useButtonIcon.GetSprite(i));

        //__instance.fastUseSettings[ImageNames.UseButton] = GenerateSettings(useButtonIcon.GetSprite(0));
    }
}

[HarmonyPatch(typeof(PolishRubyGame), nameof(PolishRubyGame.Update))]
public static class PolishTeleportStonePatch
{
    static Exception Finalizer(PolishRubyGame __instance, Exception __exception)
    {
        if (__instance.MyNormTask) return __exception;

        for (int k = 0; k < __instance.Buttons.Length; k++)
        {
            if (__instance.Buttons[k].isActiveAndEnabled && __instance.swipes[k] < __instance.swipesToClean) return null!;
        }

        //ルビー磨きタスクが実際に終わっていれば、テレポートを実行する
        if (__instance.amClosing == Minigame.CloseState.Closing) return null!;
        if(ModSingleton<TeleportationSystem>.Instance.TryTeleport(PlayerControl.LocalPlayer.transform.position)) __instance.Close();
        return null!;
    }
}

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.Use))]
public static class PolishTeleportStoneStartPatch
{
    static private SpriteLoader stoneSprite = SpriteLoader.FromResource("Nebula.Resources.TeleporterTask.png", 100f);

    static void Postfix(SystemConsole __instance)
    {
        if (!Minigame.Instance) return;
        var rubyGame = Minigame.Instance.TryCast<PolishRubyGame>();
        if (!rubyGame) return;

        if (GeneralConfigurations.NonCrewmateCanUseTeleporterImmediatelyOption && !GamePlayer.LocalPlayer.IsCrewmate)
        {
            ModSingleton<TeleportationSystem>.Instance.TryTeleport(PlayerControl.LocalPlayer.transform.position);
            rubyGame!.ForceClose();
            return;
        }

        int kind = ModSingleton<TeleportationSystem>.Instance.GetKind(__instance.transform.position);

        rubyGame!.swipesToClean = 2;

        void SetHue(Renderer r) {
            r.material = new Material(NebulaAsset.HSVShader);
            r.material.SetFloat("_Hue", TeleportationSystem.TeleportHue[kind]);
            r.material.SetFloat("_Sat", TeleportationSystem.TeleportSat[kind]);
        }

        var rubyRenderer = rubyGame!.transform.FindChild("ruby_ruby").GetComponent<SpriteRenderer>();
        rubyRenderer.sprite = stoneSprite.GetSprite();
        SetHue(rubyRenderer);

        rubyGame.Buttons.Do(b => SetHue(b.GetComponent<SpriteRenderer>()));
        rubyGame.Buttons[0].transform.localPosition = new(1.4263f, 0.9409f, -1f);
        rubyGame.Buttons[1].transform.localPosition = new(-1.2148f, 1.1671f, -1f);
        rubyGame.Buttons[2].transform.localPosition = new(0.0426f, 2.0312f, -1f);
        rubyGame.Buttons[3].transform.localPosition = new(-1.5018f, 0.0475f, -1f);
        rubyGame.Buttons[4].transform.localPosition = new(-0.2493f, 0.8739f, -1f);
        rubyGame.Buttons[5].transform.localPosition = new(-0.7164f, -0.8277f, -1f);
        rubyGame.Buttons[6].transform.localPosition = new(0.5165f, -0.4754f, -1f);

        rubyGame.Buttons[2].transform.localEulerAngles = new(0f, 0f, 350f);
        rubyGame.Buttons[6].transform.localEulerAngles = new(0f, 0f, 270f);
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class TeleportationSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static TeleportationSystem()
    {
        DIManager.Instance.RegisterModule(() => new TeleportationSystem());
    }
    public TeleportationSystem() => ModSingleton<TeleportationSystem>.Instance = this;

    private record TeleporterPair(int kind, Vector2 pos1, Vector2 pos2);
    List<TeleporterPair> Pairs = [];
    public const int MaxTeleporterKind = 4;
    public static readonly float[] TeleportHue = [124, 296, 212, 16];
    public static readonly float[] TeleportSat = [1f, 1f, 1f, 0.5f];
    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);

    public int GetKind(Vector2 pos) => Pairs.MinBy(p => Math.Min(p.pos1.Distance(pos), p.pos2.Distance(pos)))?.kind ?? 0;
    
    void OnGameStart(GameStartEvent ev)
    {
        //ホストがテレポーターを生成する
        if (AmongUsClient.Instance.AmHost)
        {
            var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();


            var conditions = Helpers.Sequential(MaxTeleporterKind).Select(i => new MapObjectCondition("teleporter" + i, 5f)).ToArray();
            for (int i = 0; i < GeneralConfigurations.NumOfTeleportationPortalOption; i++)
            {
                var pos = spawner?.Spawn(2, 25f, "teleporter" + i, null, MapObjectType.Reachable, conditions);
                if (pos?.Length == 2) RpcSpawnTeleporter.Invoke((i, pos[0], pos[1]));
            }
        }
    }
    static private RemoteProcess<(int kind, Vector2 pos1, Vector2 pos2)> RpcSpawnTeleporter = new(
        "SpawnTelepoter",
        (message, _) =>
        {
            ModSingleton<TeleportationSystem>.Instance.Pairs.Add(new(message.kind, message.pos1, message.pos2));
            NebulaSyncObject.RpcInstantiate(Teleporter.MyTag!, [message.pos1.x, message.pos1.y, message.kind]);
            NebulaSyncObject.RpcInstantiate(Teleporter.MyTag!, [message.pos2.x, message.pos2.y, message.kind]);
        }
        );

    private float LastTeleport = 0f;
    public bool TryTeleport(Vector2 position)
    {
        if (NebulaGameManager.Instance?.CurrentTime - LastTeleport < 0.8f) return false;

        var pair = Pairs.MinBy(pair => Math.Min(position.Distance(pair.pos1), position.Distance(pair.pos2)));
        if (pair == null) return false;

        Vector2 from, to;
        if (position.Distance(pair.pos1) < position.Distance(pair.pos2))
        {
            from = pair.pos1;
            to = pair.pos2;
        }
        else
        {
            from = pair.pos2;
            to = pair.pos1;
        }

        RpcTeleport.Invoke((GamePlayer.LocalPlayer!, to));
        new StaticAchievementToken("teleportRecord");

        LastTeleport = NebulaGameManager.Instance!.CurrentTime;
        return true;
    }

    private const string teleporterAttrTag = "nebula::teleporter";
    static private IEnumerator CoTeleport(GamePlayer player, Vector2 to)
    {
        player.Unbox().IsTeleporting = true;
        SizeModulator sizeModulator = new(Vector2.one, 10000f, false, 100, teleporterAttrTag, false, false);
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, sizeModulator, true));

        float p = 0f;
        while(p < 1f)
        {
            float t2 = p * 2;
            sizeModulator.Size = new(1f - p, Mathf.Lerp(1f - p, t2 * t2, p * p));

            p += Time.deltaTime * 3.4f;

            yield return null;
        }

        player.VanillaPlayer.NetTransform.SnapTo(to);

        p = 1f;
        while (p > 0f)
        {
            float t2 = p * 2;
            sizeModulator.Size = new(1f - p, Mathf.Lerp(1f - p, t2 * t2, p * p));
            
            p -= Time.deltaTime * 2.7f;

            yield return null;
        }

        sizeModulator.Size = new(1f, 1f);
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, teleporterAttrTag));
        player.Unbox().IsTeleporting = false;
    }

    static private RemoteProcess<(GamePlayer player, Vector2 to)> RpcTeleport = new(
        "Teleport", 
        (message, _) =>
        {
            NebulaManager.Instance.StartCoroutine(CoTeleport(message.player, message.to));
        }
        );
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class Teleporter : NebulaSyncStandardObject
{
    public const string MyTag = "Teleporter";
    static private readonly IDividedSpriteLoader sprite = DividedSpriteLoader.FromResource("Nebula.Resources.Teleporter.png", 100f, TeleportationSystem.MaxTeleporterKind, 1);
    static private readonly IDividedSpriteLoader shadowSprite = DividedSpriteLoader.FromResource("Nebula.Resources.TeleporterBack.png", 100f, TeleportationSystem.MaxTeleporterKind, 1);
    private SpriteRenderer ConsoleRenderer;
    private SpriteRenderer ShadowRenderer;
    
    public Color BaseColor { get; set; }
    public Teleporter(Vector2 pos, int kind) : base(pos, ZOption.Just, true, null, Color.white)
    {
        ConsoleRenderer = UnityHelper.CreateObject<SpriteRenderer>("Console", MyRenderer.transform, Vector3.zero);
        ConsoleRenderer.sprite = sprite.GetSprite(kind);

        ShadowRenderer = UnityHelper.CreateObject<SpriteRenderer>("Console", MyRenderer.transform, new Vector3(0f, -0.2f, 0.001f));
        ShadowRenderer.sprite = shadowSprite.GetSprite(kind);

        SystemConsolize(MyRenderer.gameObject, ConsoleRenderer, (ImageNames)(128 + kind), PolishRubyPrefab);
    }

    static Teleporter() => NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new Teleporter(new(args[0], args[1]), (int)args[2]));

    void OnUpdate(GameUpdateEvent ev)
    {
        float num = Mathf.Sin(Time.time);
        ConsoleRenderer.transform.localPosition = new Vector3(0f, 0.28f + num * 0.05f, 0.0002f);
        ShadowRenderer.color = new(1f, 1f, 1f, 0.6f - num * 0.4f);

    }

    private static Minigame? polishRubyPrefab = null;
    private static Minigame PolishRubyPrefab { get { 
            if(polishRubyPrefab == null) polishRubyPrefab = VanillaAsset.MapAsset[4].ShortTasks.FirstOrDefault(t => t.MinigamePrefab.TryCast<PolishRubyGame>())?.MinigamePrefab;
            return polishRubyPrefab!;
        } }
}