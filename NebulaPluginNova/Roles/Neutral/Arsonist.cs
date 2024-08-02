using Nebula.Behaviour;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Arsonist : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public RoleTeam MyTeam = new Team("teams.arsonist", new(229, 93, 0), TeamRevealType.OnlyMe);
    private Arsonist():base("arsonist", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [DouseCoolDownOption, DouseDurationOption, VentConfiguration]) { }
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player,arguments);

    static private FloatConfiguration DouseCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.arsonist.douseCoolDown", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DouseDurationOption = NebulaAPI.Configurations.Configuration("options.role.arsonist.douseDuration", (1f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.arsonist.vent", true);

    static public Arsonist MyRole = new Arsonist();
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private GameTimer ventCoolDown = (new Timer(VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentConfiguration.Duration);
        private bool canUseVent = VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;
        private int initialDousedMask = 0;
        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length == 1) initialDousedMask = arguments[0];
        }

        int[]? RuntimeAssignable.RoleArguments
        {
            get
            {
                int mask = 0;
                foreach (var icon in playerIcons.Where(icon => icon.icon.GetAlpha() > 0.8f))
                {
                    mask |= 1 << icon.playerId;
                }
                return new int[] { mask };
            }
        }


        private Modules.ScriptComponents.ModAbilityButton? douseButton = null;
        private Modules.ScriptComponents.ModAbilityButton? igniteButton = null;
        static private Image douseButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DouseButton.png", 115f);
        static private Image IgniteButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.IgniteButton.png", 115f);
        private List<(byte playerId,PoolablePlayer icon)> playerIcons = new();
        private bool canIgnite;

        private bool CheckDoused((byte playerId, PoolablePlayer icon) p) => p.icon.GetAlpha() > 0.8f;
        private bool CheckIgnitable()
        {
            canIgnite = playerIcons.All(CheckDoused);
            return canIgnite;
        }

        private void UpdateIcons()
        {
            for(int i=0;i<playerIcons.Count;i++)
            {
                playerIcons[i].icon.transform.localPosition = new(i * 0.29f - 0.3f, -0.1f, -i * 0.01f);
            }
        }

        void RuntimeAssignable.OnActivated()
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

                var douseTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.StandardPredicate(p) && playerIcons.Any(tuple => tuple.playerId == p.PlayerId && tuple.icon.GetAlpha() < 0.8f)));

                douseButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                douseButton.SetSprite(douseButtonSprite.GetSprite());
                douseButton.Availability = (button) => MyPlayer.CanMove && douseTracker.CurrentTarget != null;
                douseButton.Visibility = (button) => !MyPlayer.IsDead && !canIgnite;
                douseButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                douseButton.OnEffectEnd = (button) =>
                {
                    if (douseTracker.CurrentTarget == null) return;

                    if (!button.EffectTimer!.IsProgressing)
                        foreach (var icon in playerIcons) if (icon.playerId == douseTracker.CurrentTarget.PlayerId) icon.icon.SetAlpha(1f);

                    CheckIgnitable();
                    douseButton.StartCoolDown();
                };
                douseButton.OnUpdate = (button) => {
                    if (!button.EffectActive) return;
                    if (douseTracker.CurrentTarget == null) button.InactivateEffect();
                };
                douseButton.CoolDownTimer = Bind(new Timer(0f, DouseCoolDownOption).SetAsAbilityCoolDown().Start());
                douseButton.EffectTimer = Bind(new Timer(0f, DouseDurationOption));
                douseButton.SetLabel("douse");

                bool won = false;
                igniteButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
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

        [Local]
        void LocalUpdate(GameUpdateEvent ev) => UpdateIcons();
        
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
        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            var notDoused = playerIcons.FindAll(icon => !CheckDoused(icon) && (!(NebulaGameManager.Instance?.GetPlayer(icon.playerId)?.IsDead ?? true) || ev.Player.PlayerId == icon.playerId));
            if (notDoused.Count == 1 && notDoused[0].playerId == ev.Player.PlayerId)
                acTokenChallenge = new("arsonist.challenge", false, (val, _) => val);
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value = ev.EndState.Winners.Test(MyPlayer) && ev.EndState.EndCondition == NebulaGameEnd.ArsonistWin;
        }
    }
}
