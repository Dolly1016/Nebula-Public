using Nebula.Configuration;
using Nebula.VoiceChat;
using Virial.Assignable;

namespace Nebula.Roles.Neutral;

public class Jackal : ConfigurableStandardRole
{
    static public Jackal MyRole = new Jackal();
    static public Team MyTeam = new("teams.jackal", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory RoleCategory => RoleCategory.NeutralRole;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return Sidekick.MyRole; }
    public override string LocalizedName => "jackal";
    public override Color RoleColor => new Color(8f / 255f, 190f / 255f, 245f / 255f);
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments.Length >= 1 ? arguments[0] : player.PlayerId, arguments.Length >= 2 ? arguments[1] : MyRole.NumOfKillingToCreateSidekickOption.GetMappedInt());

    private KillCoolDownConfiguration KillCoolDownOption = null!;
    public NebulaConfiguration CanCreateSidekickOption = null!;
    private NebulaConfiguration NumOfKillingToCreateSidekickOption = null!;


    protected override void LoadOptions()
    {
        base.LoadOptions();

        KillCoolDownOption = new(RoleConfig, "killCoolDown",KillCoolDownConfiguration.KillCoolDownType.Relative, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 25f, -5f, 1f);
        CanCreateSidekickOption = new NebulaConfiguration(RoleConfig, "canCreateSidekick", null, false, false);
        NumOfKillingToCreateSidekickOption = new NebulaConfiguration(RoleConfig, "numOfKillingToCreateSidekick", null, 10, 2, 2);
    }


    public class Instance : RoleInstance
    {
        private ModAbilityButton? killButton = null;
        private ModAbilityButton? sidekickButton = null;
        public override AbstractRole Role => MyRole;
        public int JackalTeamId;
        private int leftKillingToCreateSidekick = MyRole.NumOfKillingToCreateSidekickOption.GetMappedInt();
        public Instance(PlayerModInfo player,int jackalTeamId, int needToCreateSidekick) : base(player)
        {
            JackalTeamId = jackalTeamId;
            leftKillingToCreateSidekick = needToCreateSidekick;
        }

        static private ISpriteLoader sidekickButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SidekickButton.png", 115f);

        private bool IsMySidekick(PlayerModInfo? player)
        {
            if (player == null) return false;
            if (player.Role is Sidekick.Instance sidekick && sidekick.JackalTeamId == JackalTeamId) return true;
            if (player.AllModifiers.Any(m => m is SidekickModifier.Instance sidekick && sidekick.JackalTeamId == JackalTeamId)) return true;
            return false;
        }
        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => endCondition == NebulaGameEnd.JackalWin;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                bool hasSidekick = false;

                var myTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => p.PlayerId != MyPlayer.PlayerId && !p.Data.IsDead && !IsMySidekick(p.GetModInfo()), Impostor.Impostor.MyRole.CanKillHidingPlayerOption));

                SpriteRenderer? lockSprite = null;
                TMPro.TextMeshPro? leftText = null;

                if ((JackalTeamId == MyPlayer.PlayerId && MyRole.CanCreateSidekickOption) || Sidekick.MyRole.CanCreateSidekickChainlyOption)
                {
                    sidekickButton = Bind(new ModAbilityButton(true)).KeyBind(Virial.Compat.VirtualKeyInput.Ability);

                    if (leftKillingToCreateSidekick > 0)
                    {
                        lockSprite = sidekickButton.VanillaButton.AddLockedOverlay();
                        leftText = sidekickButton.ShowUsesIcon(3);
                        leftText.text = leftKillingToCreateSidekick.ToString();
                    }
                    sidekickButton.SetSprite(sidekickButtonSprite.GetSprite());
                    sidekickButton.Availability = (button) => myTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove && leftKillingToCreateSidekick <= 0;
                    sidekickButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && !hasSidekick;
                    sidekickButton.OnClick = (button) =>
                    {
                        button.StartCoolDown();

                        if (Sidekick.MyRole.IsModifierOption)
                            myTracker.CurrentTarget.GetModInfo()?.RpcInvokerSetModifier(SidekickModifier.MyRole, new int[] { JackalTeamId }).InvokeSingle();
                        else
                            myTracker.CurrentTarget.GetModInfo()?.RpcInvokerSetRole(Sidekick.MyRole, new int[] { JackalTeamId }).InvokeSingle();
                        hasSidekick = true;
                    };
                    sidekickButton.CoolDownTimer = Bind(new Timer(15).Start());
                    sidekickButton.SetLabel("sidekick");
                }

                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => myTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                killButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                killButton.OnClick = (button) =>
                {
                    MyPlayer.MyControl.ModKill(myTracker.CurrentTarget!, true, PlayerState.Dead, EventDetail.Kill);
                    button.StartCoolDown();

                    leftKillingToCreateSidekick--;
                    if (leftKillingToCreateSidekick == 0)
                    {
                        if (lockSprite) GameObject.Destroy(lockSprite!.gameObject);
                        if (leftText) GameObject.Destroy(leftText!.transform.parent.gameObject);
                        lockSprite = null;
                        leftText = null;
                    }
                    else
                    {
                        if(leftText)leftText!.text = leftKillingToCreateSidekick.ToString();
                    }
                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
                killButton.SetLabel("kill");

                if (GeneralConfigurations.JackalRadioOption)
                {
                    VoiceChatRadio jackalRadio = new(IsMySidekick, Language.Translate("voiceChat.info.jackalRadio"), MyRole.RoleColor);
                    Bind(new NebulaGameScript()
                    {
                        OnActivatedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.AddRadio(jackalRadio),
                        OnReleasedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.RemoveRadio(jackalRadio)
                    });
                }
            }
        }

        public override void OnGameStart()
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

        public override void OnDead()
        {
            foreach (var player in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (player.IsDead) continue;
                if (IsMySidekick(player)) player.RpcInvokerSetRole(Jackal.MyRole, new int[] { JackalTeamId }).InvokeSingle();

            }
        }

        public override void DecorateOtherPlayerName(PlayerModInfo player, ref string text, ref Color color)
        {
            if(IsMySidekick(player))color = Jackal.MyRole.RoleColor;
        }

        public override bool HasImpostorVision => true;
        public override bool IgnoreBlackout => true;
    }
}

public class Sidekick : ConfigurableRole
{
    static public Sidekick MyRole = new Sidekick();

    public override RoleCategory RoleCategory => RoleCategory.NeutralRole;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return Jackal.MyRole; }

    public override string InternalName => "jackal.sidekick";
    public override string LocalizedName => "sidekick";
    
    public override Color RoleColor => Jackal.MyRole.RoleColor;
    public override RoleTeam Team => Jackal.MyTeam;

    public override int RoleCount => 0;
    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments.Length == 1 ? arguments[0] : 0);

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
        public Instance(PlayerModInfo player,int jackalTeamId) : base(player)
        {
            JackalTeamId=jackalTeamId;
        }
        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => endCondition == NebulaGameEnd.JackalWin;
        public override void OnActivated()
        {
            //サイドキック除去
            MyPlayer.UnsetModifierLocal(m=>m.Role == SidekickModifier.MyRole);

            if (AmOwner)
            {
                if (MyRole.SidekickCanKillOption)
                {
                    var myTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => p.PlayerId != MyPlayer.PlayerId && !p.Data.IsDead));

                    killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                    killButton.Availability = (button) => myTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                    killButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                    killButton.OnClick = (button) =>
                    {
                        MyPlayer.MyControl.ModKill(myTracker.CurrentTarget!, true, PlayerState.Dead, EventDetail.Kill);
                        button.StartCoolDown();
                    };
                    killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                    killButton.SetLabel("kill");
                }

                if (GeneralConfigurations.JackalRadioOption)
                {
                    VoiceChatRadio jackalRadio = new((p)=>p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, Language.Translate("voiceChat.info.jackalRadio"), MyRole.RoleColor);
                    Bind(new NebulaGameScript()
                    {
                        OnActivatedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.AddRadio(jackalRadio),
                        OnReleasedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.RemoveRadio(jackalRadio)
                    });
                }
            }
        }
    }
}

public class SidekickModifier : AbstractModifier
{
    static public SidekickModifier MyRole = new SidekickModifier();

    public override string LocalizedName => "sidekick";
    public override Color RoleColor => Jackal.MyRole.RoleColor;
    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments.Length == 1 ? arguments[0] : 0);

    public class Instance : ModifierInstance
    {
        public override AbstractModifier Role => MyRole;
        public int JackalTeamId;

        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => endCondition == NebulaGameEnd.JackalWin;

        public Instance(PlayerModInfo player, int jackalTeamId) : base(player)
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
                if (GeneralConfigurations.JackalRadioOption)
                {
                    VoiceChatRadio jackalRadio = new((p) => p.Role is Jackal.Instance jackal && jackal.JackalTeamId == JackalTeamId, Language.Translate("voiceChat.info.jackalRadio"), MyRole.RoleColor);
                    Bind(new NebulaGameScript()
                    {
                        OnActivatedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.AddRadio(jackalRadio),
                        OnReleasedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.RemoveRadio(jackalRadio)
                    });
                }
            }
        }
    }
}
