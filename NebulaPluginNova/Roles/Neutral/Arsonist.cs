using Nebula.Behavior;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;

namespace Nebula.Roles.Neutral;

public class Arsonist : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.arsonist", new(229, 93, 0), TeamRevealType.OnlyMe);
    private Arsonist():base("arsonist", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [DouseCoolDownOption, DouseDurationOption, VentConfiguration])
    {
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Arsonist.png");

    }
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player,arguments);

    static private FloatConfiguration DouseCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.arsonist.douseCoolDown", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DouseDurationOption = NebulaAPI.Configurations.Configuration("options.role.arsonist.douseDuration", (1f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.arsonist.vent", true);

    static public Arsonist MyRole = new Arsonist();
    static private GameStatsEntry StatsDouse = NebulaAPI.CreateStatsEntry("stats.arsonist.doused", GameStatsCategory.Roles, MyRole);

    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private int initialDousedMask = 0;
        public Instance(GamePlayer player, int[] arguments) : base(player, VentConfiguration)
        {
            if (arguments.Length == 1) initialDousedMask = arguments[0];
        }

        int[]? RuntimeAssignable.RoleArguments
        {
            get
            {
                int mask = 0;
                foreach (var icon in dousedPlayers)
                {
                    mask |= 1 << icon.PlayerId;
                }
                return new int[] { mask };
            }
        }

        static private readonly RemoteProcess<(GamePlayer arsonist, GamePlayer target)> RpcUpdateStatus = new("UpdateArsonist", (message, _) => { 
            if(message.arsonist.Role is Instance arsonist)
            {
                arsonist.dousedPlayers.Add(message.target);
            }
        });

        GUIWidget RuntimeAssignable.ProgressWidget => ProgressGUI.Holder(
            ProgressGUI.OneLineText(Language.Translate("role.arsonist.gui.left")),
            ProgressGUI.Holder(GamePlayer.AllOrderedPlayers.Where(p => p != MyPlayer && !dousedPlayers.Contains(p)).Select(p => 
                ProgressGUI.OneLineText("-" + p.ColoredName + (p.IsDead ? ("(" + Language.Translate("role.arsonist.gui.left.dead") + ")").Color(Color.gray) : ""))
            )).Move(new(0.04f, 0f))
            );

        static private Image douseButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DouseButton.png", 115f);
        static private Image IgniteButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.IgniteButton.png", 115f);
        private List<(byte playerId,PoolablePlayer icon)> playerIcons = [];
        private HashSet<GamePlayer> dousedPlayers = [];
        private bool canIgnite;

        private bool CheckDoused((byte playerId, PoolablePlayer icon) p) => p.icon.GetAlpha() > 0.8f;
        private bool CheckIgnitable()
        {
            int doused = playerIcons.Count(CheckDoused);
            int num = playerIcons.Count;
            ModSingleton<IWinningOpportunity>.Instance.RpcSetOpportunity(MyTeam, 1f - (num - doused) * 0.1f);
            
            canIgnite = doused == num;
            return canIgnite;
        }

        private void UpdateIcons()
        {
            for(int i=0;i<playerIcons.Count;i++)
            {
                playerIcons[i].icon.transform.localPosition = new(i * 0.29f - 0.3f, -0.1f, -i * 0.01f);
            }
        }

        public override void OnActivated()
        {
            //共有している進捗を進める
            {
                var mask = BitMasks.AsPlayer((uint)initialDousedMask);
                foreach(var p in GamePlayer.AllPlayers)
                {
                    if (mask.Test(p) || p.IsDead) dousedPlayers.Add(p);
                }
            }

            if (AmOwner)
            {
                var IconsHolder = HudContent.InstantiateContent("ArsonistIcons", true, true, false, true);
                this.BindGameObject(IconsHolder.gameObject);
                

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
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
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

                var douseTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeStandardPredicate(p) && playerIcons.Any(tuple => tuple.playerId == p.RealPlayer.PlayerId && tuple.icon.GetAlpha() < 0.8f));
                var douseButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    DouseCoolDownOption, DouseDurationOption, "douse", douseButtonSprite,
                    _ => douseTracker.CurrentTarget != null, _ => !canIgnite);
                douseButton.OnEffectStart = _ => douseTracker.KeepAsLongAsPossible = true;
                douseButton.OnEffectEnd = (button) =>
                {
                    douseTracker.KeepAsLongAsPossible = false;
                    if (douseTracker.CurrentTarget == null) return;
                    if (MeetingHud.Instance) return;

                    if (!button.EffectTimer!.IsProgressing)
                    {
                        if (!(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, douseTracker.CurrentTarget, new(RealPlayerOnly: true))).IsCanceled ?? false))
                        {
                            foreach (var icon in playerIcons) if (icon.playerId == douseTracker.CurrentTarget.RealPlayer.PlayerId) icon.icon.SetAlpha(1f);
                            RpcUpdateStatus.Invoke((MyPlayer, douseTracker.CurrentTarget.RealPlayer));
                            StatsDouse.Progress();
                            CheckIgnitable();
                        }
                    }
                    douseButton.StartCoolDown();
                };
                douseButton.OnUpdate = (button) => {
                    if (!button.IsInEffect) return;
                    if (douseTracker.CurrentTarget == null) button.InterruptEffect();
                };

                bool won = false;
                var igniteButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    0f, "ignite", IgniteButtonSprite,
                    null, _ => canIgnite && !won);
                igniteButton.OnClick = (button) => {
                    NebulaGameManager.Instance.RpcInvokeSpecialWin(NebulaGameEnd.ArsonistWin, 1 << MyPlayer.PlayerId);
                    won = true;
                };
            }

        }

        [Local]
        void LocalUpdate(GameUpdateEvent ev) => UpdateIcons();
        
        
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (AmOwner)
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
            GamePlayer.AllPlayers.Where(p => p.IsDead).Do(p => dousedPlayers.Add(p));
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
