using Nebula.Compat;
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
    static public Team MyTeam = new("teams.jackal", new(8,190,245), TeamRevealType.OnlyMe);
    static public Jackal MyRole = new Jackal();

    private Jackal() : base("jackal", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [KillCoolDownOption, CanCreateSidekickOption, NumOfKillingToCreateSidekickOption])
    {

    }

    public override IEnumerable<IAssignableBase> RelatedOnConfig() { if (Sidekick.MyRole.RoleConfig.IsShown) yield return Sidekick.MyRole; }
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, player.PlayerId), arguments.Get(1, 0), arguments.Get(2, 0));

    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("role.jackal.killCoolDown", CoolDownType.Relative, (2.5f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static public BoolConfiguration CanCreateSidekickOption = NebulaAPI.Configurations.Configuration("role.jackal.canCreateSidekick", false);
    static private IntegerConfiguration NumOfKillingToCreateSidekickOption = NebulaAPI.Configurations.Configuration("role.jackal.numOfKillingToCreateSidekick", (0, 10), 2);
    //private NebulaConfiguration NumOfKillingToWinOption = null!;

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

        static private ISpriteLoader sidekickButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SidekickButton.png", 115f);

        int[]? RuntimeAssignable.RoleArguments => [JackalTeamId, killingTotal, myKillingTotal];
        public bool CanWinDueToKilling => /*killingTotal >= MyRole.NumOfKillingToWinOption*/ true;
        public bool IsMySidekick(GamePlayer? player)
        {
            if (player == null) return false;
            if (player.Role is Sidekick.Instance sidekick && sidekick.JackalTeamId == JackalTeamId) return true;
            if (player.Unbox().AllModifiers.Any(m => m is SidekickModifier.Instance sidekick && sidekick.JackalTeamId == JackalTeamId)) return true;
            return false;
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                if (JackalTeamId != MyPlayer.PlayerId)
                    new StaticAchievementToken("sidekick.common1");

                bool hasSidekick = false;

                var myTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => p.PlayerId != MyPlayer.PlayerId && !p.IsDead && !IsMySidekick(p), null, Impostor.Impostor.CanKillHidingPlayerOption));

                SpriteRenderer? lockSprite = null;
                TMPro.TextMeshPro? leftText = null;

                if ((JackalTeamId == MyPlayer.PlayerId && CanCreateSidekickOption) || Sidekick.MyRole.CanCreateSidekickChainlyOption)
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

                        if (Sidekick.MyRole.IsModifierOption)
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
                    MyPlayer.MurderPlayer(myTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, true, true);
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
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
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

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            var myInfo = PlayerControl.LocalPlayer.GetModInfo();
            if(myInfo == null) return;

            if (IsMySidekick(myInfo))
            {
                color = Jackal.MyRole.RoleColor.ToUnityColor();
            } 

        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            foreach (var player in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (player.IsDead) continue;
                if (IsMySidekick(player)) player.Unbox().RpcInvokerSetRole(Jackal.MyRole, [JackalTeamId, killingTotal]).InvokeSingle();

            }
        }

        public override void DecorateOtherPlayerName(GamePlayer player, ref string text, ref Color color)
        {
            if(IsMySidekick(player))color = Jackal.MyRole.RoleColor.ToUnityColor();
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
    static public Sidekick MyRole = new Sidekick();
    private Sidekick() : base("sidekick", Jackal.MyTeam.Color, RoleCategory.NeutralRole, Jackal.MyTeam, [], false ) { }
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return Jackal.MyRole; }

    string DefinedAssignable.InternalName => "jackal.sidekick";
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    static internal BoolConfiguration IsModifierOption = NebulaAPI.Configurations.Configuration("role.sidekick.isModifier", false);
    static internal BoolConfiguration SidekickCanKillOption = NebulaAPI.Configurations.Configuration("role.sidekick.canKill", false, () => !IsModifierOption);
    static internal BoolConfiguration CanCreateSidekickChainlyOption = NebulaAPI.Configurations.Configuration("role.sidekick.canCreateSidekickChainly", false);
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("role.sidekick.killCoolDown", CoolDownType.Relative, (2.5f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);

    protected override void LoadOptions()
    {
        base.LoadOptions();

        IsModifierOption = new NebulaConfiguration(RoleConfig, "isModifier", null, false, false);
        SidekickCanKillOption = new NebulaConfiguration(RoleConfig, "canKill", null, false, false);
        SidekickCanKillOption.Predicate = () => !IsModifierOption;
        KillCoolDownOption = new(RoleConfig, "killCoolDown", KillCoolDownConfiguration.KillCoolDownType.Relative, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 25f, -5f, 1f);
        KillCoolDownOption.EditorOption.Predicate = () => SidekickCanKillOption;

        CanCreateSidekickChainlyOption = new NebulaConfiguration(RoleConfig, "canCreateSidekickChainly", null, false, false);

        RoleConfig.SetPredicate(() => Jackal.MyRole.RoleCount > 0 && Jackal.MyRole.CanCreateSidekickOption);
    }

    public override bool IsSpawnable { get => Jackal.MyRole.IsSpawnable && Jackal.MyRole.CanCreateSidekickOption && !IsModifierOption; }

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
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));
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
                        MyPlayer.MurderPlayer(myTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, true, true);
                        button.StartCoolDown();
                    };
                    killButton.CoolDownTimer = Bind(new Timer(KillCoolDownOption.CoolDown).SetAsKillCoolDown().Start());
                    killButton.SetLabel("kill");
                }

                if (GeneralConfigurations.JackalRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript((p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, "voiceChat.info.jackalRadio", MyRole.RoleColor));
            }
        }
    }
}

public class SidekickModifier : AbstractModifier, HasCitation
{
    static public SidekickModifier MyRole = new SidekickModifier();

    public override string LocalizedName => "sidekick";
    public override Color RoleColor => Jackal.MyRole.RoleColor;
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0,0));

    public class Instance : ModifierInstance, RuntimeModifier
    {
        public override AbstractModifier Role => MyRole;
        public int JackalTeamId;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));

        public Instance(GamePlayer player, int jackalTeamId) : base(player)
        {
            JackalTeamId = jackalTeamId;
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (AmOwner || (NebulaGameManager.Instance?.CanSeeAllInfo ?? false)) text += " #".Color(Jackal.MyRole.RoleColor);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (GeneralConfigurations.JackalRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript((p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, "voiceChat.info.jackalRadio", MyRole.RoleColor));                

                if (MyPlayer.Role.Role.Category == RoleCategory.ImpostorRole && MyPlayer.Unbox().TryGetModifier<Lover.Instance>(out _))
                    new StaticAchievementToken("threeRoles");
            }
        }
    }
}
