using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial;
using Virial.Game;
using static Nebula.Roles.Neutral.Spectre;
using Virial.Events.Game.Minimap;
using Virial.Utilities;
using Nebula.Behaviour;

namespace Nebula.Roles.Neutral;

internal class SpectreFollower : DefinedRoleTemplate, DefinedRole
{
    string DefinedAssignable.InternalName => "spectre.follower";
    private SpectreFollower() : base("spectreFollower", Spectre.MyTeam.Color, RoleCategory.NeutralRole, Spectre.MyTeam, [VentConfiguration, SatietyRateOption, ShowDishesOnMapOption], false, optionHolderPredicate: ()=>IsSpawnableImpl){
        ConfigurationHolder?.ScheduleAddRelated(() => [Spectre.MyRole.ConfigurationHolder!]);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.spectreFollower.vent", true);
    static public FloatConfiguration SatietyRateOption = NebulaAPI.Configurations.Configuration("options.role.spectreFollower.satietyRate", (0.25f, 2f, 0.25f), 0.5f, FloatConfigurationDecorator.Ratio);
    static private BoolConfiguration ShowDishesOnMapOption = NebulaAPI.Configurations.Configuration("options.role.spectreFollower.showDishesOnMap", true);
    static public SpectreFollower MyRole = new SpectreFollower();

    static public GameStatsEntry StatsEatFries = NebulaAPI.CreateStatsEntry("stats.spectreFollower.eatFries", GameStatsCategory.Roles, MyRole);

    static private bool IsSpawnableImpl => (Spectre.MyRole as ISpawnable).IsSpawnable && Spectre.ImmoralistOption.GetValue() is 1 or >= 3;
    bool ISpawnable.IsSpawnable => IsSpawnableImpl;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, ISpectreTeam
    {
        DefinedRole RuntimeRole.Role => MyRole;
        private GameTimer ventCoolDown = (new Timer(VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentConfiguration.Duration);
        private bool canUseVent = VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;

        public int SpectreId { get;private set; }
        private GamePlayer? mySpectreCache = null;
        public Spectre.Instance? MySpectre => (this as ISpectreTeam).GetMySpectre(ref mySpectreCache);

        public Instance(GamePlayer player, int spectreId) : base(player){
            SpectreId = spectreId;
        }

        int[] RuntimeAssignable.RoleArguments => [SpectreId];
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                FunctionalGetter<float> leftAliveTimeProperty = new(() => MySpectre?.LeftAliveTime ?? 0f);
                var guage = Bind(new FriesGuageViewer(MyPlayer, leftAliveTimeProperty).Register(this));

                #region achievements
                (float spectre, float me) friesContribution = (0f, 0f);
                GameOperatorManager.Instance?.Register<Spectre.EatFriesEvent>(ev => {
                    if (ev.Player == null) return;
                    if (ev.Player == MySpectre?.MyPlayer) friesContribution.spectre += 1f;
                    if (ev.Player.AmOwner) friesContribution.me += SatietyRateOption;
                }, this);
                AchievementTokens.FunctionalToken("spectreFollower.challenge", () => 
                    friesContribution.spectre < friesContribution.me && 
                    NebulaGameManager.Instance!.EndState!.EndCondition == NebulaGameEnd.SpectreWin && 
                    NebulaGameManager.Instance.EndState.Winners.Test(MyPlayer));

                var firstSpectre = MySpectre?.MyPlayer;
                AchievementTokens.FunctionalToken("spectreFollower.hard1", ()=>
                    firstSpectre != null &&
                    MyPlayer.Role.Role == Spectre.MyRole &&
                    !NebulaGameManager.Instance!.EndState!.Winners.Test(firstSpectre) &&
                    NebulaGameManager.Instance.EndState!.Winners.Test(MyPlayer) &&
                    NebulaGameManager.Instance.EndState.EndCondition == NebulaGameEnd.SpectreWin
                );

                #endregion achievements
            }

        }

        DishMapLayer? mapLayer = null;
        [Local]
        void OnOpenNormalMap(MapOpenNormalEvent ev)
        {
            if (!ShowDishesOnMapOption) return;

            if (mapLayer is null)
            {
                mapLayer = UnityHelper.CreateObject<DishMapLayer>("DishLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                this.Bind(mapLayer.gameObject);
            }
            mapLayer.gameObject.SetActive(true);
        }

        [Local]
        void OnOpenAdminMap(MapOpenAdminEvent ev)
        {
            if (mapLayer) mapLayer?.gameObject.SetActive(false);
        }

        bool RuntimeRole.HasImpostorVision => true;
        bool RuntimeRole.IgnoreBlackout => true;
    }
}

internal class SpectreImmoralist : DefinedRoleTemplate, DefinedRole
{
    string DefinedAssignable.InternalName => "spectre.immoralist";
    private SpectreImmoralist() : base("spectreImmoralist", Spectre.MyTeam.Color, RoleCategory.NeutralRole, Spectre.MyTeam, [VentConfiguration, RespawnCooldownOption, RespawnDurationOption, ShowDishesOnMapOption, CanSuicideOption, ShowKillFlashOption], false, optionHolderPredicate: () => IsSpawnableImpl) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Spectre.MyRole.ConfigurationHolder!]);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.spectreImmoralist.vent", true);
    static private FloatConfiguration RespawnCooldownOption = NebulaAPI.Configurations.Configuration("options.role.spectreImmoralist.respawnCooldown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration RespawnDurationOption = NebulaAPI.Configurations.Configuration("options.role.spectreImmoralist.respawnDuration", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration ShowDishesOnMapOption = NebulaAPI.Configurations.Configuration("options.role.spectreImmoralist.showDishesOnMap", false);
    static private BoolConfiguration CanSuicideOption = NebulaAPI.Configurations.Configuration("options.role.spectreImmoralist.canSuicide", true);
    static private BoolConfiguration ShowKillFlashOption = NebulaAPI.Configurations.Configuration("options.role.spectreImmoralist.showKillFlash", true);
    static public SpectreImmoralist MyRole = new SpectreImmoralist();

    static private GameStatsEntry StatsRespawnFries = NebulaAPI.CreateStatsEntry("stats.spectreImmoralist.respawnFries", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsRemoveFries = NebulaAPI.CreateStatsEntry("stats.spectreImmoralist.removeFries", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsSuicide = NebulaAPI.CreateStatsEntry("stats.spectreImmoralist.suicide", GameStatsCategory.Roles, MyRole);

    static private bool IsSpawnableImpl => (Spectre.MyRole as ISpawnable).IsSpawnable && Spectre.ImmoralistOption.GetValue() is 2 or >=3;
    bool ISpawnable.IsSpawnable => IsSpawnableImpl;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, ISpectreTeam
    {
        DefinedRole RuntimeRole.Role => MyRole;
        private GameTimer ventCoolDown = (new Timer(VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentConfiguration.Duration);
        private bool canUseVent = VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;

        public int SpectreId { get; private set; }
        private GamePlayer? mySpectreCache = null;
        public Spectre.Instance? MySpectre => (this as ISpectreTeam).GetMySpectre(ref mySpectreCache);


        public Instance(GamePlayer player, int spectreId) : base(player)
        {
            SpectreId = spectreId;
        }
        int[] RuntimeAssignable.RoleArguments => [SpectreId];

        static private Image friesBoostButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ImmoralistAliveButton.png", 115f);
        static private Image friesRemoveButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ImmoralistDeadButton.png", 115f);
        static private Image suicideButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ImmoralistSuicideButton.png", 115f);
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                ObjectTracker<Dish> friesTracker = new ObjectTrackerUnityImpl<Dish, Dish>(MyPlayer.VanillaPlayer, 0.8f, ()=>ModSingleton<FriesDishManager>.Instance.AllDishes,
                    dish => dish.Ate == !MyPlayer.IsDead, dish => MyPlayer.IsDead || !NebulaPhysicsHelpers.AnyShadowBetween(MyPlayer.TruePosition, dish.Position, out _), d => d, dish => [dish.Console.transform.position], dish => dish.Console.Renderer, MyRole.UnityColor, true);
                
                var boostButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                boostButton.SetSprite(friesBoostButtonSprite.GetSprite());
                boostButton.Availability = (button) => MyPlayer.CanMove && friesTracker.CurrentTarget != null;
                boostButton.Visibility = (button) => !MyPlayer.IsDead;
                boostButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                boostButton.OnEffectEnd = (button) =>
                {
                    if (friesTracker.CurrentTarget == null || !MyPlayer.CanMove || MyPlayer.IsDead) return;

                    FriesDishManager.RpcUpdateFries.Invoke((friesTracker.CurrentTarget.DishId, false, null));
                    StatsRespawnFries.Progress();
                    boostButton.StartCoolDown();
                };
                boostButton.OnUpdate = (button) => {
                    if (!button.EffectActive) return;
                    if (friesTracker.CurrentTarget == null) button.InactivateEffect();
                };
                boostButton.EffectTimer = Bind(new Timer(RespawnDurationOption)).Start();
                boostButton.CoolDownTimer = Bind(new Timer(RespawnCooldownOption)).SetAsAbilityCoolDown().Start();
                boostButton.SetLabel("immoralist.place");

                var removeButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                removeButton.SetSprite(friesRemoveButtonSprite.GetSprite());
                removeButton.Availability = (button) => MyPlayer.CanMove && friesTracker.CurrentTarget != null;
                removeButton.Visibility = (button) => MyPlayer.IsDead;
                removeButton.OnClick = (button) => {
                    FriesDishManager.RpcUpdateFries.Invoke((friesTracker.CurrentTarget!.DishId, true, null));
                    StatsRemoveFries.Progress();
                    NebulaAsset.PlaySE(NebulaAudioClip.SpectreEat, pitch: 0.9f + System.Random.Shared.NextSingle() * 0.2f);
                };
                removeButton.SetLabel("immoralist.remove");

                if (CanSuicideOption)
                {
                    var suicideButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                    suicideButton.SetSprite(suicideButtonSprite.GetSprite());
                    suicideButton.Availability = (button) => MyPlayer.CanMove;
                    suicideButton.Visibility = (button) => !MyPlayer.IsDead;
                    suicideButton.OnClick = (button) =>
                    {
                        StatsSuicide.Progress();
                        MyPlayer.Suicide(PlayerState.Suicide, null, KillParameter.RemoteKill);
                    };
                    suicideButton.CoolDownTimer = Bind(new Timer(5f)).Start();
                    suicideButton.SetLabel("immoralist.suicide");
                    suicideButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                }

                #region achievements
                AchievementTokens.FunctionalToken("spectreImmoralist.challenge", () =>
                    NebulaGameManager.Instance!.EndState!.EndCondition == NebulaGameEnd.SpectreWin &&
                    NebulaGameManager.Instance!.EndState!.OriginalEndReason == GameEndReason.Situation &&
                    NebulaGameManager.Instance.EndState.Winners.Test(MyPlayer) && 
                    (NebulaGameManager.Instance.GetLastDead?.AmOwner ?? false) &&
                    (MyPlayer.PlayerState == PlayerState.Suicide || MyPlayer.PlayerState == PlayerState.Exiled));

                AchievementTokens.FunctionalToken("spectreImmoralist.hard1", () =>
                    (MySpectre?.MyPlayer?.IsDead ?? false) &&
                    MyPlayer.Role.Role.Team != Spectre.MyTeam &&
                    NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer) &&
                    NebulaGameManager.Instance.EndState.EndCondition != NebulaGameEnd.SpectreWin
                );

                #endregion achievements
            }

        }

        DishMapLayer? mapLayer = null;
        [Local]
        void OnOpenNormalMap(MapOpenNormalEvent ev)
        {
            if (!ShowDishesOnMapOption) return;

            if (mapLayer is null)
            {
                mapLayer = UnityHelper.CreateObject<DishMapLayer>("DishLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                this.Bind(mapLayer.gameObject);
            }
            mapLayer.gameObject.SetActive(true);
        }

        [Local]
        void OnOpenAdminMap(MapOpenAdminEvent ev)
        {
            if (mapLayer) mapLayer?.gameObject.SetActive(false);
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (!ShowKillFlashOption) return;

            if (MeetingHud.Instance || ExileController.Instance) return;

            if (ev.Player.AmOwner) return;
            if (!ev.Dead.HasAttribute(PlayerAttributes.BuskerEffect)) AmongUsUtil.PlayFlash(MyRole.UnityColor);
        }

        bool RuntimeRole.HasImpostorVision => true;
        bool RuntimeRole.IgnoreBlackout => true;
    }
}