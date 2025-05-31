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
using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using TMPro;
using UnityEngine;
using Nebula.Roles.Impostor;
using static Nebula.Roles.Impostor.Thurifer;
using Virial.DI;
using Virial.Events.Game;
using System.Diagnostics.CodeAnalysis;
using Nebula.Roles.Abilities;
using static UnityEngine.GridBrushBase;
using Nebula.Roles.Crewmate;
using Il2CppInterop.Runtime.Attributes;
using Virial.Events.Game.Minimap;
using Virial.Utilities;
using Nebula.Modules.Cosmetics;

namespace Nebula.Roles.Neutral;

internal class Spectre : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.spectre", new(185, 152, 197), TeamRevealType.OnlyMe);
    static private bool alternateFlag = false;

    private Spectre() : base("spectre", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [VentConfiguration, ShowWhereKillersAreOption, ClairvoyanceOption,
        new GroupConfiguration("options.role.spectre.group.immoralist", [ImmoralistOption , SpectreFollowerChanceOption], MyTeam.Color.ToUnityColor().RGBMultiplied(0.65f)),
        new GroupConfiguration("options.role.spectre.group.dish", [NumOfDishesOption, SatietyRateOption, MaxSatietyOption, InitialSatietyOption, RequiredSatietyForWinningOption, ShowDishesOnMapOption, FriesReplenishmentCooldownOption], MyTeam.Color.ToUnityColor().RGBMultiplied(0.65f)),
        new GroupConfiguration("options.role.spectre.group.vanish", [VanishCostOption, VanishCooldownOption, VanishDurationOption], MyTeam.Color.ToUnityColor().RGBMultiplied(0.65f)),
        ],
        othersAssignments: () => {
            return ImmoralistOption.GetValue() switch
            {
                1 => [new((_, playerId) => (SpectreFollower.MyRole, [playerId]), RoleCategory.NeutralRole)],
                2 => [new((_, playerId) => (SpectreImmoralist.MyRole, [playerId]), RoleCategory.NeutralRole)],
                3 => [new((_, playerId) => (Helpers.Prob(SpectreFollowerChanceOption / 100f) ? SpectreFollower.MyRole : SpectreImmoralist.MyRole, [playerId]), RoleCategory.NeutralRole)],
                4 => [new((_, playerId) => {
                            alternateFlag = !alternateFlag;
                            return (alternateFlag ? SpectreImmoralist.MyRole : SpectreFollower.MyRole, [playerId]);
                        }, RoleCategory.NeutralRole)],
                _ => [],
            };
        })
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagDifficult);
        ConfigurationHolder?.ScheduleAddRelated(() => [SpectreFollower.MyRole.ConfigurationHolder!, SpectreImmoralist.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Spectre.png");
    }

    DefinedRole[] DefinedRole.AdditionalRoles => ImmoralistOption.GetValue() switch
    {
        0 => [],
        1 => [SpectreFollower.MyRole],
        2 => [SpectreImmoralist.MyRole],
        <= 3 => [SpectreFollower.MyRole, SpectreImmoralist.MyRole],
    };

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, (byte)arguments.Get(0, player.PlayerId), (float)arguments.Get(1, (int)(InitialSatietyOption * SatietyRateOption)));

    static private IntegerConfiguration NumOfDishesOption = NebulaAPI.Configurations.Configuration("options.role.spectre.numOfDishes", (0, 20), 10);
    static private FloatConfiguration VanishCostOption = NebulaAPI.Configurations.Configuration("options.role.spectre.vanishCost", (0f, 2f, 0.25f), 0.5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration VanishCooldownOption = NebulaAPI.Configurations.Configuration("options.role.spectre.vanishCooldown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration VanishDurationOption = NebulaAPI.Configurations.Configuration("options.role.spectre.vanishDuration", (2.5f, 30f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration SatietyRateOption = NebulaAPI.Configurations.Configuration("options.role.spectre.satiety", (20f, 100f, 2.5f), 45f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MaxSatietyOption = NebulaAPI.Configurations.Configuration("options.role.spectre.maxSatiety", (1f, 8f, 0.25f), 5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration InitialSatietyOption = NebulaAPI.Configurations.Configuration("options.role.spectre.initialSatiety", (0.5f, 8f, 0.25f), 4f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration RequiredSatietyForWinningOption = NebulaAPI.Configurations.Configuration("options.role.spectre.requiredSatietyForWinning", (0f, 7.75f, 0.25f), 4f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration FriesReplenishmentCooldownOption = NebulaAPI.Configurations.Configuration("options.role.spectre.friesReplenishmentCooldown", (20f, 240f, 5f), 80f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration ShowWhereKillersAreOption = NebulaAPI.Configurations.Configuration("options.role.spectre.showWhereKillersAre", true);
    static private BoolConfiguration ShowDishesOnMapOption = NebulaAPI.Configurations.Configuration("options.role.spectre.showDishesOnMap", true);
    static private BoolConfiguration ClairvoyanceOption = NebulaAPI.Configurations.Configuration("options.role.spectre.clairvoyance", true);
    static public ValueConfiguration<int> ImmoralistOption = NebulaAPI.Configurations.Configuration("options.role.spectre.immoralistVariation", [
        "options.role.spectre.immoralistVariation.none",
        "options.role.spectre.immoralistVariation.spectreFollower", 
        "options.role.spectre.immoralistVariation.immoralist", 
        "options.role.spectre.immoralistVariation.random", 
        "options.role.spectre.immoralistVariation.alternate"], 0);
    static private FloatConfiguration SpectreFollowerChanceOption = NebulaAPI.Configurations.Configuration("options.role.spectre.spectreFollowerChance", (10f, 90f, 1f), 50f, FloatConfigurationDecorator.Percentage, () => ImmoralistOption.GetValue() == 3);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.spectre.vent", true);


    static public Spectre MyRole = new();
    static private GameStatsEntry StatsVanish = NebulaAPI.CreateStatsEntry("stats.spectre.vanish", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsEatFries = NebulaAPI.CreateStatsEntry("stats.spectre.eatFries", GameStatsCategory.Roles, MyRole);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Dish : NebulaSyncShadowObject
    {
        static internal IDividedSpriteLoader DishSprite = DividedSpriteLoader.FromResource("Nebula.Resources.SpectreDish.png", 100f, 2, 1);
        static float[] InitialRespawnCoeff = [1.0f, 0.6f, 0.2f, 0.5f, 0.8f, 0.3f, 0.7f, 0.4f, 0.9f, 1.0f, 1.0f, 1.0f, 1.0f];
        public Dish(Vector2 pos) : base(pos, ZOption.Back, DishSprite.GetSprite(0), Color.white)
        {
            ModSingleton<FriesDishManager>.Instance.RegisterDish(this);
            if (DishId % 2 == 0)
                SetState(false);
            else
                RespawnCooldown = FriesReplenishmentCooldownOption * InitialRespawnCoeff[DishId / 2];

            MyRenderer.material = VanillaAsset.GetHighlightMaterial();
            Console = MyRenderer.gameObject.AddComponent<CustomConsole>();
            Console.Renderer = MyRenderer;
            Console.Property = new() {
                CanUse = (console) => {
                    if (Ate) return false;
                    var myPlayer = GamePlayer.LocalPlayer;
                    return 
                    (myPlayer?.Role?.Role == MyRole || myPlayer?.Role?.Role == SpectreFollower.MyRole) && !(myPlayer?.IsDead ?? true);
                },
                Use = CustomConsoleProperty.MinigameAction<FriesMinigame>(NebulaAsset.FriesMinigame, (minigame, console) =>
                {
                    minigame.MyDish = this;
                }),
                OutlineColor = MyRole.UnityColor
            };
            
        }

        public const string MyTag = "SpectreDish";
        public int DishId { get; internal set; } = -1;
        public bool Ate = true;
        public float RespawnCooldown { get; private set; } = 0f;
        public CustomConsole Console { get; private set; }
        public void SetState(bool ate)
        {
            Ate = ate;
            RespawnCooldown = FriesReplenishmentCooldownOption;
            MyRenderer.sprite = DishSprite.GetSprite(ate ? 0 : 1);

            if (ate && Minigame.Instance)
            {
                var minigame = Minigame.Instance.TryCast<FriesMinigame>();
                if (minigame && minigame!.MyDish.DishId == DishId) minigame.Close();
            }
        }

        static Dish() => NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new Dish(new(args[0], args[1])));

        void OnUpdate(GameUpdateEvent ev)
        {
            if (Ate && !MeetingHud.Instance && !ExileController.Instance)
            {
                RespawnCooldown -= Time.deltaTime;
                if (RespawnCooldown < 0f && Helpers.AmHost(PlayerControl.LocalPlayer)) FriesDishManager.RpcUpdateFries.Invoke((DishId, false, null));
            }
        }
    }

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    [NebulaRPCHolder]
    public class FriesDishManager : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        static FriesDishManager() => DIManager.Instance.RegisterModule(() => new FriesDishManager());
        private List<Dish> allDishes = [];
        public IEnumerable<Dish> AllDishes => allDishes;
        internal void RegisterDish(Dish dish) {
            dish.DishId = allDishes.Count;
            allDishes.Add(dish); 
        }
        private FriesDishManager()
        {
            ModSingleton<FriesDishManager>.Instance = this;
            this.Register(NebulaAPI.CurrentGame!);
        }
        public bool IsAvailable { get; private set; } = false;
        void OnGameStarted(GameStartEvent ev)
        {
            IsAvailable = GeneralConfigurations.CurrentGameMode == Virial.Game.GameModes.FreePlay || (Spectre.MyRole as ISpawnable).IsSpawnable;

            if (!IsAvailable) return;

            if (AmongUsClient.Instance.AmHost)
            {
                using (RPCRouter.CreateSection("SpawnDish"))
                {
                    var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();
                    spawner?.Spawn(NumOfDishesOption, 7.5f, "dish", Dish.MyTag, MapObjectType.SmallOrTabletopOutOfSight);
                }
            }
        }

        static internal RemoteProcess<(int id, bool ate, GamePlayer? player)> RpcUpdateFries = new("DishFriesUpdate",
            (message, _) =>
            {
                if(ModSingleton<FriesDishManager>.Instance.TryGetDish(message.id, out var dish)) dish.SetState(message.ate);
                if(message.player != null) GameOperatorManager.Instance?.Run<EatFriesEvent>(new EatFriesEvent(message.player));
            });
        public bool TryGetDish(int id, [MaybeNullWhen(false)]out Dish dish)
        {
            if (id < allDishes.Count) dish = allDishes[id];
            else dish = null!;
            return dish != null;
        }
    }

    public class DishMapLayer : MonoBehaviour
    {
        List<(int id, SpriteRenderer renderer)> allDishes = null!;

        static DishMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<DishMapLayer>();
        
        public void Awake()
        {
            var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
            var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);
            allDishes = [];
            foreach (var dish in ModSingleton<FriesDishManager>.Instance.AllDishes)
            {
                var renderer = UnityHelper.CreateObject<SpriteRenderer>("Dish", transform, VanillaAsset.ConvertToMinimapPos(dish.Position, center, scale));
                renderer.transform.localScale = new(0.7f, 0.7f, 1f);
                renderer.gameObject.AddComponent<MinimapScaler>();
                allDishes.Add((dish.DishId, renderer));
            }

            Update();
        }

        public void Update()
        {
            foreach(var dish in allDishes)
            {
                if(ModSingleton<FriesDishManager>.Instance.TryGetDish(dish.id, out var d))
                {
                    dish.renderer.sprite = Dish.DishSprite.GetSprite(d.Ate ? 0 : 1);
                }
            }
        }
    }

    [NebulaRPCHolder]
    public class FriesGuageViewer : DependentLifespan, IGameOperator
    {
        private class GuageFries
        {
            GameObject partialHolder;
            SpriteRenderer? fullRenderer;
            SpriteRenderer partialRenderer;
            SpriteRenderer[] guageRenderer;
            GameObject holder;

            int index;
            public GuageFries(Transform transform, Vector3 pos, int index, float scale)
            {
                this.index = index;

                holder = UnityHelper.CreateObject("FriesHolder", transform, pos);
                holder.transform.localScale = new(0.83f * scale, 0.83f * scale, 1f);

                partialHolder = UnityHelper.CreateObject("Partial", holder.transform, Vector3.zero);
                partialRenderer = UnityHelper.CreateObject<SpriteRenderer>("Outer", partialHolder.transform, new(0f, 0f, -0.1f));

                if (index + 1 <= (int)(float)MaxSatietyOption)
                {
                    fullRenderer = UnityHelper.CreateObject<SpriteRenderer>("Full", holder.transform, Vector3.zero);
                    fullRenderer.sprite = guageFriesSprite.GetSprite(0);
                    partialRenderer.sprite = guageFriesSprite.GetSprite(4);
                    guageRenderer = new SpriteRenderer[4];
                }
                else
                {
                    fullRenderer = null;
                    int num = (int)(((float)MaxSatietyOption - (float)index) * 4f);
                    partialRenderer.sprite = guageFriesSprite.GetSprite(num);
                    guageRenderer = new SpriteRenderer[num];
                }
                for (int i = 0; i < guageRenderer.Length; i++)
                {
                    guageRenderer[i] = UnityHelper.CreateObject<SpriteRenderer>("Guage" + i, partialHolder.transform, Vector3.zero);
                    guageRenderer[i].sprite = guageFriesSprite.GetSprite(5 + i);
                }

                Update(InitialSatietyOption);
            }

            private static Color GuagePurpleColor = new(202f / 255f, 148f / 255f, 221f / 255f);
            private static Color GuageGrayColor = Color.white.RGBMultiplied(0.2f);
            public void Update(float currentFoodlevel)
            {
                if ((float)(index + 1) > currentFoodlevel)
                {
                    //満タンまでいかないとき
                    fullRenderer?.gameObject.SetActive(false);
                    partialHolder.SetActive(true);

                    for (int i = 0; i < guageRenderer.Length; i++)
                    {
                        guageRenderer[i].gameObject.SetActive(true);

                        float guageMax = (float)index + 0.25f * (float)(i + 1);
                        float guageMin = (float)index + 0.25f * (float)i;
                        if (currentFoodlevel < guageMax)
                        {
                            if (currentFoodlevel > guageMin)
                            {
                                float level = (currentFoodlevel - guageMin) * 4f;//0から1の値をとる
                                bool active;
                                if (level < 0.25f)
                                    active = Mathf.Repeat(Time.time, 0.52f) < 0.26f;
                                else if (level < 0.65f)
                                    active = Mathf.Repeat(Time.time, 0.8f) < 0.5f;
                                else
                                    active = Mathf.Repeat(Time.time, 0.9f) < 0.65f;
                                guageRenderer[i].color = active ? GuageGrayColor : (level < 0.5f ? Color.red : GuagePurpleColor);
                            }
                            else
                            {
                                guageRenderer[i].color = GuageGrayColor;
                            }
                        }
                        else
                        {
                            guageRenderer[i].color = GuagePurpleColor;
                        }
                    }
                }
                else
                {
                    fullRenderer?.gameObject.SetActive(true);
                    partialHolder.SetActive(false);
                }
            }
        }

        static private IDividedSpriteLoader guageIconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.SpectreGuageIcon.png", 100f, 2, 1);
        static private IDividedSpriteLoader guageFriesSprite = DividedSpriteLoader.FromResource("Nebula.Resources.SpectreGuageFries.png", 100f, 5, 2);
        static private ResourceExpandableSpriteLoader guageBackgroundSprite = new("Nebula.Resources.SpectreGuageBackground.png", 100f, 10, 10);

        private IFunctionalGetProperty<float> leftAliveTime;
        
        private GamePlayer player;
        public float LeftSatiety => leftAliveTime.Value / SatietyRateOption;

        public FriesGuageViewer(GamePlayer myPlayer, IFunctionalGetProperty<float> leftAliveTime)
        {
            player = myPlayer;

            this.leftAliveTime = leftAliveTime;

            var gauge = HudContent.InstantiateContent("SpectreGauge", true, true, false);
            this.BindGameObject(gauge.gameObject);

            float friesScale = MaxSatietyOption > 5 ? 0.6f : 0.8f;
            float friesOffset = MaxSatietyOption > 5 ? 1f : 1.15f;
            float gaugeWidth = 0.9f + friesScale * MaxSatietyOption;
            float gaugeScale = 0.6f;
            var center = UnityHelper.CreateObject("Adjuster", gauge.transform, new(-0.04f + gaugeWidth * gaugeScale * 0.5f, -0.2f, -0.1f));
            center.transform.localScale = new(gaugeScale, gaugeScale, 1f);

            var gaugeRenderer = UnityHelper.CreateObject<SpriteRenderer>("GaugeSprite", center.transform, new(0f, 0f, 0f));
            gaugeRenderer.sprite = guageBackgroundSprite.GetSprite();
            gaugeRenderer.drawMode = SpriteDrawMode.Sliced;
            gaugeRenderer.tileMode = SpriteTileMode.Continuous;
            gaugeRenderer.size = new(gaugeWidth, 0.75f);

            List<GuageFries> guageFries = [];
            int index = 0;
            while ((float)index < MaxSatietyOption)
            {
                guageFries.Add(new GuageFries(center.transform, new(-gaugeWidth * 0.5f + friesOffset + friesScale * (float)index, 0f, -0.1f), index, friesScale / 0.8f));
                index++;
            }

            var foxRenderer = UnityHelper.CreateObject<SpriteRenderer>("Fox", center.transform, new(-gaugeWidth * 0.5f, 0.1f, -0.1f));
            foxRenderer.sprite = guageIconSprite.GetSprite(1);

            GameOperatorManager.Instance?.Subscribe<GameHudUpdateEvent>((ev) => {
                gauge.gameObject.SetActive(!player.IsDead);
                float foodLevel = leftAliveTime.Value / SatietyRateOption;
                if (center) guageFries.Do(g => g.Update(foodLevel));
                foxRenderer.sprite = guageIconSprite.GetSprite(foodLevel < 1f ? 0 : 1);
            }, this);
        }
    }

    public interface ISpectreTeam
    {
        int SpectreId { get; }

        Spectre.Instance? MySpectre { get; }
        internal Spectre.Instance? GetMySpectre(ref GamePlayer? cache)
        {
            if (cache != null && cache.Role is Spectre.Instance spectre && spectre.SpectreId == SpectreId) return spectre;
            else cache = null;

            if (cache == null) if (NebulaGameManager.Instance?.AllPlayerInfo.Find(p => p.Role is Spectre.Instance s && s.SpectreId == SpectreId, out var found) ?? false) cache = found;
            if (cache == null) return null;
            return cache.Role as Spectre.Instance;
        }
    }

    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole, ISpectreTeam
    {
        public override DefinedRole Role => MyRole;
        public int SpectreId { get; private set; } = 0;
        public Spectre.Instance? MySpectre => this;
        int[] RuntimeAssignable.RoleArguments => [SpectreId, (int)LeftAliveTime];
        
        public Instance(GamePlayer player, byte spectreId, float leftAliveTime) : base(player, VentConfiguration){
            this.SpectreId = spectreId;
            this.leftAliveTime = leftAliveTime;
        }

        private float leftAliveTime;
        public float LeftAliveTime => leftAliveTime;
        public void AddSatiety(float satiety)
        {
            leftAliveTime = Mathf.Clamp(leftAliveTime + satiety * SatietyRateOption, 0f, MaxSatietyOption * SatietyRateOption);
            
        }
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectreButton.png", 115f);
        
        List<TrackingArrowAbility> killerArrows = [];
        public override void OnActivated()
        {
            if (AmOwner)
            {
                leftAliveTime = InitialSatietyOption * SatietyRateOption;
                float maxAliveTime = MaxSatietyOption * SatietyRateOption;
                FunctionalGetter<float> leftAliveTimeProperty = new(()=>leftAliveTime);

                var guage = new FriesGuageViewer(MyPlayer, leftAliveTimeProperty).Register(this);

                var vanishButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    VanishCooldownOption, VanishDurationOption, "vanish", buttonSprite,
                    _ => (VanishCostOption + 1f) < guage.LeftSatiety);
                vanishButton.OnEffectStart = (button) => {
                    AddSatiety(-VanishCostOption);
                    MyPlayer.GainAttribute(PlayerAttributes.Invisible, VanishCooldownOption, false, 100, "nebula::spectreVanish");
                    StatsVanish.Progress();
                };
                vanishButton.OnEffectEnd = (button) => vanishButton.StartCoolDown();
                NebulaGameManager.Instance?.AllPlayerInfo.Do(CheckAndTrackKiller);


                //やらかし称号
                var acTokenAnother = AchievementTokens.FirstFailedAchievementToken("spectre.another1", MyPlayer, this);
                GameOperatorManager.Instance?.Subscribe<EatFriesEvent>(ev => {
                    if (ev.Player.AmOwner) acTokenAnother.Value.triggered = true;
                }, this);
            }

        }

        float lastSuicideRequestTime = 0f;
        float lastShareTime = -10f;
        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!MeetingHud.Instance && !ExileController.Instance) leftAliveTime -= Time.deltaTime;

            if (!(leftAliveTime > 0f))
            {
                leftAliveTime = 0f;
                if (!MyPlayer.IsDead && NebulaGameManager.Instance!.CurrentTime > lastSuicideRequestTime + 0.2f)
                {
                    lastSuicideRequestTime = NebulaGameManager.Instance!.CurrentTime;
                    MyPlayer.Suicide(PlayerState.Starved, null, KillParameter.NormalKill);
                }
            }

            if (NebulaGameManager.Instance!.CurrentTime > lastShareTime + 0.2f)
            {
                RpcShareStatus.Invoke((MyPlayer, leftAliveTime));
                lastShareTime = NebulaGameManager.Instance!.CurrentTime;
            }
        }

        [Local, OnlyMyPlayer]
        void OnRevived(PlayerReviveEvent ev)
        {
            leftAliveTime = 40f;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            leftAliveTime = Mathf.Max(20f, leftAliveTime);
        }

        bool Won => !MyPlayer.IsDead && CanWinForGuage;
        void OnCheckGameEnd(EndCriteriaMetEvent ev)
        {
            if (Won && ev.EndReason != GameEndReason.Sabotage) ev.TryOverwriteEnd(NebulaGameEnd.SpectreWin, GameEndReason.Special);
        }

        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.SpectreWin && Won && IsSameTeam(ev.Player));


        //キラー役職に矢印を付ける
        [Local]
        void OnSetRole(PlayerRoleSetEvent ev)
        {
            if (ShowWhereKillersAreOption) CheckAndTrackKiller(ev.Player);
        }

        private void CheckAndTrackKiller(GamePlayer player)
        {
            void RegisterArrow(GamePlayer player, Color color) => killerArrows.Add(new TrackingArrowAbility(player.Unbox(), 0f, color, false).Register(this));

            killerArrows.RemoveAll(a => { if (a.MyPlayer == player) { a.Release(); return true; } else return false; });
            if (player.AmOwner) return;

            if (player.IsImpostor) RegisterArrow(player, Palette.ImpostorRed);
            if (player.Role is Jackal.Instance) RegisterArrow(player, Jackal.MyRole.UnityColor);
            if (player.Role.Role is Sheriff) RegisterArrow(player, Color.white);
            if (player.Role is Avenger.Instance) RegisterArrow(player, Avenger.MyRole.UnityColor);

        }

        DishMapLayer? mapLayer = null;
        [Local]
        void OnOpenNormalMap(MapOpenNormalEvent ev)
        {
            if (!ShowDishesOnMapOption) return;

            if (mapLayer is null)
            {
                mapLayer = UnityHelper.CreateObject<DishMapLayer>("DishLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                this.BindGameObject(mapLayer.gameObject);
            }
            mapLayer.gameObject.SetActive(true);
        }

        [Local]
        void OnOpenAdminMap(MapOpenAdminEvent ev)
        {
            if (mapLayer) mapLayer?.gameObject.SetActive(false);
        }

        bool RuntimeRole.EyesightIgnoreWalls => ClairvoyanceOption;
        bool RuntimeRole.HasImpostorVision => true;
        bool RuntimeRole.IgnoreBlackout => true;
        public bool CanWinForGuage => (leftAliveTime / SatietyRateOption) > RequiredSatietyForWinningOption;

        static private RemoteProcess<(GamePlayer player, float leftAliveTime)> RpcShareStatus = new("ShareSpectreStatus", (message, _) =>
        {
            if (message.player.Role is Spectre.Instance spectre) spectre.leftAliveTime = message.leftAliveTime;
        }, false);

        static public RemoteProcess<(GamePlayer player, float satiety)> RpcAddSatiety = new("SpectreAddSatiety", (message, _) =>
        {
            if (message.player.Role is Spectre.Instance spectre) spectre.AddSatiety(message.satiety);
        });

        private bool IsSameTeam(GamePlayer? player)
        {
            if(player == null) return false;
            if(player.Role is Spectre.ISpectreTeam spectre && spectre.SpectreId == SpectreId) return true;
            return false;
        }

        //妖狐は同チームを識別できる。
        [Local]
        void DecorateFollowersColor(PlayerDecorateNameEvent ev)
        {
            if (IsSameTeam(ev.Player) && !ev.Player.AmOwner) ev.Color = MyRole.RoleColor;
        }

        //背徳者系は妖狐を識別できる。
        [OnlyMyPlayer]
        void DecorateJackalColor(PlayerDecorateNameEvent ev)
        {
            if (IsSameTeam(GamePlayer.LocalPlayer) && !ev.Player.AmOwner) ev.Color = MyRole.RoleColor;
        }

        //背徳者系を後追いさせる。

        [OnlyHost, OnlyMyPlayer]
        void OnSpectreDead(PlayerDieOrDisconnectEvent ev)
        {
            //妖狐以外の生存した同チームを後追い/追加追放させる。
            NebulaGameManager.Instance?.AllPlayerInfo.Where(p => !p.IsDead && IsSameTeam(p) && p.Role.Role != MyRole).Do(p =>
            {
                if(ev is PlayerExiledEvent)
                    p.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
                else
                    p.Suicide(PlayerState.Suicide, null, KillParameter.RemoteKill);
            });
        }

        #region Achievements

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.EndCondition == NebulaGameEnd.SpectreWin && ev.EndState.Winners.Test(MyPlayer))
            {
                if (MoreCosmic.GetTags(MyPlayer.DefaultOutfit.outfit).Contains("hat.role.spectre")) new StaticAchievementToken("spectre.costume1");
                if (ev.EndState.OriginalEndReason == GameEndReason.SpecialSituation && ev.EndState.OriginalEndCondition != NebulaGameEnd.SpectreWin) new StaticAchievementToken("spectre.common3");
                if(NebulaGameManager.Instance!.AllPlayerInfo.All(p => !p.IsImpostor || !p.IsDead) && NebulaGameManager.Instance!.AllPlayerInfo.Count(p => p.IsImpostor) >= 2) new StaticAchievementToken("spectre.challenge");
            }
        }

        #endregion Achievements
    }


    public class EatFriesEvent : AbstractPlayerEvent
    {
        internal EatFriesEvent(GamePlayer player): base(player) { }
    }

    public class FriesMinigame : Minigame
    {

        static FriesMinigame() => ClassInjector.RegisterTypeInIl2Cpp<FriesMinigame>();
        public FriesMinigame(System.IntPtr ptr) : base(ptr) { }
        public FriesMinigame() : base(ClassInjector.DerivedConstructorPointer<FriesMinigame>())
        { ClassInjector.DerivedConstructorBody(this); }

        public Dish MyDish;

        public override void Begin(PlayerTask task)
        {
            this.BeginInternal(task);

            MetaScreen.InstantiateCloseButton(transform, new(-3f, 1f, -0.5f)).OnClick.AddListener(Close);

            GameObject[] fries = [gameObject.transform.GetChild(2).gameObject, gameObject.transform.GetChild(3).gameObject, gameObject.transform.GetChild(4).gameObject];
            int eaten = 0;
            int eatenSubCounter = 0;
            
            foreach (var f in fries)
            {
                for (int i = 2; i >= 0; i--)
                {
                    var t = f.transform.GetChild(i); ;
                    var button = t.gameObject.AddComponent<PassiveButton>();
                    button.OnMouseOut = new UnityEngine.Events.UnityEvent();
                    button.OnMouseOver = new UnityEngine.Events.UnityEvent();
                    button.OnClick.RemoveAllListeners();
                    int index = i;
                    GameObject? nextObj = i != 0 ? f.transform.GetChild(i - 1).gameObject : null;
                    button.OnClick.AddListener(() => {
                    if (amClosing == CloseState.Closing) return;

                    NebulaAsset.PlaySE(NebulaAudioClip.SpectreEat, true, pitch: 1.0f + (float)System.Random.Shared.NextDouble() * 0.3f);
                    eatenSubCounter++;

                    if (eatenSubCounter < 2) return;

                    eatenSubCounter = 0;
                    t.gameObject.SetActive(false);

                    if (nextObj != null)
                    {
                        nextObj.SetActive(true);
                    }
                    else
                    {
                        eaten++;
                        if (eaten == 3)
                        {
                            var role = GamePlayer.LocalPlayer!.Role;
                                if (role is Spectre.Instance spectre)
                                {
                                    spectre.AddSatiety(1);
                                    StatsEatFries.Progress();
                                }
                                else if (role is SpectreFollower.Instance spectreFollower)
                                {
                                    Spectre.Instance.RpcAddSatiety.Invoke(((spectreFollower.MySpectre as RuntimeRole)?.MyPlayer!, SpectreFollower.SatietyRateOption));
                                    SpectreFollower.StatsEatFries.Progress();
                                }

                                Close();
                                if (MyDish != null) FriesDishManager.RpcUpdateFries.Invoke((MyDish.DishId, true, GamePlayer.LocalPlayer));
                            }
                        }
                    });
                }
            }
        }

        public override void Close()
        {
            this.CloseInternal();
        }
    }

}    
