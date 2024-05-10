using Nebula.Roles.Modifier;
using Nebula.VoiceChat;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Jackal : ConfigurableStandardRole, HasCitation
{
    static public Jackal MyRole = new Jackal();
    static public Team MyTeam = new("teams.jackal", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { if (Sidekick.MyRole.RoleConfig.IsShown) yield return Sidekick.MyRole; }
    public override string LocalizedName => "jackal";
    public override Color RoleColor => new Color(8f / 255f, 190f / 255f, 245f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, player.PlayerId), arguments.Get(1, 0), arguments.Get(2, 0));

    private KillCoolDownConfiguration KillCoolDownOption = null!;
    public NebulaConfiguration CanCreateSidekickOption = null!;
    private NebulaConfiguration NumOfKillingToCreateSidekickOption = null!;
    //private NebulaConfiguration NumOfKillingToWinOption = null!;

    public static bool IsJackal(GamePlayer player, int teamId)
    {
        if (player.Role is Instance j) return j.JackalTeamId == teamId;
        if(player.Role is Sidekick.Instance s) return s.JackalTeamId == teamId;
        return player.Unbox().GetModifiers<SidekickModifier.Instance>().Any(m => m.JackalTeamId == teamId);
    }

    protected override void LoadOptions()
    {
        base.LoadOptions();

        KillCoolDownOption = new(RoleConfig, "killCoolDown",KillCoolDownConfiguration.KillCoolDownType.Relative, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 25f, -5f, 1f);
        CanCreateSidekickOption = new NebulaConfiguration(RoleConfig, "canCreateSidekick", null, false, false);
        NumOfKillingToCreateSidekickOption = new NebulaConfiguration(RoleConfig, "numOfKillingToCreateSidekick", null, 10, 2, 2);
        //NumOfKillingToWinOption = new NebulaConfiguration(RoleConfig, "numOfKillingToWin", null, 10, 2, 2);
    }


    public class Instance : RoleInstance, IBindPlayer
    {
        private ModAbilityButton? killButton = null;
        private ModAbilityButton? sidekickButton = null;
        public override AbstractRole Role => MyRole;
        public int JackalTeamId;
        private int killingTotal = 0;
        private int myKillingTotal = 0;

        private int LeftKillingToCreateSidekick => Math.Max(0, MyRole.NumOfKillingToCreateSidekickOption.GetMappedInt() - myKillingTotal);
        public Instance(GamePlayer player,int jackalTeamId, int totalKilling, int myTotalKilling) : base(player)
        {
            JackalTeamId = jackalTeamId;
            killingTotal = totalKilling;
            myKillingTotal = myTotalKilling;
        }

        static private ISpriteLoader sidekickButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SidekickButton.png", 115f);

        public override int[]? GetRoleArgument() => [JackalTeamId, killingTotal, myKillingTotal];
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

        public override void OnActivated()
        {
            if (AmOwner)
            {
                if (JackalTeamId != MyPlayer.PlayerId)
                    new StaticAchievementToken("sidekick.common1");

                bool hasSidekick = false;

                var myTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => p.PlayerId != MyPlayer.PlayerId && !p.IsDead && !IsMySidekick(p), null, Impostor.Impostor.MyRole.CanKillHidingPlayerOption));

                SpriteRenderer? lockSprite = null;
                TMPro.TextMeshPro? leftText = null;

                if ((JackalTeamId == MyPlayer.PlayerId && MyRole.CanCreateSidekickOption) || Sidekick.MyRole.CanCreateSidekickChainlyOption)
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
                killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
                killButton.SetLabel("kill");

                if (GeneralConfigurations.JackalRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript(IsMySidekick, "voiceChat.info.jackalRadio", MyRole.RoleColor));
                
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
                color = Jackal.MyRole.RoleColor;
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
            if(IsMySidekick(player))color = Jackal.MyRole.RoleColor;
        }

        public override bool HasImpostorVision => true;
        public override bool IgnoreBlackout => true;

        public override void OnGameEnd(EndState endState)
        {
            if (!AmOwner) return;
            if (endState.EndCondition != NebulaGameEnd.JackalWin) return;
            if (!endState.CheckWin(MyPlayer.PlayerId)) return;

            if (endState.EndReason != GameEndReason.Situation) return;

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
        if ((lastRole?.Assignable as RoleInstance)?.Role.Category == RoleCategory.ImpostorRole)
        {
            new AchievementToken<bool>("sidekick.challenge", default, (val, _) =>
                NebulaGameManager.Instance!.EndState!.CheckWin(myPlayer.PlayerId) && NebulaGameManager.Instance!.EndState!.EndCondition == NebulaGameEnd.JackalWin);
        }

    }
}

public class Sidekick : ConfigurableRole, HasCitation
{
    static public Sidekick MyRole = new Sidekick();

    public override RoleCategory Category => RoleCategory.NeutralRole;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return Jackal.MyRole; }

    public override string InternalName => "jackal.sidekick";
    public override string LocalizedName => "sidekick";
    
    public override Color RoleColor => Jackal.MyRole.RoleColor;
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Jackal.MyTeam;

    public override int RoleCount => 0;
    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    public NebulaConfiguration IsModifierOption = null!;
    public NebulaConfiguration SidekickCanKillOption = null!;
    public NebulaConfiguration CanCreateSidekickChainlyOption = null!;
    private KillCoolDownConfiguration KillCoolDownOption = null!;

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

    public override float GetRoleChance(int count) => 0f;

    public override bool IsSpawnable { get => Jackal.MyRole.IsSpawnable && Jackal.MyRole.CanCreateSidekickOption && !IsModifierOption; }

    public class Instance : RoleInstance
    {
        private ModAbilityButton? killButton = null;
        public override AbstractRole Role => MyRole;
        public int JackalTeamId;
        public Instance(GamePlayer player,int jackalTeamId) : base(player)
        {
            JackalTeamId=jackalTeamId;
        }

        public override int[]? GetRoleArgument() => [JackalTeamId];

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.JackalWin && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && Jackal.IsJackal(p, JackalTeamId)));
        public override void OnActivated()
        {
            //サイドキック除去
            MyPlayer.Unbox().UnsetModifierLocal(m=>m.Role == SidekickModifier.MyRole);

            if (AmOwner)
            {
                SidekickAchievementChecker.TriggerSidekickChallenge(MyPlayer);

                if (MyRole.SidekickCanKillOption)
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
                    killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
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

    public class Instance : ModifierInstance
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

        public override void OnActivated()
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
