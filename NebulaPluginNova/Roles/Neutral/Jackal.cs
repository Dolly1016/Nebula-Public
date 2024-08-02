using Nebula.Compat;
using Nebula.Game.Statistics;
using Nebula.Roles.Modifier;
using Nebula.VoiceChat;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Jackal : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public RoleTeam MyTeam = new Team("teams.jackal", new(8, 190, 245), TeamRevealType.OnlyMe);

    private Jackal() : base("jackal", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [KillCoolDownOption, CanCreateSidekickOption, NumOfKillingToCreateSidekickOption])
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Sidekick.MyRole.ConfigurationHolder!]);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, player.PlayerId), arguments.Get(1, 0), arguments.Get(2, 0));

    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.jackal.killCoolDown", CoolDownType.Relative, (2.5f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static public BoolConfiguration CanCreateSidekickOption = NebulaAPI.Configurations.Configuration("options.role.jackal.canCreateSidekick", false);
    static private IntegerConfiguration NumOfKillingToCreateSidekickOption = NebulaAPI.Configurations.Configuration("options.role.jackal.numOfKillingToCreateSidekick", (0, 10), 2);
    //private NebulaConfiguration NumOfKillingToWinOption = null!;

    static public Jackal MyRole = new Jackal();
    public static bool IsJackal(GamePlayer player, int teamId)
    {
        if (player.Role is Instance j) return j.JackalTeamId == teamId;
        if(player.Role is Sidekick.Instance s) return s.JackalTeamId == teamId;
        return player.Unbox().GetModifiers<SidekickModifier.Instance>().Any(m => m.JackalTeamId == teamId);
    }


    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? killButton = null;
        private ModAbilityButton? sidekickButton = null;
        public int JackalTeamId;
        private int killingTotal = 0;
        private int myKillingTotal = 0;

        private int LeftKillingToCreateSidekick => Math.Max(0, NumOfKillingToCreateSidekickOption - myKillingTotal);
        public Instance(GamePlayer player,int jackalTeamId, int totalKilling, int myTotalKilling) : base(player)
        {
            JackalTeamId = jackalTeamId;
            killingTotal = totalKilling;
            myKillingTotal = myTotalKilling;
        }

        static private Image sidekickButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SidekickButton.png", 115f);

        bool RuntimeRole.CanUseVent => true;
        int[]? RuntimeAssignable.RoleArguments => [JackalTeamId, killingTotal, myKillingTotal];
        public bool CanWinDueToKilling => /*killingTotal >= MyRole.NumOfKillingToWinOption*/ true;
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

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                if (JackalTeamId != MyPlayer.PlayerId)
                    new StaticAchievementToken("sidekick.common1");

                bool hasSidekick = false;

                var myTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.StandardPredicate(p) && !IsMySidekick(p), null, Impostor.Impostor.CanKillHidingPlayerOption));

                SpriteRenderer? lockSprite = null;
                TMPro.TextMeshPro? leftText = null;

                if ((JackalTeamId == MyPlayer.PlayerId && CanCreateSidekickOption) || Sidekick.CanCreateSidekickChainlyOption)
                {
                    sidekickButton = Bind(new ModAbilityButton(true)).KeyBind(Virial.Compat.VirtualKeyInput.Ability);

                    if (LeftKillingToCreateSidekick > 0)
                    {
                        lockSprite = sidekickButton.VanillaButton.AddLockedOverlay();
                        leftText = sidekickButton.ShowUsesIcon(3);
                        leftText.text = LeftKillingToCreateSidekick.ToString();
                    }
                    sidekickButton.SetSprite(sidekickButtonSprite.GetSprite());
                    sidekickButton.Availability = (button) => myTracker.CurrentTarget != null && MyPlayer.CanMove && LeftKillingToCreateSidekick <= 0;
                    sidekickButton.Visibility = (button) => !MyPlayer.IsDead && !hasSidekick;
                    sidekickButton.OnClick = (button) =>
                    {
                        button.StartCoolDown();

                        if (Sidekick.IsModifierOption)
                            myTracker.CurrentTarget?.Unbox().RpcInvokerSetModifier(SidekickModifier.MyRole, [JackalTeamId]).InvokeSingle();
                        else
                            myTracker.CurrentTarget?.Unbox().RpcInvokerSetRole(Sidekick.MyRole, [JackalTeamId]).InvokeSingle();
                        hasSidekick = true;

                        new StaticAchievementToken("jackal.common1");
                        if (JackalTeamId != MyPlayer.PlayerId) new StaticAchievementToken("sidekick.common2");
                    };
                    sidekickButton.CoolDownTimer = Bind(new Timer(15).Start());
                    sidekickButton.SetLabel("sidekick");
                }

                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => myTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.Visibility = (button) => !MyPlayer.IsDead;
                killButton.OnClick = (button) =>
                {
                    MyPlayer.MurderPlayer(myTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    button.StartCoolDown();

                    if (LeftKillingToCreateSidekick == 0)
                    {
                        if (lockSprite) GameObject.Destroy(lockSprite!.gameObject);
                        if (leftText) GameObject.Destroy(leftText!.transform.parent.gameObject);
                        lockSprite = null;
                        leftText = null;
                    }
                    else
                    {
                        if(leftText)leftText!.text = LeftKillingToCreateSidekick.ToString();
                    }
                };
                killButton.CoolDownTimer = Bind(new Timer(KillCoolDownOption.CoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                killButton.SetLabel("kill");

                if (GeneralConfigurations.JackalRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript(IsMySidekick, "voiceChat.info.jackalRadio", MyRole.RoleColor.ToUnityColor()));
                
            }
        }

        [OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            myKillingTotal++;
            killingTotal++;
        }

        //勝利条件を失ったらどうすればいいのか？
        /*
        void IGameEntity.OnPlayerDead(Virial.Game.Player dead)
        {
            if(AmOwner && !MyPlayer.IsDead)
            {
                int aliveElse = NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead && !p.AmOwner);
                if (killingTotal + aliveElse < MyRole.NumOfKillingToWinOption) MyPlayer.MyControl.ModFlexibleKill(MyPlayer.MyControl, false, PlayerStates.Suicide, EventDetail.Kill, true);
            }
        }
        */

        public void OnGameStart(GameStartEvent ev)
        {
            JackalTeamId = MyPlayer.PlayerId;
        }

        //ジャッカルはサイドキックを識別できる
        [Local]
        void DecorateSidekickColor(PlayerDecorateNameEvent ev)
        {
            if (IsMySidekick(ev.Player)) ev.Color = Jackal.MyRole.RoleColor;
        }
        
        //サイドキックはジャッカルを識別できる
        [OnlyMyPlayer]
        void DecorateJackalColor(PlayerDecorateNameEvent ev)
        {
            var myInfo = PlayerControl.LocalPlayer.GetModInfo();
            if (myInfo == null) return;

            if (IsMySidekick(myInfo)) ev.Color = Jackal.MyRole.RoleColor;
        }


        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            foreach (var player in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (player.IsDead) continue;
                if (IsMySidekick(player))
                {
                    using(RPCRouter.CreateSection("Sidekick")){
                        player.Unbox().RpcInvokerSetRole(Jackal.MyRole, [JackalTeamId, killingTotal]).InvokeSingle();
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

            var lastDead = NebulaGameManager.Instance!.AllPlayerInfo().MaxBy(p => p.DeathTime ?? 0f);
            if (lastDead == null || lastDead.MyKiller == null || !lastDead.MyKiller.AmOwner) return;

            if ( /*インポスターが最後に死亡*/ (lastDead as GamePlayer).IsImpostor &&
                /*一人だけ生き残る*/ NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead) == 1)
                new StaticAchievementToken("jackal.challenge");
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
    private Sidekick() : base("sidekick", Jackal.MyTeam.Color, RoleCategory.NeutralRole, Jackal.MyTeam, [IsModifierOption, SidekickCanKillOption, CanCreateSidekickChainlyOption, KillCoolDownOption], false, optionHolderPredicate: ()=>Jackal.CanCreateSidekickOption ) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Jackal.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Title = ConfigurationHolder.Title.WithComparison("role.jackal.sidekick.name");
    }

    string DefinedAssignable.InternalName => "jackal.sidekick";
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    static internal BoolConfiguration IsModifierOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.isModifier", false);
    static internal BoolConfiguration SidekickCanKillOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canKill", false, () => !IsModifierOption);
    static internal BoolConfiguration CanCreateSidekickChainlyOption = NebulaAPI.Configurations.Configuration("options.role.sidekick.canCreateSidekickChainly", false);
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sidekick.killCoolDown", CoolDownType.Relative, (2.5f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);

    static public Sidekick MyRole = new Sidekick();
    bool ISpawnable.IsSpawnable { get => (Jackal.MyRole as DefinedSingleAssignable).IsSpawnable && Jackal.CanCreateSidekickOption && !IsModifierOption; }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        private ModAbilityButton? killButton = null;
        DefinedRole RuntimeRole.Role => MyRole;
        public int JackalTeamId;
        public Instance(GamePlayer player,int jackalTeamId) : base(player)
        {
            JackalTeamId=jackalTeamId;
        }

        int[]? RuntimeAssignable.RoleArguments => [JackalTeamId];

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (SidekickCanKillOption)
                {
                    var myTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                    killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                    killButton.Availability = (button) => myTracker.CurrentTarget != null && MyPlayer.CanMove;
                    killButton.Visibility = (button) => !MyPlayer.IsDead;
                    killButton.OnClick = (button) =>
                    {
                        MyPlayer.MurderPlayer(myTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                        button.StartCoolDown();
                    };
                    killButton.CoolDownTimer = Bind(new Timer(KillCoolDownOption.CoolDown).SetAsKillCoolDown().Start());
                    killButton.SetLabel("kill");
                }

                if (GeneralConfigurations.JackalRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript((p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, "voiceChat.info.jackalRadio", MyRole.UnityColor));
            }
        }
    }
}

public class SidekickModifier : DefinedModifierTemplate, HasCitation, DefinedModifier
{
    static public SidekickModifier MyRole = new SidekickModifier();
    private SidekickModifier() : base("sidekick", Jackal.MyTeam.Color, withConfigurationHolder: false) { }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public int JackalTeamId;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));

        public Instance(GamePlayer player, int jackalTeamId) : base(player)
        {
            JackalTeamId = jackalTeamId;
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += " #".Color(Jackal.MyRole.UnityColor);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (GeneralConfigurations.JackalRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript((p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, "voiceChat.info.jackalRadio", MyRole.UnityColor));                

                if (MyPlayer.Role.Role.Category == RoleCategory.ImpostorRole && MyPlayer.Unbox().TryGetModifier<Lover.Instance>(out _))
                    new StaticAchievementToken("threeRoles");
            }
        }
    }
}
