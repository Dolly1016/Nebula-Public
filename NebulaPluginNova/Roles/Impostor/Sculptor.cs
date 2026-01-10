using Il2CppInterop.Runtime.Injection;
using Il2CppSystem;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Player;
using Nebula.Roles.MapLayer;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;
using static Nebula.Roles.Crewmate.Doppelganger;
using static Virial.Game.OutfitDefinition;

namespace Nebula.Roles.Impostor;

internal class Sculptor : DefinedSingleAbilityRoleTemplate<Sculptor.Ability>, DefinedRole, IAssignableDocument
{
    private Sculptor() : base("sculptor", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SampleCoolDownOption, MaxSamplesOption, CreateCoolDownOption, StayDurationOption, DecoyDetectionRadiusOption]) {
        GameActionTypes.SculptorAction = new("sculptor.summon", this, isPlacementAction: true);
    }

    static private FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.sculptor.sampleCoolDown", (0f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration CreateCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.sculptor.createCoolDown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration StayDurationOption = NebulaAPI.Configurations.Configuration("options.role.sculptor.stayDurationAfterMoving", (0f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration MaxSamplesOption = NebulaAPI.Configurations.Configuration("options.role.sculptor.maxSamples", (1, 10), 3);
    static internal readonly FloatConfiguration DecoyDetectionRadiusOption = NebulaAPI.Configurations.Configuration("options.role.sculptor.detectionRadius", (0f, 10f, 0.25f), 3f, FloatConfigurationDecorator.Ratio);
    //static private BoolConfiguration LoseSampleOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.sculptor.loseSampleOnMeeting", false);

    public class SculptorMapLayer : FakePlayerMapLayer
    {
        static SculptorMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<SculptorMapLayer>();
        private Ability myAbility = null!;
        public void InjectAbility(Ability ability) => myAbility = ability;
        protected override void OnClick(Vector2 worldPos, Vector2 minimapPos)
        {
            Resources.UnloadUnusedAssets();
            var hudPos = MapBehaviour.Instance.transform.TransformPointLocalToLocal(minimapPos, HudManager.Instance.transform);

            void TrySummonDecoy(OutfitDefinition outfit, byte playerId)
            {
                if (!myAbility.MyPlayer.CanMove)
                {
                    //効果音を出した方がいい?
                    return;
                }
                myAbility.TrySummonDecoy(playerId, outfit, worldPos);
                if (CreateCoolDownOption > 5f) MapBehaviour.Instance.Close();
            }

            if (myAbility.CanCreateDecoy)
            {
                if (myAbility.HasAnyOtherSamples)
                {
                    NebulaManager.Instance.ShowRingMenu(myAbility.AllSamples.Select(s => new RingMenu.RingMenuElement(
                        new NoSGameObjectGUIWrapper(Virial.Media.GUIAlignment.Center, () => (AmongUsUtil.GetPlayerIcon(s.outfit, null, Vector3.zero, new(0.2f, 0.2f, 1f), includePet: false).gameObject, new(0.4f, 0.4f))),
                        () => TrySummonDecoy(s.outfit, s.playerId))).ToArray(), () => this && this.isActiveAndEnabled, null, hudPos);
                }
                else
                {
                    var s = myAbility.AllSamples.First();
                    TrySummonDecoy(s.outfit, s.playerId);
                }
                myAbility.StartCreateCooldown();
            }
        }


        protected override void OnDisable()
        {
            base.OnDisable();
            Resources.UnloadUnusedAssets();
        }
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Skip(1));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;
    static public Sculptor MyRole = new Sculptor();
    static private GameStatsEntry StatsSample = NebulaAPI.CreateStatsEntry("stats.sculptor.sample", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsCreate = NebulaAPI.CreateStatsEntry("stats.sculptor.create", GameStatsCategory.Roles, MyRole);

    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueMapAbility;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(Morphing.Ability.SampleButtonSprite, "role.sculptor.ability.sample");
        yield return new(mapButtonSprite, "role.sculptor.ability.map");
    }

    static private Image mapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyMapImpostorButton.png", 115f);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButtonImpl? sampleButton = null;
        private ModAbilityButtonImpl? summonButton = null;

        List<(byte playerId, PoolablePlayer display, OutfitDefinition outfit)> samples = [];

        internal IEnumerable<(OutfitDefinition outfit, byte playerId)> AllSamples => samples.Select(s => (s.outfit, s.playerId)).Prepend((MyPlayer.DefaultOutfit, MyPlayer.PlayerId));
        internal bool HasAnyOtherSamples => samples.Count > 0;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), .. samples.Select(s => (int)s.playerId)];
        ModAbilityButton mapButton = null!;
        public bool CanCreateDecoy => !mapButton.CoolDownTimer!.IsProgressing;
        public void StartCreateCooldown() => mapButton.StartCoolDown();
        public Ability(GamePlayer player, bool isUsurped, IEnumerable<int> samples) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var playersHolder = HudContent.InstantiateContent("SculptorIcons", true, true, false);
                this.BindGameObject(playersHolder.gameObject);

                void AddPlayer(IPlayerlike player)
                {
                    var outfit = player.RealPlayer.GetOutfit(OutfitPriority.TransformedThrethold);
                    int lastCount = this.samples.Count;
                    
                    var existed = this.samples.FindIndex(s => s.outfit.outfit.ColorId == outfit.outfit.ColorId);
                    if(existed >= 0){
                        this.samples[existed].display.transform.localScale = new(0.5f, 0.5f, 0.5f);
                        return;
                    }
                    if(this.samples.Count >= MaxSamplesOption)
                    {
                        var removed = this.samples[0];
                        this.samples.RemoveAt(0);
                        GameObject.Destroy(removed.display.gameObject);
                    }

                    var icon = AmongUsUtil.GetPlayerIcon(outfit, playersHolder.transform, Vector3.zero, Vector3.one * 0.5f, includePet: false);
                    icon.transform.localPosition = new((float)lastCount * 0.29f, -0.1f, -(float)this.samples.Count * 0.01f);
                    this.samples.Add((player.RealPlayer.PlayerId, icon, outfit));
                }

                var sampleTracker = NebulaAPI.Modules.PlayerlikeTracker(this, MyPlayer);
                var sampleButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "sculptor.sample",
                    SampleCoolDownOption, "sample", Morphing.Ability.SampleButtonSprite,
                    _ => sampleTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                sampleButton.OnClick = (button) => {
                    AddPlayer(sampleTracker.CurrentTarget!);
                    button.StartCoolDown();
                    StatsSample.Progress();
                };
                

                mapButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, null,
                   CreateCoolDownOption, "sculptor.command", mapButtonSprite).SetAsUsurpableButton(this);
                mapButton.OnClick = (button) =>
                {
                    HudManager.Instance.ToggleMapVisible(new MapOptions() { AllowMovementWhileMapOpen = true, Mode = MapOptions.Modes.Normal, ShowLivePlayerPosition = true });
                };
            }
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            Vector3 targetScale = new(0.31f, 0.31f, 1f);
            for(int i = 0; i < samples.Count; i++)
            {
                Vector3 targetPosition = new((float)i * 0.29f - 0.05f, -0.1f, -(float)i * 0.01f);

                var transform = samples[i].display.transform;
                transform.localPosition -= (transform.localPosition - targetPosition).Delta(2.8f, 0.01f);
                transform.localScale -= (transform.localScale - targetScale).Delta(2.9f, 0.02f);
            }
        }

        SculptorMapLayer? mapLayer = null;
        ShowPlayersMapLayer? mapCountLayer = null;

        [Local]
        void OnOpenMap(AbstractMapOpenEvent ev)
        {
            if (ev is MapOpenNormalEvent && !IsUsurped && !MeetingHud.Instance && !MyPlayer.IsDead)
            {
                if (!mapLayer)
                {
                    mapLayer = UnityHelper.CreateObject<SculptorMapLayer>("SculptorLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    this.BindGameObject(mapLayer.gameObject);
                    mapLayer.InjectAbility(this);
                }
                mapLayer!.gameObject.SetActive(true);
            }
            else
            {
                if (mapLayer) mapLayer!.gameObject.SetActive(false);
            }

            if (ev is not MapOpenAdminEvent && !IsUsurped && !MeetingHud.Instance && !MyPlayer.IsDead)
            {
                if (!mapCountLayer)
                {
                    mapCountLayer = UnityHelper.CreateObject<ShowPlayersMapLayer>("SculptorCountLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    this.BindGameObject(mapCountLayer.gameObject);
                    mapCountLayer.SetUp(p => !p.AmOwner && myDecoysLocal.Any(d => d.IsActive && p.Position.Distance(d.Position) < DecoyDetectionRadiusOption), null);
                }
                mapCountLayer!.gameObject.SetActive(true);
            }
            else
            {
                if (mapCountLayer) mapCountLayer!.gameObject.SetActive(false);
            }
        }

        HashSet<IFakePlayer> myDecoysLocal = [];

        [Local]
        void OnInteract(PlayerInteractionFailedForMyFakePlayerEvent ev)
        {
            if (myDecoysLocal.Contains(ev.Target)) DespawnDecoy(ev.Target, true);
        }

        [Local]
        void OnTryMurder(PlayerKillFakePlayerEvent ev)
        {
            if (myDecoysLocal.Contains(ev.Target)) new StaticAchievementToken("sculptor.common1");
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            myDecoysLocal.ToArray().Do(d => DespawnDecoy(d, false));
        }

        private int DecoyOriginalMask = 0;

        internal bool TrySummonDecoy(byte playerId, OutfitDefinition outfit, Vector2 to)
        {
            var path = NavVerticesHelpers.CalcPath(MyPlayer.TruePosition, to);
            if (path == null) return false;

            var decoy = FakePlayerController.SpawnSyncFakePlayer(GamePlayer.GetPlayer(playerId)!, new(MyPlayer.Position, KillCharacteristics.Disappear, true, false, null, new(outfit, "SculptorDecoy", OutfitPriority.FakeSpecialOutfit, true))).BindLifespan(this);
            myDecoysLocal.Add(decoy);
            if (myDecoysLocal.Count(d => d.IsActive) > GamePlayer.AllPlayers.Count(p => !p.IsDead)) new StaticAchievementToken("sculptor.common3");
            NebulaManager.Instance.StartCoroutine(
                   ManagedEffects.Sequence(
                       NavVerticesHelpers.WalkPath(path.Path, path.StopCond, decoy.Logics),
                       ManagedEffects.Wait(StayDurationOption),
                       ManagedEffects.Action(()=>DespawnDecoy(decoy, true))
                       ).WrapToIl2Cpp()
                   );
            StatsCreate.Progress();
            DecoyOriginalMask |= 1 << playerId;
            NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.SculptorAction);
            return true;
        }
        [Local]
        void OnExiled(PlayerExiledEvent ev)
        {
            if (((1 << ev.Player.PlayerId) & DecoyOriginalMask) != 0 && MyPlayer.CanKill(ev.Player)) new StaticAchievementToken("sculptor.challenge");
        }

        [Local]
        void OnStartTaskPhase(TaskPhaseRestartEvent ev) => DecoyOriginalMask = 0;

        internal void DespawnDecoy(IFakePlayer decoy, bool withEffect)
        {
            if (decoy.IsActive)
            {
                using (RPCRouter.CreateSection("DoppelgangerSwap"))
                {
                    if (withEffect) Crewmate.Doppelganger.Ability.RpcPlayerDespawnEffect.Invoke(decoy);
                    decoy.Release();

                }
            }
            myDecoysLocal.Remove(decoy);
        }
    }
}
