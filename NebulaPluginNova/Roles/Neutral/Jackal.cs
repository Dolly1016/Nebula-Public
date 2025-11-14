using Epic.OnlineServices.Presence;
using Nebula.Game.Statistics;
using Nebula.Roles.Complex;
using Nebula.Roles.Modifier;
using Nebula.VoiceChat;
using System.ComponentModel;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Roles.Neutral;

[NebulaPreprocess(PreprocessPhase.BuildAssignmentTypes)]
internal static class JackalAssignmentSetUp
{
    static public Virial.Color Color = new(8, 190, 245);
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.RegisterAssignmentType(() => Neutral.Jackal.MyRole, (lastArgs, role) => Jackal.GenerateArgument(lastArgs[0], role), "jackalized", Color, (status, role) => status.HasFlag(AbilityAssignmentStatus.CanLoadToKillNeutral), () => (Jackal.MyRole as ISpawnable).IsSpawnable && Jackal.JackalizedImpostorOption);
    }
}

internal class UsurpedImpostorAbility : FlexibleLifespan, IUsurpableAbility
{
    public DefinedRole Role => jackalizedRole;
    private DefinedRole jackalizedRole;
    private IUsurpableAbility jackalizedAbility;
    public IUsurpableAbility Ability => jackalizedAbility;
    bool IUsurpableAbility.IsUsurped => jackalizedAbility.IsUsurped;
    bool IUsurpableAbility.Usurp() => jackalizedAbility.Usurp();
    public GamePlayer MyPlayer { get; private init; }
    public bool AmOwner => MyPlayer.AmOwner;

    public UsurpedImpostorAbility(GamePlayer player, DefinedRole role, IUsurpableAbility ability)
    {
        this.MyPlayer = player;
        this.jackalizedRole = role;
        this.jackalizedAbility = ability.Register(this);
    }

    IEnumerable<IPlayerAbility> IPlayerAbility.SubAbilities => [jackalizedAbility];
    int[] IPlayerAbility.AbilityArguments => [jackalizedRole.Id, .. jackalizedAbility.AbilityArguments];
}


public class Jackal : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static readonly public RoleTeam MyTeam = new Team("teams.jackal", JackalAssignmentSetUp.Color, TeamRevealType.OnlyMe, () => KillCooldown);

    private Jackal() : base("jackal", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [KillCoolDownOption, CanCreateSidekickOption, NumOfKillingToCreateSidekickOption, JackalizedImpostorOption],
    othersAssignments: () => {
        return (Sidekick.AssignedSidekickOption /*&& !Sidekick.IsModifierOption*/) ? [new((_, playerId)=> (Sidekick.MyRole, [0, playerId]), RoleCategory.NeutralRole)] : [];
        
    })
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Sidekick.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Jackal.png");
    }

    DefinedRole[] DefinedRole.AdditionalRoles => Sidekick.AssignedSidekickOption /*&& !Sidekick.IsModifierOption*/ ? [Sidekick.MyRole] : [];
    IEnumerable<DefinedRole> DefinedRole.GetGuessableAbilityRoles() => ((this as DefinedRole).IsSpawnable && !JackalizedImpostorOption) ? [this] : [];
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, player.PlayerId), arguments.Get(1, 0), arguments.Get(2, 0), arguments.Get(3, 0), Roles.GetRole(arguments.Get(4, -1)), arguments.Skip(5).ToArray());
    static public int[] GenerateArgument(int jackalTeamId, DefinedRole? jackalized) => [jackalTeamId, 0, 0, 0, jackalized?.Id ?? -1];
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.jackal.killCoolDown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static public BoolConfiguration CanCreateSidekickOption = NebulaAPI.Configurations.Configuration("options.role.jackal.canCreateSidekick", false);
    static private IntegerConfiguration NumOfKillingToCreateSidekickOption = NebulaAPI.Configurations.Configuration("options.role.jackal.numOfKillingToCreateSidekick", (0, 10), 2, () => CanCreateSidekickOption);
    static public BoolConfiguration JackalizedImpostorOption = NebulaAPI.Configurations.Configuration("options.role.jackal.jacklizedImpostor", false);

    static public float KillCooldown => KillCoolDownOption.CoolDown;

    static public Jackal MyRole = new Jackal();
    static private GameStatsEntry StatsSidekick = NebulaAPI.CreateStatsEntry("stats.jackal.sidekick", GameStatsCategory.Roles, MyRole);

    bool DefinedRole.IsKiller => true;

    public static bool IsJackalLeader(GamePlayer player, int teamId, bool excludeDefeatedJackal = false)
    {
        if (player.Role is Instance j) return j.JackalTeamId == teamId && (!excludeDefeatedJackal || !j.IsDefeatedJackal);
        return false;
    }

    public static bool IsJackalTeam(GamePlayer player, int teamId)
    {
        if (player.Role is Instance j) return j.JackalTeamId == teamId;
        if(player.Role is Sidekick.Instance s) return s.JackalTeamId == teamId;
        return player.Unbox().GetModifiers<SidekickModifier.Instance>().Any(m => m.JackalTeamId == teamId);
    }

    static public bool ShouldShowTeamId(int id, bool canSeeAllInfo)
    {
        canSeeAllInfo |= !AmongUsClient.Instance || AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Ended;
        if (!canSeeAllInfo) return false;
        if (id > 0) return true;
        bool moreThanOne = ((Jackal.MyRole as DefinedSingleAssignable)?.AllocationParameters?.RoleCountSum ?? 0) > 1;
        return moreThanOne && canSeeAllInfo;
    }
    static public string GetDisplayTeamId(int id) => ((char)('A' + id)).ToString();

    static public string AppendTeamIdIfNecessary(string orig, int id, bool canSeeAlllInfo, bool withParentheses) => ShouldShowTeamId(id, canSeeAlllInfo) ? withParentheses ? orig + "(" + GetDisplayTeamId(id) + ")" : orig + " " + GetDisplayTeamId(id) : orig;

    IUsurpableAbility? DefinedRole.GetUsurpedAbility(Virial.Game.Player player, int[] arguments)
    {
        var role = Roles.GetRole(arguments.Get(0, -1));
        var ability = role?.GetUsurpedAbility(player, arguments.Skip(1).ToArray());
        if (ability != null) return new UsurpedImpostorAbility(player, role!, ability);
        return null;
    }

    string DefinedRole.GetDisplayName(IPlayerAbility ability)
    {
        if (ability is UsurpedImpostorAbility a) return a.Role.GetDisplayName(a.Ability);
        return (this as DefinedRole).DisplayName;
    }

    string DefinedRole.GetDisplayShort(IPlayerAbility ability)
    {
        if (ability is UsurpedImpostorAbility a) return a.Role.GetDisplayShort(a.Ability);
        return (this as DefinedRole).DisplayShort;
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        public int JackalTeamId;
        private int killingTotal = 0;
        private int myKillingTotal = 0;
        private int inherited = 0;//継承回数
        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => MyJackalized != null ? [MyRole, MyJackalized] : [MyRole];
        public DefinedRole? MyJackalized { get; private set; }
        public IPlayerAbility? JackalizedAbility { get; private set; } = null;
        private int[] StoredJackalizedArgument { get; set; }

        string RuntimeRole.DisplayIntroBlurb => (MyJackalized ?? MyRole).DisplayIntroBlurb;
        string RuntimeRole.DisplayIntroRoleName => (MyJackalized ?? MyRole).DisplayName;
       
        IEnumerable<IPlayerAbility?> RuntimeAssignable.MyAbilities => JackalizedAbility != null ? [JackalizedAbility, ..JackalizedAbility.SubAbilities] : [];

        private int LeftKillingToCreateSidekick => Math.Max(0, NumOfKillingToCreateSidekickOption - myKillingTotal);
        public Instance(GamePlayer player, int jackalTeamId, int totalKilling, int myTotalKilling, int inherited, DefinedRole? jackalized, int[] jackalizedArgument) : base(player)
        {
            JackalTeamId = jackalTeamId;
            killingTotal = totalKilling;
            myKillingTotal = myTotalKilling;
            MyJackalized = jackalized;
            StoredJackalizedArgument = jackalizedArgument;
            this.inherited = inherited;
        }

        static private Image sidekickButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SidekickButton.png", 115f);

        bool RuntimeRole.CanUseVent => true;
        //RoleAssignmentでもこの配置を前提とした部分がある点に注意(ジャッカル化の割り当てのため)
        int[] JackalArguments => [JackalTeamId, killingTotal, myKillingTotal, inherited, MyJackalized?.Id ?? -1];
        int[]? RuntimeAssignable.RoleArguments => JackalArguments.Concat(JackalizedAbility?.AbilityArguments ?? []).ToArray();
        int[]? RuntimeRole.UsurpedAbilityArguments => (JackalizedAbility?.AbilityArguments ?? []).Prepend(MyJackalized?.Id ?? -1).ToArray();
        bool RuntimeRole.CheckGuessAbility(DefinedRole abilityRole) => abilityRole == MyJackalized || abilityRole == MyRole;
        public int[] RoleArgumentsForSidekick
        {
            get
            {
                var arg = (this as RuntimeAssignable).RoleArguments;
                arg[3]++;
                arg[2] = 0;
                return arg;
            }
        }
        public bool IsMySidekick(GamePlayer? player)
        {
            if (player == null) return false;
            if (player.Role is Sidekick.Instance sidekick && sidekick.JackalTeamId == JackalTeamId) return true;
            if (player.Modifiers.Any(m => m is SidekickModifier.Instance sidekick && sidekick.JackalTeamId == JackalTeamId)) return true;
            return false;
        }

        public bool IsSameTeam(GamePlayer? player)
        {
            if (IsMySidekick(player)) return true;
            if (player?.Role is Instance jackal && jackal.JackalTeamId == JackalTeamId) return true;
            return false;
        }

        public bool IsMyJackal(GamePlayer? player)
        {
            if (player?.Role is Instance jackal && jackal.JackalTeamId == JackalTeamId) return true;
            return false;
        }

        //勝敗の決定時に使用
        internal bool IsDefeatedJackal = false;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
        {
            ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && Jackal.IsJackalLeader(p, JackalTeamId, true)));
        }

        void RuntimeRole.Usurp()
        {
            (JackalizedAbility as IUsurpableAbility)?.Usurp();
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                if (inherited > 0) new StaticAchievementToken("sidekick.common1");

                bool hasSidekick = false;

                var myKillTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeLocalKillablePredicate(p) && !IsMySidekick(p.RealPlayer), null, Impostor.Impostor.CanKillHidingPlayerOption);
                var mySidekickTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeStandardPredicate(p) && !IsMySidekick(p.RealPlayer), null, false);

                GameObject? lockSprite = null;
                TMPro.TextMeshPro? leftText = null;
                ModAbilityButton? sidekickButton = null;

                if (CanCreateSidekickOption && ((inherited == 0 && !Sidekick.AssignedSidekickOption) || Sidekick.CanCreateSidekickChainlyOption))
                {
                    sidekickButton = NebulaAPI.Modules.InteractButton(this, MyPlayer, mySidekickTracker, new PlayerInteractParameter(RealPlayerOnly: true), false, true, Virial.Compat.VirtualKeyInput.SidekickAction, null,
                        15f, "sidekick", sidekickButtonSprite,
                        (p, button) =>
                        {
                            if (Sidekick.IsModifierOption)
                                myKillTracker.CurrentTarget?.RealPlayer.AddModifier(SidekickModifier.MyRole, [1, JackalTeamId]);
                            else
                                myKillTracker.CurrentTarget?.RealPlayer.SetRole(Sidekick.MyRole, [1, JackalTeamId]);
                            hasSidekick = true;

                            new StaticAchievementToken("jackal.common1");
                            if (inherited > 0) new StaticAchievementToken("sidekick.common2");
                            StatsSidekick.Progress();
                        },
                        _ => LeftKillingToCreateSidekick <= 0 && !MyPlayer.IsDived,
                        _ => !hasSidekick);

                    if (LeftKillingToCreateSidekick > 0)
                    {
                        lockSprite = sidekickButton.AddLockedOverlay();
                        sidekickButton.ShowUsesIcon(3, LeftKillingToCreateSidekick.ToString());
                    }
                }

                var killButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill,
                    KillCooldown, "kill", ModAbilityButton.LabelType.Impostor, null!,
                    (target, _) =>
                    {
                        var cancelable = GameOperatorManager.Instance?.Run(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target));
                        if (!(cancelable?.IsCanceled ?? false))
                        {
                            //キャンセルされなければキルを実行する
                            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);
                        }

                        //クールダウンをリセットする
                        if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    },
                    null,
                    _ => myKillTracker.CurrentTarget != null && !MyPlayer.IsDived,
                    _ => MyPlayer.AllowToShowKillButtonByAbilities
                    );
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());

                GameOperatorManager.Instance?.Subscribe<PlayerMurderedEvent>(ev =>
                {
                    if (ev.Murderer.AmOwner)
                    {
                        if (ev.Dead.PlayerState == PlayerState.Guessed) return;//推察キルは対象外

                        myKillingTotal++;
                        killingTotal++;

                        if (LeftKillingToCreateSidekick == 0)
                        {
                            if (lockSprite) GameObject.Destroy(lockSprite!.gameObject);
                            sidekickButton?.HideUsesIcon();
                            lockSprite = null;
                            leftText = null;
                        }
                        else if (LeftKillingToCreateSidekick > 0)
                        {
                            sidekickButton?.ShowUsesIcon(3, LeftKillingToCreateSidekick.ToString());
                        }
                    }
                }, this);

                if (GeneralConfigurations.JackalRadioOption)
                {
                    ModSingleton<NoSVCRoom>.Instance?.RegisterRadioChannel(Language.Translate("voiceChat.info.jackalRadio"), 1, IsMySidekick, this, MyRole.UnityColor);
                }
            }

            JackalizedAbility = MyJackalized?.GetAbilityOnRole(MyPlayer, AbilityAssignmentStatus.CanLoadToKillNeutral, StoredJackalizedArgument)?.Register(this);
        }

        //ジャッカルはサイドキックとジャッカルを識別できる
        [Local]
        void DecorateSidekickColor(PlayerDecorateNameEvent ev)
        {
            if (IsSameTeam(ev.Player) && !ev.Player.AmOwner) ev.Color = Jackal.MyRole.RoleColor;
        }
        
        //サイドキックはジャッカルを識別できる
        [OnlyMyPlayer]
        void DecorateJackalColor(PlayerDecorateNameEvent ev)
        {
            if (IsMySidekick(GamePlayer.LocalPlayer) && !ev.Player.AmOwner) ev.Color = Jackal.MyRole.RoleColor;
        }

        string RuntimeAssignable.DisplayName { get { 
                var name = Jackal.AppendTeamIdIfNecessary(MyJackalized?.GetDisplayName(JackalizedAbility!) ?? (MyRole as DefinedRole).DisplayName, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, false);
                return name;
            }
        }
        string RuntimeAssignable.DisplayColoredName => (this as RuntimeAssignable).DisplayName.Color(MyTeam.UnityColor);
        string RuntimeRole.DisplayShort => Jackal.AppendTeamIdIfNecessary(MyJackalized?.GetDisplayShort(JackalizedAbility!) ?? (MyRole as DefinedRole).DisplayShort, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, true);

        void PromoteSidekick(bool isExileEvent)
        {
            if (Sidekick.CanPromoteToJackal)
            {
                foreach (var player in NebulaGameManager.Instance!.AllPlayerInfo)
                {
                    if (player.IsDead) continue;
                    if (IsMySidekick(player))
                    {
                        using (RPCRouter.CreateSection("Sidekick"))
                        {
                            player.SetRole(Jackal.MyRole, RoleArgumentsForSidekick);
                            player.RemoveModifier(SidekickModifier.MyRole);
                        }
                    }
                }
            }
            else if (Sidekick.SuicideOnMyJackalDies)
            {
                if (NebulaGameManager.Instance?.AllPlayerInfo.Find(p => !p.IsDead && IsMySidekick(p), out var sidekick) ?? false)
                {
                    if (isExileEvent)
                    {
                        sidekick.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
                    }
                    else
                    {
                        sidekick.Suicide(PlayerState.Suicide, PlayerState.Suicide, KillParameter.NormalKill);
                    }
                }

            }
        }

        [OnlyHost, OnlyMyPlayer]
        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            //生存している仲間のジャッカルがいればなにもしない。
            if (GamePlayer.AllPlayers.Any(p => IsMyJackal(p) && !p.IsDead)) return;

            PromoteSidekick(ev is PlayerExiledEvent);
        }

        bool RuntimeRole.HasImpostorVision => true;
        bool RuntimeRole.IgnoreBlackout => true;

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.EndCondition != NebulaGameEnd.JackalWin) return;
            if (!ev.EndState.Winners.Test(MyPlayer)) return;

            if (ev.EndState.EndReason != GameEndReason.Situation) return;

            var lastDead = NebulaGameManager.Instance!.AllPlayerInfo.MaxBy(p => p.DeathTime ?? 0f);
            if (lastDead == null || lastDead.MyKiller == null || !lastDead.MyKiller.AmOwner) return;

            if ( /*インポスターが最後に死亡*/ (lastDead as GamePlayer).IsImpostor &&
                /*一人だけ生き残る*/ NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead) == 1)
                new StaticAchievementToken("jackal.challenge");
        }

        [OnlyMyPlayer]
        void OnCheckCanKill(PlayerCheckCanKillLocalEvent ev)
        {
            if (IsSameTeam(ev.Target)) ev.SetAsCannotKillBasically();
        }

        void RuntimeAssignable.OnInactivated() {
            if (!GamePlayer.LocalPlayer!.AmHost || MyPlayer.IsDead) return;
            bool IsOtherMyTeamJackal(GamePlayer p) => p != MyPlayer && !p.IsDead && p.Role.Role == MyRole && p.Role is Instance j && j.JackalTeamId == JackalTeamId;
            if (!GamePlayer.AllPlayers.Any(IsOtherMyTeamJackal))
            {
                PromoteSidekick(false);
            }
        }

        void OnPlayerDie(PlayerDieEvent ev)
        {
            ModSingleton<IWinningOpportunity>.Instance.SetOpportunity(NebulaTeams.JackalTeam, Impostor.KillerOpportunityHelpers.CalcTeamOpportunity(p => MyPlayer == p, IsMySidekick));
        }

        void ClaimJackalRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Jackal.MyTeam) return;
            if(MyPlayer.IsAlive) callback.MarkRemaining();
        }
    }
}

file static class SidekickAchievementChecker
{
    static public void TriggerSidekickChallenge(GamePlayer myPlayer)
    {
        var lastRole = NebulaGameManager.Instance?.RoleHistory.Last(h => h.PlayerId == myPlayer.PlayerId && !h.IsModifier);
        if ((lastRole?.Assignable as RuntimeRole)?.Role.Category == RoleCategory.ImpostorRole)
        {
            new AchievementToken<bool>("sidekick.challenge", default, (val, _) =>
                NebulaGameManager.Instance!.EndState!.Winners.Test(myPlayer) && NebulaGameManager.Instance!.EndState!.EndCondition == NebulaGameEnd.JackalWin);
        }

    }
}

public class Sidekick : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Sidekick() : base("sidekick", Jackal.MyTeam.Color, RoleCategory.NeutralRole, Jackal.MyTeam, [IsModifierOption, InheritanceRuleOption, AssignedSidekickOption, SidekickCanKillOption, CanCreateSidekickChainlyOption, KillCoolDownOption], false, optionHolderPredicate: ()=> (Jackal.MyRole as DefinedRole).IsSpawnable && Jackal.CanCreateSidekickOption ) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Jackal.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Title = ConfigurationHolder.Title.WithComparison("jackal.sidekick");
    }
    IEnumerable<DefinedAssignable> DefinedAssignable.AchievementGroups => [Sidekick.MyRole, MyRole];
    string DefinedAssignable.InternalName => "jackal.sidekick";
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.GetAsBool(0,false), arguments.Get(1, 0));

    static internal BoolConfiguration IsModifierOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.isModifier", false, () => !AssignedSidekickOption!);
    static internal BoolConfiguration SidekickCanKillOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canKill", false, () => !IsModifierOption);
    static internal BoolConfiguration CanCreateSidekickChainlyOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canCreateSidekickChainly", false, () => CanPromoteToJackal!);
    static internal BoolConfiguration CanWinAsOriginalTeamOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canWinAsOriginalTeam", true, () => IsModifierOption || AssignedSidekickOption!);
    static internal BoolConfiguration AssignedSidekickOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.assignedSidekick", false);
    static internal ValueConfiguration<int> InheritanceRuleOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.inheritanceRule", [
        "options.role.sidekick.inheritanceRule.inherit",
        "options.role.sidekick.inheritanceRule.suicide",
        "options.role.sidekick.inheritanceRule.none"
        ], 0);
    static internal bool CanPromoteToJackal => InheritanceRuleOption.GetValue() == 0;
    static internal bool SuicideOnMyJackalDies => InheritanceRuleOption.GetValue() == 1;
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sidekick.killCoolDown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f, () => SidekickCanKillOption ,() => Jackal.KillCooldown);

    static public Sidekick MyRole = new Sidekick();
    static public bool SidekickShouldBeCountAsKillers => CanPromoteToJackal || (!IsModifierOption && SidekickCanKillOption);
    bool ISpawnable.IsSpawnable { get => (Jackal.MyRole as DefinedSingleAssignable).IsSpawnable && Jackal.CanCreateSidekickOption && !IsModifierOption; }
    bool DefinedRole.IsKiller => SidekickShouldBeCountAsKillers;
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public int JackalTeamId;
        private bool ShouldResolveToJackalTeamId = false; //チームIDの代わりにJackalのプレイヤーIDが与えられる場合、true。割り当て直後にチームIDを解決する必要がある。
        public Instance(GamePlayer player,bool givenTeamId, int jackalId) : base(player)
        {
            ShouldResolveToJackalTeamId = !givenTeamId;
            JackalTeamId = jackalId;
        }

        int[]? RuntimeAssignable.RoleArguments => [1, JackalTeamId];

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && Jackal.IsJackalLeader(p, JackalTeamId, true)));
        void RuntimeAssignable.OnActivated()
        {
            if (ShouldResolveToJackalTeamId)
            {
                JackalTeamId = (GamePlayer.GetPlayer((byte)JackalTeamId)?.Role as Jackal.Instance)?.JackalTeamId ?? 0;
                ShouldResolveToJackalTeamId = false;
            }
            if (AmOwner)
            {
                AmongUsUtil.PlayCustomFlash(Jackal.MyRole.UnityColor, 0f, 0.25f, 0.45f);
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (SidekickCanKillOption)
                {
                    var killButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill,
                        KillCoolDownOption.CoolDown, "kill", ModAbilityButton.LabelType.Impostor, null!,
                        (player, _) => {
                            MyPlayer.MurderPlayer(player, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                            NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                        }
                        );
                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
                }

                if (GeneralConfigurations.JackalRadioOption)
                {
                    ModSingleton<NoSVCRoom>.Instance?.RegisterRadioChannel(Language.Translate("voiceChat.info.jackalRadio"), 1, p=> p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, this, MyRole.UnityColor);
                }
            }
        }

        string RuntimeAssignable.DisplayName => Jackal.AppendTeamIdIfNecessary((MyRole as DefinedAssignable).DisplayName, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, false);
        string RuntimeRole.DisplayShort => Jackal.AppendTeamIdIfNecessary((MyRole as DefinedRole).DisplayShort, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, true);

        [OnlyMyPlayer]
        void OnCheckCanKill(PlayerCheckCanKillLocalEvent ev) {
            if (ev.Target.Role is Jackal.Instance ji && ji.IsSameTeam(MyPlayer)) ev.SetAsCannotKillBasically();
        }

        void ClaimJackalRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Jackal.MyTeam) return;
            if (!SidekickShouldBeCountAsKillers) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }
    }
}

public class SidekickModifier : DefinedModifierTemplate, HasCitation, DefinedModifier
{
    static public SidekickModifier MyRole = new SidekickModifier();
    private SidekickModifier() : base("sidekick", Jackal.MyTeam.Color, withConfigurationHolder: false) { }
    IEnumerable<DefinedAssignable> DefinedAssignable.AchievementGroups => [Sidekick.MyRole, MyRole];
    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.GetAsBool(0, false), arguments.Get(1, 0));
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public int JackalTeamId;
        private bool ShouldResolveToJackalTeamId = false; //チームIDの代わりにJackalのプレイヤーIDが与えられる場合、true。割り当て直後にチームIDを解決する必要がある。

        //設定によっては共存する役職の勝利条件をブロックする
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= !Sidekick.CanWinAsOriginalTeamOption && ev.GameEnd != NebulaGameEnd.JackalWin;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && Jackal.IsJackalLeader(p, JackalTeamId, true)));

        public Instance(GamePlayer player, bool givenTeamId, int jackalId) : base(player)
        {
            ShouldResolveToJackalTeamId = !givenTeamId;
            JackalTeamId = jackalId;
        }

        int[] RuntimeAssignable.RoleArguments => [1, JackalTeamId];

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) {
                var append = " #";
                append = Jackal.AppendTeamIdIfNecessary(append, JackalTeamId, canSeeAllInfo, true );
                name += append.Color(Jackal.MyRole.UnityColor);
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            if (ShouldResolveToJackalTeamId)
            {
                JackalTeamId = (GamePlayer.GetPlayer((byte)JackalTeamId)?.Role as Jackal.Instance)?.JackalTeamId ?? 0;
                ShouldResolveToJackalTeamId = false;
            }
            if (AmOwner)
            {
                AmongUsUtil.PlayCustomFlash(Jackal.MyRole.UnityColor, 0f, 0.25f, 0.4f);
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (GeneralConfigurations.JackalRadioOption)
                {
                    ModSingleton<NoSVCRoom>.Instance?.RegisterRadioChannel(Language.Translate("voiceChat.info.jackalRadio"), 1, p => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, this, MyRole.UnityColor);
                }

                if (MyPlayer.Role.Role.Category == RoleCategory.ImpostorRole && MyPlayer.TryGetModifier<Lover.Instance>(out _))
                    new StaticAchievementToken("threeRoles");
            }
        }

        [OnlyMyPlayer]
        void OnCheckCanKill(PlayerCheckCanKillLocalEvent ev)
        {
            if (ev.Target.Role is Jackal.Instance ji && ji.IsSameTeam(MyPlayer)) ev.SetAsCannotKillBasically();
        }

        void ClaimJackalRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Jackal.MyTeam) return;
            if (!Sidekick.SidekickShouldBeCountAsKillers) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        bool RuntimeAssignable.MyCrewmateTaskIsIgnored => true;
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class JackalCriteria : AbstractModule<IGameModeStandard>, IGameOperator
{
    static JackalCriteria() => DIManager.Instance.RegisterModule(() => new JackalCriteria().Register(NebulaAPI.CurrentGame!));

    List<Jackal.Instance> allAliveJackals = [];
    [OnlyHost]
    void OnUpdate(GameUpdateEvent ev)
    {
        bool sidekickShouldBeCountKillers = Sidekick.SidekickShouldBeCountAsKillers;

        int totalAlive = 0;
        bool leftOtherKillers = GameOperatorManager.Instance?.Run(new KillerTeamCallback(NebulaTeams.JackalTeam)).RemainingOtherTeam ?? false; //帆かキラーが残っていても殲滅勝利は可能

        //全生存者数を数え、生存しているジャッカルチームを発見する。
        allAliveJackals.Clear();
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            if (p.IsDead) continue;
            if (p.Role is Jackal.Instance jRole) allAliveJackals.Add(jRole);
            totalAlive++;
        }



        ulong jackalMask = 0;
        int teamCount = 0;
        int winningJackalTeams = 0;
        ulong completeWinningJackalMask = 0;

        //全ジャッカルに対して、各チームごとに勝敗を調べる
        foreach (var jackal in allAliveJackals)
        {
            //ジャッカル陣営の数をカウントする
            ulong myMask = 1ul << jackal!.JackalTeamId;
            if ((jackalMask & myMask) == 0) teamCount++;
            else continue; //既に考慮したチームはスキップしてよい
            jackalMask |= myMask;

            //死亡しておらず、同チーム、かつラバーズでないか相方死亡ラバー。ただし、サイドキックがキル人外でないならジャッカル本人のみ
            int aliveJackals = sidekickShouldBeCountKillers ? NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead && jackal!.IsSameTeam(p) && (!p.TryGetModifier<Lover.Instance>(out var lover) || lover.IsAloneLover) && !p.IsMadmate) : 1;


            //完全殲滅勝利
            if (aliveJackals == totalAlive) completeWinningJackalMask |= myMask;
            //キル勝利
            if (aliveJackals * 2 >= totalAlive && !leftOtherKillers) winningJackalTeams++;
        }

        //キル勝利のトリガー
        if (teamCount == 1 && winningJackalTeams > 0) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JackalWin, GameEndReason.Situation);
        //完全殲滅勝利のトリガー
        if (completeWinningJackalMask != 0)
        {
            allAliveJackals.Do(j => j.IsDefeatedJackal = (completeWinningJackalMask & (1ul << j.JackalTeamId)) == 0ul);
            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JackalWin, GameEndReason.Situation);
        }
    }
};