using Epic.OnlineServices.Presence;
using NAudio.CoreAudioApi;

using Nebula.Game.Statistics;
using Nebula.Roles.Complex;
using Nebula.Roles.Modifier;
using Nebula.VoiceChat;
using System.ComponentModel;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Jackal : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static readonly public RoleTeam MyTeam = new Team("teams.jackal", new(8, 190, 245), TeamRevealType.OnlyMe, () => KillCooldown);

    private Jackal() : base("jackal", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [KillCoolDownOption, CanCreateSidekickOption, NumOfKillingToCreateSidekickOption, JackalizedImpostorOption])
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Sidekick.MyRole.ConfigurationHolder!]);
    }

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
        if (ability != null) return new UsurpedJackalizedAbility(player, role!, ability);
        return null;
    }

    private class UsurpedJackalizedAbility : FlexibleLifespan, IUsurpableAbility
    {
        public DefinedRole Role => jackalizedRole;
        private DefinedRole jackalizedRole;
        private IUsurpableAbility jackalizedAbility;
        public IUsurpableAbility Ability => jackalizedAbility;
        bool IUsurpableAbility.IsUsurped => jackalizedAbility.IsUsurped;
        bool IUsurpableAbility.Usurp() => jackalizedAbility.Usurp();
        public GamePlayer MyPlayer { get; private init; }
        public bool AmOwner => MyPlayer.AmOwner;

        public UsurpedJackalizedAbility(GamePlayer player, DefinedRole role, IUsurpableAbility ability)
        {
            this.MyPlayer = player;
            this.jackalizedRole = role;
            this.jackalizedAbility = ability.Register(this);
        }

        IEnumerable<IPlayerAbility> IPlayerAbility.SubAbilities => [jackalizedAbility];
        int[] IPlayerAbility.AbilityArguments => [jackalizedRole.Id, ..jackalizedAbility.AbilityArguments];
    }

    string DefinedRole.GetDisplayName(IPlayerAbility ability)
    {
        if (ability is UsurpedJackalizedAbility a) return a.Role.GetDisplayName(a.Ability);
        return (this as DefinedRole).DisplayName;
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
            if (player.Unbox().AllModifiers.Any(m => m is SidekickModifier.Instance sidekick && sidekick.JackalTeamId == JackalTeamId)) return true;
            return false;
        }

        public bool IsSameTeam(GamePlayer? player)
        {
            if (IsMySidekick(player)) return true;
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

                var myTracker = ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.StandardPredicate(p) && !IsMySidekick(p), null, Impostor.Impostor.CanKillHidingPlayerOption).Register(this);

                GameObject? lockSprite = null;
                TMPro.TextMeshPro? leftText = null;
                ModAbilityButton? sidekickButton = null;

                if ((inherited == 0 && CanCreateSidekickOption) || Sidekick.CanCreateSidekickChainlyOption)
                {
                    sidekickButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, false, true, Virial.Compat.VirtualKeyInput.SidekickAction, null,
                        15f, "sidekick",sidekickButtonSprite,
                        _ => myTracker.CurrentTarget != null && LeftKillingToCreateSidekick <= 0,
                        _ => !hasSidekick);

                    if (LeftKillingToCreateSidekick > 0)
                    {
                        lockSprite = sidekickButton.AddLockedOverlay();
                        sidekickButton.ShowUsesIcon(3, LeftKillingToCreateSidekick.ToString());
                    }
                    sidekickButton.OnClick = (button) =>
                    {
                        if (Sidekick.IsModifierOption)
                            myTracker.CurrentTarget?.Unbox().RpcInvokerSetModifier(SidekickModifier.MyRole, [JackalTeamId]).InvokeSingle();
                        else
                            myTracker.CurrentTarget?.Unbox().RpcInvokerSetRole(Sidekick.MyRole, [JackalTeamId]).InvokeSingle();
                        hasSidekick = true;

                        new StaticAchievementToken("jackal.common1");
                        if (inherited > 0) new StaticAchievementToken("sidekick.common2");
                        StatsSidekick.Progress();
                    };
                }

                var killButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, true, false, Virial.Compat.VirtualKeyInput.Kill, null,
                    KillCooldown,"kill", null!,
                    _ => myTracker.CurrentTarget != null,
                    _ => MyPlayer.AllowToShowKillButtonByAbilities
                    ).SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                killButton.OnClick = (button) =>
                {
                    MyPlayer.MurderPlayer(myTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill, result =>
                    {
                        if (result != KillResult.Kill) return;

                        myKillingTotal++;
                        killingTotal++;

                        if (LeftKillingToCreateSidekick == 0)
                        {
                            if (lockSprite) GameObject.Destroy(lockSprite!.gameObject);
                            if (leftText) GameObject.Destroy(leftText!.transform.parent.gameObject);
                            lockSprite = null;
                            leftText = null;
                        }
                        else
                        {
                            if (leftText) leftText!.text = LeftKillingToCreateSidekick.ToString();
                        }
                    });
                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());

                if (GeneralConfigurations.JackalRadioOption) VoiceChatManager.RegisterRadio(this, IsMySidekick, "voiceChat.info.jackalRadio", MyRole.RoleColor.ToUnityColor());
            }

            JackalizedAbility = MyJackalized?.GetJackalizedAbility(MyPlayer, StoredJackalizedArgument)?.Register(this);
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
        string RuntimeRole.DisplayShort => Jackal.AppendTeamIdIfNecessary((MyRole as DefinedRole).DisplayShort, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, true);

        [OnlyHost, OnlyMyPlayer]
        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            foreach (var player in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (player.IsDead) continue;
                if (IsMySidekick(player))
                {
                    using(RPCRouter.CreateSection("Sidekick")){
                        player.Unbox().RpcInvokerSetRole(Jackal.MyRole, RoleArgumentsForSidekick).InvokeSingle();
                        player.Unbox().RpcInvokerUnsetModifier(SidekickModifier.MyRole).InvokeSingle();
                    }
                }

            }
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

        bool RuntimeAssignable.CanKill(Virial.Game.Player player) =>!IsSameTeam(player);
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
    private Sidekick() : base("sidekick", Jackal.MyTeam.Color, RoleCategory.NeutralRole, Jackal.MyTeam, [IsModifierOption, SidekickCanKillOption, CanCreateSidekickChainlyOption, KillCoolDownOption], false, optionHolderPredicate: ()=> (Jackal.MyRole as DefinedRole).IsSpawnable && Jackal.CanCreateSidekickOption ) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Jackal.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Title = ConfigurationHolder.Title.WithComparison("jackal.sidekick");
    }
    IEnumerable<DefinedAssignable> DefinedAssignable.AchievementGroups => [Sidekick.MyRole, MyRole];
    string DefinedAssignable.InternalName => "jackal.sidekick";
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    static internal BoolConfiguration IsModifierOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.isModifier", false);
    static internal BoolConfiguration SidekickCanKillOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canKill", false, () => !IsModifierOption);
    static internal BoolConfiguration CanCreateSidekickChainlyOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canCreateSidekickChainly", false);
    static internal BoolConfiguration CanWinAsOriginalTeamOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canWinAsOriginalTeam", true, () => IsModifierOption);
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sidekick.killCoolDown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f, () => SidekickCanKillOption ,() => Jackal.KillCooldown);

    static public Sidekick MyRole = new Sidekick();
    bool ISpawnable.IsSpawnable { get => (Jackal.MyRole as DefinedSingleAssignable).IsSpawnable && Jackal.CanCreateSidekickOption && !IsModifierOption; }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public int JackalTeamId;
        public Instance(GamePlayer player,int jackalTeamId) : base(player)
        {
            JackalTeamId=jackalTeamId;
        }

        int[]? RuntimeAssignable.RoleArguments => [JackalTeamId];

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && Jackal.IsJackalLeader(p, JackalTeamId, true)));
        void RuntimeAssignable.OnActivated()
        {
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
                    VoiceChatManager.RegisterRadio(this, (p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, "voiceChat.info.jackalRadio", MyRole.UnityColor);
            }
        }

        string RuntimeAssignable.DisplayName => Jackal.AppendTeamIdIfNecessary((MyRole as DefinedAssignable).DisplayName, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, false);
        string RuntimeRole.DisplayShort => Jackal.AppendTeamIdIfNecessary((MyRole as DefinedRole).DisplayShort, JackalTeamId, NebulaGameManager.Instance?.CanSeeAllInfo ?? false, true);

        bool RuntimeAssignable.CanKill(Virial.Game.Player player) => !(player.Role is Jackal.Instance ji && ji.IsSameTeam(MyPlayer));
    }
}

public class SidekickModifier : DefinedModifierTemplate, HasCitation, DefinedModifier
{
    static public SidekickModifier MyRole = new SidekickModifier();
    private SidekickModifier() : base("sidekick", Jackal.MyTeam.Color, withConfigurationHolder: false) { }
    IEnumerable<DefinedAssignable> DefinedAssignable.AchievementGroups => [Sidekick.MyRole, MyRole];
    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public int JackalTeamId;

        //設定によっては共存する役職の勝利条件をブロックする
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= !Sidekick.CanWinAsOriginalTeamOption && ev.GameEnd != NebulaGameEnd.JackalWin;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && Jackal.IsJackalLeader(p, JackalTeamId, true)));

        public Instance(GamePlayer player, int jackalTeamId) : base(player)
        {
            JackalTeamId = jackalTeamId;
        }

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
            if (AmOwner)
            {
                AmongUsUtil.PlayCustomFlash(Jackal.MyRole.UnityColor, 0f, 0.25f, 0.4f);
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (GeneralConfigurations.JackalRadioOption)
                    VoiceChatManager.RegisterRadio(this, (p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, "voiceChat.info.jackalRadio", MyRole.UnityColor);                

                if (MyPlayer.Role.Role.Category == RoleCategory.ImpostorRole && MyPlayer.Unbox().TryGetModifier<Lover.Instance>(out _))
                    new StaticAchievementToken("threeRoles");
            }
        }

        bool RuntimeAssignable.CanKill(Virial.Game.Player player) => !(player.Role is Jackal.Instance ji && ji.IsSameTeam(MyPlayer));
        bool RuntimeModifier.MyCrewmateTaskIsIgnored => true;
    }
}
