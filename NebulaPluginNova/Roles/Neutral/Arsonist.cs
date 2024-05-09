using Nebula.Behaviour;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Arsonist : ConfigurableStandardRole, HasCitation
{
    static public Arsonist MyRole = new Arsonist();
    static public Team MyTeam = new("teams.arsonist", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;

    public override string LocalizedName => "arsonist";
    public override Color RoleColor => new Color(229f / 255f, 93f / 255f, 0f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player,arguments);

    private NebulaConfiguration DouseCoolDownOption = null!;
    private NebulaConfiguration DouseDurationOption = null!;
    private new VentConfiguration VentConfiguration = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);
        DouseCoolDownOption = new NebulaConfiguration(RoleConfig, "douseCoolDown", null, 2.5f, 30f, 2.5f, 10f, 10f);
        DouseDurationOption = new NebulaConfiguration(RoleConfig, "douseDuration", null, 1f, 10f, 0.5f, 3f, 3f);
    }

    public class Instance : RoleInstance, IGamePlayerOperator
    {
        public override AbstractRole Role => MyRole;

        private Timer ventCoolDown = new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;
        private int initialDousedMask = 0;
        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length == 1) initialDousedMask = arguments[0];
        }

        public override int[]? GetRoleArgument()
        {
            int mask = 0;
            foreach(var icon in playerIcons.Where(icon => icon.icon.GetAlpha() > 0.8f))
            {
                mask |= 1 << icon.playerId;
            }
            return new int[] { mask };
        }


        private ModAbilityButton? douseButton = null;
        private ModAbilityButton? igniteButton = null;
        static private ISpriteLoader douseButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DouseButton.png", 115f);
        static private ISpriteLoader IgniteButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.IgniteButton.png", 115f);
        private List<(byte playerId,PoolablePlayer icon)> playerIcons = new();
        private bool canIgnite;

        private bool CheckDoused((byte playerId, PoolablePlayer icon) p) => p.icon.GetAlpha() > 0.8f;
        private bool CheckIgnitable()
        {
            canIgnite = playerIcons.All(CheckDoused);
            return canIgnite;
        }
        
        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => false;

        private void UpdateIcons()
        {
            for(int i=0;i<playerIcons.Count;i++)
            {
                playerIcons[i].icon.transform.localPosition = new(i * 0.29f - 0.3f, -0.1f, -i * 0.01f);
            }
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var IconsHolder = HudContent.InstantiateContent("ArsonistIcons", true, true, false, true);
                this.Bind(IconsHolder.gameObject);

                var ajust = UnityHelper.CreateObject<ScriptBehaviour>("Ajust", IconsHolder.transform, Vector3.zero);
                ajust.UpdateHandler += () =>
                {
                    if (MeetingHud.Instance)
                    {
                        ajust.transform.localScale = new(0.65f, 0.65f, 1f);
                        ajust.transform.localPosition = new(-0.45f, -0.37f, 0f);
                    }
                    else
                    {
                        ajust.transform.localScale = Vector3.one;
                        ajust.transform.localPosition = Vector3.zero;
                    }
                };
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if (p.AmOwner) continue;

                    var icon = AmongUsUtil.GetPlayerIcon(p.Unbox().DefaultOutfit, ajust.transform, Vector3.zero, Vector3.one * 0.31f);
                    icon.ToggleName(false);
                    icon.SetAlpha(0.35f);
                    playerIcons.Add((p.PlayerId,icon));

                    UpdateIcons();
                }

                if (initialDousedMask != 0)
                {
                    foreach (var icon in playerIcons) if (((1 << icon.playerId) & initialDousedMask) != 0) icon.icon.SetAlpha(1f);
                    CheckIgnitable();
                }

                var douseTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => playerIcons.Any(tuple => tuple.playerId == p.PlayerId && tuple.icon.GetAlpha() < 0.8f)));

                douseButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                douseButton.SetSprite(douseButtonSprite.GetSprite());
                douseButton.Availability = (button) => MyPlayer.CanMove && douseTracker.CurrentTarget != null;
                douseButton.Visibility = (button) => !MyPlayer.IsDead && !canIgnite;
                douseButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                douseButton.OnEffectEnd = (button) =>
                {
                    if (douseTracker.CurrentTarget == null) return;

                    if (!button.EffectTimer!.IsInProcess)
                        foreach (var icon in playerIcons) if (icon.playerId == douseTracker.CurrentTarget.PlayerId) icon.icon.SetAlpha(1f);

                    CheckIgnitable();
                    douseButton.StartCoolDown();
                };
                douseButton.OnUpdate = (button) => {
                    if (!button.EffectActive) return;
                    if (douseTracker.CurrentTarget == null) button.InactivateEffect();
                };
                douseButton.CoolDownTimer = Bind(new Timer(0f, MyRole.DouseCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                douseButton.EffectTimer = Bind(new Timer(0f, MyRole.DouseDurationOption.GetFloat()));
                douseButton.SetLabel("douse");

                bool won = false;
                igniteButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                igniteButton.SetSprite(IgniteButtonSprite.GetSprite());
                igniteButton.Availability = (button) => MyPlayer.CanMove;
                igniteButton.Visibility = (button) => !MyPlayer.IsDead && canIgnite && !won;
                igniteButton.OnClick = (button) => {
                    NebulaGameManager.Instance.RpcInvokeSpecialWin(NebulaGameEnd.ArsonistWin, 1 << MyPlayer.PlayerId);
                    won = true;
                };
                igniteButton.SetLabel("ignite");
            }

        }

        public override void LocalUpdate()
        {
            UpdateIcons();
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            playerIcons.RemoveAll(tuple =>
            {
                if (NebulaGameManager.Instance?.GetPlayer(tuple.playerId)?.IsDead ?? true)
                {
                    GameObject.Destroy(tuple.icon.gameObject);
                    return true;
                }
                return false;
            });
            CheckIgnitable();
        }

        StaticAchievementToken? acTokenCommon;

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (AmOwner)
            {
                if (acTokenCommon == null && playerIcons.Count(icon => CheckDoused(icon) && ((!NebulaGameManager.Instance?.GetPlayer(icon.playerId)?.IsDead) ?? false)) >= 3)
                    acTokenCommon = new("arsonist.common1");
            }
        }

        AchievementToken<bool>? acTokenChallenge;
        void IGameOperator.OnPlayerExiled(GamePlayer exiled)
        {
            if (AmOwner)
            {
                var notDoused = playerIcons.FindAll(icon => !CheckDoused(icon) && (!(NebulaGameManager.Instance?.GetPlayer(icon.playerId)?.IsDead ?? true) || exiled.PlayerId == icon.playerId));
                if (notDoused.Count == 1 && notDoused[0].playerId == exiled.PlayerId)
                    acTokenChallenge = new("arsonist.challenge", false, (val, _) => val);
            }
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value = endState.CheckWin(MyPlayer.PlayerId) && endState.EndCondition == NebulaGameEnd.ArsonistWin;
        }
    }
}
