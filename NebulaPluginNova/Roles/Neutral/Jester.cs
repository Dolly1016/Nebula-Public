using Nebula.Configuration;
using Nebula.Modules.GUIWidget;
using Nebula.Roles.Scripts;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;

namespace Nebula.Roles.Neutral;

public class Jester : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.jester", new(253,84,167), TeamRevealType.OnlyMe);

    private Jester() : base("jester", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [VentConfiguration, CanDragDeadBodyOption, CanFixLightOption, CanFixCommsOption,
        TaskConfiguration.AsGroup(GroupConfigurationColor.ToDarkenColor(MyTeam.Color)),
        new GroupConfiguration("options.role.jester.group.perk", [JesterPerkOption, NumOfPerksOption, RandomPerkOption, OnlyPlantPerksOption, ..PerkEntries.Select(e => e.Configuration)], GroupConfigurationColor.ToDarkenColor(MyTeam.Color))
        ]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Jester.png");
    }

    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private BoolConfiguration CanDragDeadBodyOption = NebulaAPI.Configurations.Configuration("options.role.jester.canDragDeadBody", true);
    static private BoolConfiguration CanFixLightOption = NebulaAPI.Configurations.Configuration("options.role.jester.canFixLight", false);
    static private BoolConfiguration CanFixCommsOption = NebulaAPI.Configurations.Configuration("options.role.jester.canFixComms", false);
    static private BoolConfiguration JesterPerkOption = NebulaAPI.Configurations.Configuration("options.role.jester.perk", false);
    static private IntegerConfiguration NumOfPerksOption = NebulaAPI.Configurations.Configuration("options.role.jester.numOfPerks", (1, 4), 2, () => JesterPerkOption);
    static private BoolConfiguration RandomPerkOption = NebulaAPI.Configurations.Configuration("options.role.jester.randomPerk", false, () => JesterPerkOption);
    static private BoolConfiguration OnlyPlantPerksOption = NebulaAPI.Configurations.Configuration("options.role.jester.onlyPlantPerks", true, () => JesterPerkOption);

    private class JesterPerkEntry
    {
        private readonly Func<PerkFunctionalDefinition> perkGetter;
        private readonly Func<bool> gainableCondition;
        private readonly IOrderedSharableVariable<bool> enabled;
        public PerkFunctionalDefinition Perk => perkGetter.Invoke();
        public IConfiguration Configuration { get; }

        public bool IsEnabled => enabled.CurrentValue;

        public bool IsGainableInCurrentSettings => gainableCondition.Invoke();

        public bool IsSpawnableFromPlants => Perk.SpawnRate.Value > 0;

        public bool IsAllowedByPlantSettings => !OnlyPlantPerksOption || IsSpawnableFromPlants;

        public JesterPerkEntry(string id, Func<PerkFunctionalDefinition> perk, Func<bool>? gainableCondition = null)
        {
            string configId = "options.role.jester.perkEnabled." + id;
            this.perkGetter = perk;
            this.gainableCondition = gainableCondition ?? (() => true);
            this.enabled = NebulaAPI.Configurations.SharableVariable(configId, true);
            this.Configuration = NebulaAPI.Configurations.Configuration(
                () => IsShown ? "  " + Perk.PerkDefinition.DisplayName + ": " + BoolConfigurationImpl.GetDisplayValue(IsEnabled) : null,
                () => GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Left,
                    Perk.PerkDefinition.GetPerkImageWidget(true, overlay: () => Perk.PerkDefinition.GetPerkWidget()),
                    new NoSGUIMargin(Virial.Media.GUIAlignment.Center, new(0.25f, 0f)),
                    ConfigurationAssets.GetOptionTitle(GUI.API.LocalizedTextComponent(Perk.PerkDefinition.DisplayNameTranslationKey), configId),
                    ConfigurationAssets.Semicolon,
                    new NoSGUIMargin(Virial.Media.GUIAlignment.Center, new(0.1f, 0f)),
                    new GUIButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsButtonMedium), new LazyTextComponent(() => BoolConfigurationImpl.GetDisplayValue(IsEnabled)))
                    {
                        OnClick = _ => { enabled.CurrentValue = !enabled.CurrentValue; NebulaAPI.Configurations.RequireUpdateSettingScreen(); },
                        Color = VColor.Lerp(VColor.White, IsEnabled ? VColor.Cyan : VColor.Red, 0.65f)
                    }),
                () => IsShown);
        }

        private bool IsShown => JesterPerkOption && IsAllowedByPlantSettings;
    }

    static private bool IsSpawnable(DefinedRole role) => (role as ISpawnable)?.IsSpawnable ?? false;

    static private readonly JesterPerkEntry[] PerkEntries = [
        new("sniperIcon", () => Perks.SniperIcon.Def, () => IsSpawnable(Impostor.Sniper.MyRole)),
        new("raiderEvidence", () => Perks.RaiderEvidence.Def, () => IsSpawnable(Impostor.Raider.MyRole)),
        new("fakeBubblegun", () => Perks.FakeBubblegun.Def, () => IsSpawnable(Impostor.Bubblegun.MyRole)),
        new("handOfMercy", () => Perks.HandOfMercy.Def),
        new("decelTrap", () => Perks.PlaceTrap.DecelTrap, () => IsSpawnable(Complex.Trapper.MyEvilRole) || IsSpawnable(Complex.Trapper.MyNiceRole)),
        new("ventRelocate", () => Perks.VentRelocate.Def),
        new("monowayRotate", () => Perks.Monoway.DefRotate),
        new("eraseBody", () => Perks.EraseBody.Def),
        ];
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.jester.vent", true);
    static private ITaskConfiguration TaskConfiguration = NebulaAPI.Configurations.TaskConfiguration("options.role.jester.task", false, true, translationKey: "options.role.jester.task");
    static public bool RequiresTasksForWin => TaskConfiguration.RequiresTasks;

    static public Jester MyRole = new Jester();
    static private readonly GameStatsEntry StatsPerk = NebulaAPI.CreateStatsEntry("stats.jester.perk", GameStatsCategory.Roles, MyRole, annotation: GameStatsAnnotation.From2611);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => CanDragDeadBodyOption;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        if(CanDragDeadBodyOption) yield return new(Draggable.ButtonImage, "role.jester.ability.drag");
        if (JesterPerkOption) yield return new(PerkButtonImage, "role.jester.ability.perk");
    }

    static private readonly Image PerkWindowBackSprite = SpriteLoader.FromResource("Nebula.Resources.JesterUI.png", 130f);
    static private readonly Image PerkButtonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.JesterPerkButton.png", 115f);
    static private readonly PerkDefinition EmptyPerk = new("empty.jester");
    static private readonly PerkFrameInfo PerkFrame = new(PerkFunctionalDefinition.Category.Plain, null);
    private static int NumOfPerkSlots => NumOfPerksOption;
    private const float GuideTextY = -0.75f;

    static private PerkFunctionalDefinition[] AllPerkCandidates => allPerkCandidates ??= PerkEntries.Select(e => e.Perk).ToArray();
    static private PerkFunctionalDefinition[]? allPerkCandidates = null;
    static private int GetPerkId(PerkFunctionalDefinition perk) => Array.IndexOf(AllPerkCandidates, perk);
    static private PerkFunctionalDefinition? GetPerkById(int id) => 0 <= id && id < AllPerkCandidates.Length ? AllPerkCandidates[id] : null;

    static private IEnumerable<PerkFunctionalDefinition> GetSelectablePerks() => PerkEntries.Where(e => e.IsEnabled && e.IsGainableInCurrentSettings && e.IsAllowedByPlantSettings).Select(e => e.Perk);

    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;
        private Scripts.Draggable? draggable = null;

        private PerkSlot[] perkSlots = [];
        private bool HasEmptyPerkSlot => perkSlots.Any(s => !s.HasPerk);
        private bool IsHoldingPerk(PerkFunctionalDefinition perk) => perkSlots.Any(s => s.CurrentPerk == perk);

        private bool hasGainablePerk = false;
        private void UpdateHasGainablePerk() => hasGainablePerk = GetSelectablePerks().Any(CanGainPerk);

        private readonly List<int> gainedPerks = [];

        public Instance(GamePlayer player, int[]? arguments) : base(player, VentConfiguration)
        {
            var numOfPerks = arguments?.Get(0, 0) ?? 0;
            for(int i = 0; i < numOfPerks; i++)
            {
                var perkId = arguments?.Get(i + 1, -1) ?? -1;
                var perk = GetPerkById(perkId);
                if (perk != null) gainedPerks.Add(perkId);
            }
        }

        int[]? RuntimeAssignable.RoleArguments => [gainedPerks.Count, ..gainedPerks];

        static private readonly RemoteProcess<(GamePlayer player, int perkId)> RpcGainPerk = new("JesterGainPerk", (message, _) =>
        {
            if (message.player.Role is Instance jester && !jester.gainedPerks.Contains(message.perkId)) jester.gainedPerks.Add(message.perkId);
        });

        private void SetUpPerks()
        {
            perkSlots = Helpers.Sequential(NumOfPerkSlots).Select(i => new PerkSlot(PerkFunctionalDefinition.Category.Plain, EmptyPerk, 100 - i, null, this)).ToArray();

            foreach (var perk in gainedPerks.Select(GetPerkById)) if (perk != null && CanGainPerk(perk)) perkSlots.First(s => !s.HasPerk).SetPerk(perk);

            //ランダム割り当て
            if (gainedPerks.Count == 0 && RandomPerkOption)
            {
                foreach (var perk in GetSelectablePerks().Shuffle())
                {
                    if (!HasEmptyPerkSlot) break;
                    if (CanGainPerk(perk)) GainPerk(perk, false);
                }
            }

            UpdateHasGainablePerk();

            var perkButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, 0f, "jesterPerk", PerkButtonImage, visibility: _ => hasGainablePerk);
            perkButton.OnClick = _ => OpenPerkWindow();
        }

        private bool CanGainPerk(PerkFunctionalDefinition perk) => HasEmptyPerkSlot && !IsHoldingPerk(perk);
        private void GainPerk(PerkFunctionalDefinition perk, bool byChoice = true)
        {
            if (!CanGainPerk(perk)) return;
            perkSlots.First(s => !s.HasPerk).SetPerk(perk);
            RpcGainPerk.Invoke((MyPlayer, GetPerkId(perk)));
            UpdateHasGainablePerk();
            if (byChoice) StatsPerk.Progress();
        }

        private void OpenPerkWindow()
        {
            var window = MetaScreen.GenerateWindow(new(7.6f, 1.8f), AmongUsLLImpl.HudManagerInstance.transform, new(0f, 0f, -400f), true, true, false, false, true);
            var windowParent = window.transform.parent;

            var back = UnityHelper.CreateObject<SpriteRenderer>("JesterBackground", windowParent, new(0f, 0f, 0.1f));
            back.sprite = PerkWindowBackSprite.GetSprite();
            back.gameObject.layer = LayerExpansion.GetUILayer();

            //パーク一覧の更新で消えないよう、案内文はウィンドウ本体に直接配置する。
            var guideAttr = new Virial.Text.TextAttribute(GUI.API.GetAttribute(Virial.Text.AttributeParams.StandardBaredBold)) { FontSize = new(2.2f), Size = new(7f, 0.5f) };
            var guide = GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, guideAttr, "role.jester.perk.guide")
                .Instantiate(new(new(0.5f, 0.5f), new(0f, GuideTextY, -0.1f)), new(7f, 0.5f), out _);
            if (guide != null)
            {
                guide.transform.SetParent(windowParent, false);
                //文字はsortingOrderが高く設定されるため、そのままではz座標によらずオーバーレイより手前に描画されてしまう。
                //SortingGroupにまとめて外から見た描画順を0にし、オーバーレイとはz座標で前後関係が決まるようにする。
                guide.AddComponent<UnityEngine.Rendering.SortingGroup>();
            }

            void UpdateWidget()
            {
                var perks = GetSelectablePerks().ToArray();
                List<Virial.Media.GUIWidget?> widgets = [];
                for (int i = 0; i < perks.Length; i++)
                {
                    var perk = perks[i];
                    bool canGain = CanGainPerk(perk);
                    if (i > 0) widgets.Add(GUI.API.HorizontalMargin(0.15f));
                    widgets.Add(perk.PerkDefinition.GetPerkImageWidget(false, () =>
                    {
                        if (!CanGainPerk(perk)) return;
                        GainPerk(perk);
                        if (hasGainablePerk) UpdateWidget(); else window.CloseScreen();
                    }, () => perk.PerkDefinition.GetPerkWidget(), canGain ? null : UnityEngine.Color.gray, true, PerkFrame));
                }
                window.SetWidget(GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center, widgets), new VVector2(0.5f, 0.5f), out _);
            }
            UpdateWidget();

            ManagedEffects.Lerp(0.1f, p => windowParent.localScale = new VVector3(p, p, 1f)).StartOnScene();
        }

        void SetJesterTasks()
        {
            if (!TaskConfiguration.RequiresTasks) return;
            if (AmOwner)
            {
                using (RPCRouter.CreateSection("JesterTask"))
                {
                    TaskConfiguration.GetTasks(out var s, out var l, out var c);
                    MyPlayer.Tasks.Unbox().ReplaceTasksAndRecompute(s, l, c);
                    MyPlayer.Tasks.Unbox().BecomeToOutsider();
                }
            }
        }

        public override void OnActivated()
        {
            if (CanDragDeadBodyOption) new Scripts.Draggable(MyPlayer).Register(this);
            if (AmOwner && JesterPerkOption) SetUpPerks();
        }

        void OnGameStart(GameStartEvent ev) => SetJesterTasks();

        bool RuntimeAssignable.CanFixComm => CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => CanFixLightOption;


        StaticAchievementToken? acTokenCommon = null;
        [OnlyMyPlayer]
        void OnVotedForMeLocal(PlayerVotedLocalEvent ev)
        {
            if (ev.Voters.Any(v => !(v.AmOwner)))
                acTokenCommon ??= new StaticAchievementToken("jester.common1");
            if (NebulaGameManager.Instance?.AllPlayerInfo.All(p => p.IsDead || ev.Voters.Any(v => v.PlayerId == p.PlayerId)) ?? false)
                new StaticAchievementToken("jester.challenge");
            
            if(ev.Voters.Count > 0) ModSingleton<IWinningOpportunity>.Instance?.RpcSetOpportunity(MyTeam, ev.Voters.Count / 4f + 0.1f);
        }

        RoleTaskType RuntimeRole.TaskType => TaskConfiguration.RequiresTasks ? RoleTaskType.RoleTask : RoleTaskType.NoTask;

        [OnlyHost, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {
            if (!RequiresTasksForWin || (ev.Player?.Tasks.IsCompletedCurrentTasks ?? false)) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JesterWin, GameEndReason.Special, BitMasks.AsPlayer(1u << ev.Player.PlayerId));
        }
    }
}

