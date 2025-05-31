using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.AI;
using Virial;
using Virial.Compat;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
internal class ItemSupplierManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static ItemSupplierManager()
    {
        DIManager.Instance.RegisterModule(() => new ItemSupplierManager());
    }
    public ItemSupplierManager() => ModSingleton<ItemSupplierManager>.Instance = this;
    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);

    private MapObjectCondition? condition;
    private Dictionary<string, int> spawnedNum = [];
    public MapObjectCondition MapObjectCondition { get
        {
            condition ??= new MapObjectCondition("itemSupplier", 8f);
            return condition;
        } 
    }

    List<ItemSupplier> allSuppliers = new List<ItemSupplier>();
    public ItemSupplier? GetNearbySupplier(UnityEngine.Vector2 pos) => allSuppliers.MinBy(supplier => supplier.Position.Distance(pos));
    public void OnSupplierInstantiated(ItemSupplier supplier) => allSuppliers.Add(supplier);

    private AchievementToken<int>? achTokenOmikuji = null;

    [OnlyHost]
    void OnGameStart(GameStartEvent ev)
    {
        //ホストがサプライヤを生成する
        var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();

        if(PlantsAreSpawnable) spawner?.Spawn(GeneralConfigurations.NumOfPlantsOption, 8f, "itemSupplier", ItemSupplier.MyAllplayersTag, MapObjectType.Reachable, [MapObjectCondition]);
        if(WarpedPlantsAreSpawnable) spawner?.Spawn(GeneralConfigurations.NumOfWarpedPlantsOption, 8f, "itemSupplier", ItemSupplier.MyNoncrewmateTag, MapObjectType.Reachable, [MapObjectCondition]);

        if (Helpers.CurrentMonth == 1) achTokenOmikuji = new("omikuji", 0, (val, _) => val >= 5);
    }

    [OnlyHost]
    void OnMeetingEnd(MeetingEndEvent ev)
    {
        foreach(var supplier in allSuppliers) supplier.TryGrow();
    }

    public bool WarpedPlantsAreSpawnable => GeneralConfigurations.NumOfWarpedPlantsOption > 0 && Roles.Roles.AllPerks.Any(p => p.PerkCategory == PerkFunctionalDefinition.Category.NoncrewmateOnly && p.SpawnRate.Value > 0);
    public bool PlantsAreSpawnable => GeneralConfigurations.NumOfPlantsOption > 0 && Roles.Roles.AllPerks.Any(p => p.PerkCategory == PerkFunctionalDefinition.Category.Standard && p.SpawnRate.Value > 0);
    void OnRoleChanged(PlayerRoleSetEvent ev)
    {
        if (!ev.Player.AmOwner) return;

        bool isCrewmate = ev.Player.IsTrueCrewmate;
        if (isCrewmate) SetPerk(null, null, true);
        else if (ActiveNoncrewPerk == null)
        {
            if (WarpedPlantsAreSpawnable) SetPerk(null, EmptyNoncrewPerk, true);
            else SetPerk(null, null, true);
        }
    }

    void OnGameStartClient(GameStartEvent ev)
    {
        if(PlantsAreSpawnable) SetPerk(null, EmptyPerk, false);
    }

    public void SetPerk(PerkFunctionalDefinition perk) => SetPerk(perk, perk.PerkDefinition, perk.PerkCategory is PerkFunctionalDefinition.Category.NoncrewmateOnly);
    private void SetPerk(PerkFunctionalDefinition? perk, PerkDefinition? perkDefinition, bool noncrewmate)
    {
        void SetPerkInner(ref PerkInstanceEntry? instance, int priority)
        {
            instance?.instance?.Release();
            var newInstance = perkDefinition?.Instantiate();
            if (newInstance != null)
            {
                var functional = perk?.Instantiate(newInstance).Register(newInstance);
                if (functional?.HasAction ?? false)
                {
                    ButtonEffect.AddKeyGuide(newInstance.RelatedGameObject, NebulaInput.GetInput(noncrewmate ? VirtualKeyInput.PerkAction2 : VirtualKeyInput.PerkAction1).TypicalKey, new(0f, -0.75f), false);
                    newInstance.Button.OnClick.AddListener(functional.OnClick);
                }
                newInstance.Priority = priority;
                
                instance = new(newInstance, functional);
            }
            else
            {
                instance = null;
            }
        }
        if (noncrewmate)
            SetPerkInner(ref ActiveNoncrewPerk, 50);
        else
            SetPerkInner(ref ActivePerk, 100);

        if (perk != null && achTokenOmikuji != null) achTokenOmikuji.Value++;
    }

    private VirtualInput perkInput1 = NebulaInput.GetInput(VirtualKeyInput.PerkAction1);
    private VirtualInput perkInput2 = NebulaInput.GetInput(VirtualKeyInput.PerkAction2);
    void OnHudUpdate(GameHudUpdateEvent ev)
    {
        static void HandleKeyInput(PerkInstanceEntry? entry, VirtualInput input)
        {
            if (entry != null && entry.instance.RelatedGameObject.active && (entry.functionalInstance?.HasAction ?? false) && input.KeyDownInGame) entry.functionalInstance.OnClick();
        }
        HandleKeyInput(ActivePerk, perkInput1);
        HandleKeyInput(ActiveNoncrewPerk, perkInput2);

    }

    static private PerkDefinition EmptyPerk = new("empty");
    static private PerkDefinition EmptyNoncrewPerk = new("empty.noncrewmate");
    private record PerkInstanceEntry(PerkInstance instance, PerkFunctionalInstance? functionalInstance);
    PerkInstanceEntry? ActivePerk = null;
    PerkInstanceEntry? ActiveNoncrewPerk = null;

    public bool HasPerk(bool nonCrewmate)
    {
        return (nonCrewmate ? ActiveNoncrewPerk : ActivePerk)?.functionalInstance != null;
    }

    public PerkFunctionalDefinition? SelectPerk(bool nonCrewmateOnly)
    {
        PerkFunctionalDefinition.Category category = nonCrewmateOnly ? PerkFunctionalDefinition.Category.NoncrewmateOnly : PerkFunctionalDefinition.Category.Standard;
        var cand = Roles.Roles.AllPerks.Where(p => p.PerkCategory == category && p.SpawnRate.Value > 0 && GetNumOfLeftSpawnablePerks(p) > 0).ToArray();

        if (cand.Length == 0) return null;

        var sum = cand.Sum(p => p.SpawnRate.Value);
        int num = System.Random.Shared.Next(sum);
        foreach (var perk in cand)
        {
            if (perk.SpawnRate.Value > num) return perk;
            num -= perk.SpawnRate.Value;
        }

        return cand.Random();
    }

    public void OnPerkSpawned(string? perkId)
    {
        if (perkId == null) return;
        if (!spawnedNum.TryGetValue(perkId, out var num)) num = 0;
        spawnedNum[perkId] = num + 1;
    }

    public int GetNumOfLeftSpawnablePerks(PerkFunctionalDefinition perk)
    {
        if (perk.MaxSpawnConfiguration == 0) return 1000;
        if (!spawnedNum.TryGetValue(perk.Id, out var num)) num = 0;
        return perk.MaxSpawnConfiguration - num;
    }
}


[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class ItemSupplier : NebulaSyncStandardObject
{
    public const string MyAllplayersTag = "ItemSupplier";
    public const string MyNoncrewmateTag = "ItemSupplier-NoncrewmateOnly";
    static private IDividedSpriteLoader sprite = DividedSpriteLoader.FromResource("Nebula.Resources.ItemFlower.png", 140f, 4, 2);
    private SystemConsole myConsole;
    private PerkFunctionalDefinition? holdingPerk = null;
    private int leftBloom = GeneralConfigurations.MaxBloomsPerPlantOption;
    public bool NoncrewmateOnly { get; private set; }
    private int age;

    public ItemSupplier(UnityEngine.Vector2 pos, bool noncrewmateOnly) : base(pos, ZOption.Just, true, sprite.GetSprite((noncrewmateOnly ? 4 : 0) + 1), Color.white)
    {
        this.NoncrewmateOnly = noncrewmateOnly;
        this.age = 1;

        myConsole = SystemConsolize(MyRenderer.gameObject, MyRenderer, ImageNames.UseButton, UnityHelper.CreateObject<ItemSupplierMinigame>("MinigamePrefab", MyRenderer.transform, UnityEngine.Vector3.zero), 0.7f);
        myConsole.enabled = false;
        ModSingleton<ItemSupplierManager>.Instance?.OnSupplierInstantiated(this);

        NebulaManager.Instance.RegisterStaticPopup(() => false, () =>
        {
            if (holdingPerk == null) return false;
            if (NebulaInput.SomeUiIsActive) return false;
            if (MeetingHud.Instance || ExileController.Instance) return false;
            return Position.Distance(PlayerControl.LocalPlayer.transform.position) < 1f;
        }, () =>
        {
            var supplierManager = ModSingleton<ItemSupplierManager>.Instance!;
            Virial.Media.GUIWidget widget, origWidget = holdingPerk!.PerkDefinition.GetPerkWidgetWithImage();
            
            if (GamePlayer.LocalPlayer.IsDead)
            {
                widget = origWidget;
            }
            else {
                bool havePerkAlready = ModSingleton<ItemSupplierManager>.Instance!.HasPerk(NoncrewmateOnly);
                Virial.Color color = !CanObtainForRole ? Virial.Color.Red : Virial.Color.Green;
                string translationKey = !CanObtainForRole ? "perkui.message.noncrew" : havePerkAlready ? "perkui.message.swap" : "perkui.message.obtain";

                Virial.Media.GUIWidget GetTextWidget(Virial.Color color, string translationKey) => GUI.API.Text(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), GUI.API.TextComponent(color, translationKey).Bold());

                widget = GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    (NoncrewmateOnly && CanObtainForRole) ? GetTextWidget(Virial.Color.Red, "perkui.message.noncrewWarning") : null,
                    GetTextWidget(color, translationKey),
                    origWidget);
            }

            return (widget, () => UnityHelper.WorldToScreenPoint(HudManager.Instance.transform.position + new UnityEngine.Vector3(0f, -2.8f), LayerExpansion.GetUILayer()), null);
        });
    }

    static ItemSupplier()
    {
        NebulaSyncObject.RegisterInstantiater(MyAllplayersTag, (args) => new ItemSupplier(new(args[0], args[1]), false));
        NebulaSyncObject.RegisterInstantiater(MyNoncrewmateTag, (args) => new ItemSupplier(new(args[0], args[1]), true));
    }
    
    public void TryGainPerk() => RpcRequestGainPerk.Invoke((ObjectId, GamePlayer.LocalPlayer));

    public void UpdateAge(int age, string? perkId)
    {
        this.age = age;
        Sprite = sprite.GetSprite((NoncrewmateOnly ? 4 : 0) + age);

        if (age == 3) leftBloom--;

        if (perkId == null)
            holdingPerk = null;
        else
            holdingPerk = Roles.Roles.GetPerk(perkId);
    }

    public void TryGrow()
    {
        if (this.age == 3) return;
        if (this.leftBloom == 0) return;

        int nextAge = age;
        int speed = GeneralConfigurations.GrowSpeedOption;
        float[] firstSpeed = [0.5f, 0.5f, 0.65f, 0.8f, 0.8f, 0.8f, 0.9f, 0.9f, 1f];
        float[] secondSpeed = [0f, 0f, 0f, 0f, 0.15f, 0.35f, 0.5f, 0.7f, 0.85f];
        if (nextAge < 3 && Helpers.Prob(firstSpeed[speed])) nextAge++;
        if (nextAge < 3 && Helpers.Prob(secondSpeed[speed])) nextAge++;


        if (age != nextAge) {
            string perkId = "";
            if (nextAge == 3)
            {
                var selected = ModSingleton<ItemSupplierManager>.Instance.SelectPerk(NoncrewmateOnly);
                if (selected != null)
                    perkId = selected.Id;
                else
                    nextAge = 2;
            }
            
            RpcUpdateAge.Invoke((ObjectId, nextAge, perkId));
        }
    }

    bool CanObtainForRole => !NoncrewmateOnly || (!GamePlayer.LocalPlayer!.IsTrueCrewmate);

    void OnUpdate(GameUpdateEvent ev)
    {
        bool enabled = age == 3;

        var player = GamePlayer.LocalPlayer;
        if (player.IsDead) enabled = false;
        if (!CanObtainForRole) enabled = false;

        myConsole.enabled = enabled;
    }

    private static RemoteProcess<(int objectId, int age, string perkId)> RpcUpdateAge = new("updateSupplierAge",
        (message, _) =>
        {
            var supplier = NebulaSyncObject.GetObject<ItemSupplier>(message.objectId);
            if (supplier == null) return;

            supplier.UpdateAge(message.age, message.perkId.Length == 0 ? null : message.perkId);
            ModSingleton<ItemSupplierManager>.Instance?.OnPerkSpawned(message.perkId);
        });
    private static RemoteProcess<(int objectId, GamePlayer player)> RpcGainPerkAsHost = new("gainPerk",
        (message, _) =>
        {
            var supplier = NebulaSyncObject.GetObject<ItemSupplier>(message.objectId);
            if (supplier?.holdingPerk == null) return;

            if (message.player.AmOwner)
            {
                ModSingleton<ItemSupplierManager>.Instance?.SetPerk(supplier.holdingPerk);
                new StaticAchievementToken(supplier.NoncrewmateOnly ? "stats.plants.gain.warped" : "stats.plants.gain.normal");
                new StaticAchievementToken("stats.perk." + supplier.holdingPerk.Id + ".gain");
            }
            supplier.UpdateAge(0, null);
        });
    private static RemoteProcess<(int objectId, GamePlayer player)> RpcRequestGainPerk = new("requestGainPerk",
        (message, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            var supplier = NebulaSyncObject.GetObject<ItemSupplier>(message.objectId);
            if (supplier?.holdingPerk == null) return;

            RpcGainPerkAsHost.Invoke((message.objectId, message.player));
        });
}

public class ItemSupplierMinigame : Minigame
{

    static ItemSupplierMinigame() => ClassInjector.RegisterTypeInIl2Cpp<ItemSupplierMinigame>();
    public ItemSupplierMinigame(System.IntPtr ptr) : base(ptr) { }
    public ItemSupplierMinigame() : base(ClassInjector.DerivedConstructorPointer<ItemSupplierMinigame>())
    { ClassInjector.DerivedConstructorBody(this); }

    public override void Begin(PlayerTask task)
    {
        var supplier = ModSingleton<ItemSupplierManager>.Instance?.GetNearbySupplier(PlayerControl.LocalPlayer.transform.position);
        supplier?.TryGainPerk();

        this.ForceClose();
    }

    public override void Close()
    {

    }
}
