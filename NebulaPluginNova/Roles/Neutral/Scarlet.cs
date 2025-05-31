using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial;
using Nebula.Game.Statistics;
using Nebula.VoiceChat;
using Virial.Events.Game;
using Virial.Game;
using Nebula.Roles.Modifier;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Neutral;

internal class Scarlet : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.scarlet", new(138, 26, 49), TeamRevealType.OnlyMe);

    private Scarlet() : base("scarlet", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [GraceUntilDecidingFavoriteOption, NumOfKept, MaxUsesOfCommand, VentConfiguration])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Scarlet.png");
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, player.PlayerId), arguments.Get(1, NumOfKept), arguments.Get(2, 1), arguments.Get(3, MaxUsesOfCommand), arguments.Get(4, (int)(float)GraceUntilDecidingFavoriteOption));

    static private FloatConfiguration GraceUntilDecidingFavoriteOption = NebulaAPI.Configurations.Configuration("options.role.scarlet.graceUntilDecidingFavorite", (30f, 600f, 10f), 120f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration NumOfKept = NebulaAPI.Configurations.Configuration("options.role.scarlet.numOfKept", (1,10), 3);
    static private IntegerConfiguration MaxUsesOfCommand = NebulaAPI.Configurations.Configuration("options.role.scarlet.numOfCommand", (1, 10), 2);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.scarlet.vent", true);

    static public Scarlet MyRole = new Scarlet();
    static private GameStatsEntry StatsKept = NebulaAPI.CreateStatsEntry("stats.scarlet.kept", GameStatsCategory.Roles, MyRole);

    //1回の会議で投票先指定を変えられるScarletは一人だけ
    private int MeetingFixedScarlet = -1;

    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        public int FlirtatiousId { get; private set; }
        private int LeftFlirts = 3;
        private int LeftFavorite = 1;
        private int LeftMeeting = 3;
        private int GraceOnAssignment;
        TimerImpl? SuicideTimer = null;

        public Instance(GamePlayer player, int flirtatiousId, int leftFlirts, int leftFavorite, int leftMeeting, int grace) : base(player, VentConfiguration)
        {
            this.FlirtatiousId = flirtatiousId;
            this.LeftFlirts = leftFlirts;
            this.LeftFavorite = leftFavorite;
            this.LeftMeeting = leftMeeting;
            this.GraceOnAssignment = grace;
        }

        int[]? RuntimeAssignable.RoleArguments => [FlirtatiousId, LeftFlirts, LeftFavorite, LeftMeeting, (int)(SuicideTimer?.CurrentTime ?? GraceUntilDecidingFavoriteOption) + 1];

        static private Image flirtButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.FlirtatiousSubButton.png", 115f);
        static private Image favoriteButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.FlirtatiousMainButton.png", 115f);
        static private Image meetingButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.FlirtatiousMeetingButton.png", 115f);
        static private Image hourglassButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.HourglassButton.png", 115f);

        bool IsMyLover(GamePlayer player) => player.Unbox().GetModifiers<ScarletLover.Instance>().Any(f => f.FlirtatiousId == FlirtatiousId);
        bool IsMyFavorite(GamePlayer player) => player.Unbox().GetModifiers<ScarletLover.Instance>().Any(f => f.FlirtatiousId == FlirtatiousId && f.AmFavorite);
        bool IsMyFlirt(GamePlayer player) => player.Unbox().GetModifiers<ScarletLover.Instance>().Any(f => f.FlirtatiousId == FlirtatiousId && !f.AmFavorite);
        GamePlayer? GetMyFavorite() => NebulaGameManager.Instance?.AllPlayerInfo.FirstOrDefault(IsMyFavorite);

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var hourglass = new Modules.ScriptComponents.ModAbilityButtonImpl().Register(this);
                hourglass.SetSprite(hourglassButtonSprite.GetSprite());
                hourglass.Availability = (button) => true;
                hourglass.Visibility = (button) => !MyPlayer.IsDead && LeftFavorite > 0;
                hourglass.SetLabel("scarlet.grance");
                SuicideTimer = new TimerImpl(GraceOnAssignment).Register(this);
                float afterMeeting = 5f;
                SuicideTimer.SetPredicate(() => {
                    if (MeetingHud.Instance || ExileController.Instance) return false;
                    if(afterMeeting > 0f)
                    {
                        afterMeeting -= Time.deltaTime;
                        return false;
                    }
                    return true;
                });
                hourglass.OnMeeting = _ => afterMeeting = 5f;
                hourglass.OnEffectEnd = _ =>
                {
                    if (LeftFavorite > 0 && !MyPlayer.IsDead) MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                };
                hourglass.EffectTimer = SuicideTimer;
                hourglass.ActivateEffect();
                hourglass.SetInfoIcon("scarlet.suicide");

                int numOfFavorite = 0, numOfFlirt = 0;
                void CheckAndClearAch1(bool isFavorite)
                {
                    if(isFavorite)numOfFavorite++;
                    else numOfFlirt++;
                    if (numOfFavorite > 0 && numOfFlirt > 0) new StaticAchievementToken("scarlet.common1");
                }

                var playerTracker = ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p) && !IsMyLover(p)).Register(this);

                Modules.ScriptComponents.ModAbilityButtonImpl flirtButton = null!, favoriteButton = null!;
                flirtButton = new Modules.ScriptComponents.ModAbilityButtonImpl().KeyBind(Virial.Compat.VirtualKeyInput.Ability).Register(this);
                flirtButton.SetSprite(flirtButtonSprite.GetSprite());
                flirtButton.Availability = (button) => playerTracker.CurrentTarget != null && MyPlayer.CanMove;
                flirtButton.Visibility = (button) => !MyPlayer.IsDead && LeftFlirts > 0;
                var flirtIcon = flirtButton.ShowUsesIcon(4);
                flirtIcon.text = LeftFlirts.ToString();
                flirtButton.OnClick = (button) =>
                {
                    playerTracker.CurrentTarget?.Unbox().RpcInvokerSetModifier(ScarletLover.MyRole, [FlirtatiousId, 0]).InvokeSingle();
                    LeftFlirts--;
                    flirtIcon.text = LeftFlirts.ToString();
                    flirtButton.StartCoolDown();
                    favoriteButton.StartCoolDown();
                    StatsKept.Progress();
                    CheckAndClearAch1(false);
                };
                flirtButton.CoolDownTimer = new TimerImpl(2f).SetAsAbilityCoolDown().Start().Register(this);
                flirtButton.SetLabel("seduce");

                favoriteButton = new Modules.ScriptComponents.ModAbilityButtonImpl().KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility).Register(this);
                favoriteButton.SetSprite(favoriteButtonSprite.GetSprite());
                favoriteButton.Availability = (button) => playerTracker.CurrentTarget != null && MyPlayer.CanMove;
                favoriteButton.Visibility = (button) => !MyPlayer.IsDead && LeftFavorite > 0;
                var favoriteIcon = favoriteButton.ShowUsesIcon(4);
                favoriteIcon.text = LeftFavorite.ToString();
                favoriteButton.OnClick = (button) =>
                {
                    playerTracker.CurrentTarget?.Unbox().RpcInvokerSetModifier(ScarletLover.MyRole, [FlirtatiousId, 1]).InvokeSingle();
                    LeftFavorite--;
                    favoriteIcon.text = LeftFavorite.ToString();
                    flirtButton.StartCoolDown();
                    favoriteButton.StartCoolDown();

                    CheckAndClearAch1(true);
                };
                favoriteButton.CoolDownTimer = new TimerImpl(2f).SetAsAbilityCoolDown().Start().Register(this);
                favoriteButton.SetLabel("favorite");

                bool usedInTheMeeting = true;
                var meetingButton = new Modules.ScriptComponents.ModAbilityButtonImpl(alwaysShow: true).Register(this);
                meetingButton.SetSprite(meetingButtonSprite.GetSprite());
                meetingButton.Availability = (button) => MeetingHud.Instance && MeetingHud.Instance.CurrentState == MeetingHud.VoteStates.NotVoted;
                meetingButton.Visibility = (button) => !MyPlayer.IsDead && LeftMeeting > 0 && MeetingHud.Instance && (MeetingHud.Instance.CurrentState == MeetingHud.VoteStates.NotVoted || MeetingHud.Instance.CurrentState == MeetingHud.VoteStates.Discussion) && !usedInTheMeeting;
                var meetingIcon = meetingButton.ShowUsesIcon(4);
                meetingButton.OnClick = (button) =>
                {
                    RpcCommand.Invoke(MyPlayer);
                    usedInTheMeeting = true;
                };
                meetingButton.OnMeeting = _ =>
                {
                    meetingIcon.text = LeftMeeting.ToString();
                    usedInTheMeeting = false;
                };
                meetingButton.SetLabel("scarlet.command");
            }
        }

        [OnlyMyPlayer]
        void OnPreExiled(PlayerVoteDisclosedLocalEvent ev)
        {
            if (ev.VoteToWillBeExiled && MyRole.MeetingFixedScarlet == MyPlayer.PlayerId)
            {
                //号令による追放
                new StaticAchievementToken("scarlet.common2");
                NebulaAchievementManager.RpcClearAchievement.Invoke(("scarlet.another2", ev.VoteFor!));

                GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
                {
                    if (ev.EndState.EndCondition == NebulaGameEnd.ScarletWin && ev.EndState.Winners.Test(MyPlayer) && ev.EndState.Winners.Test(GetMyFavorite()) &&
                     NebulaGameManager.Instance!.AllPlayerInfo.Count(p => p.IsDead && IsMyFlirt(p)) >= 2 &&
                     NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead && IsMyFlirt(p)) >= 2)
                        new StaticAchievementToken("scarlet.challenge");
                }, this);
            }
        }


        [OnlyMyPlayer, OnlyHost]
        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            var myLover = GetMyFavorite();
            if (!(myLover?.IsDead ?? true))
            {
                if (ev is PlayerMurderedEvent or PlayerDisconnectEvent)
                {
                    myLover.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                }
                else if(ev is PlayerExiledEvent pee)
                {
                    myLover.VanillaPlayer.ModMarkAsExtraVictim(myLover.VanillaPlayer, PlayerState.Suicide, PlayerState.Suicide);
                }
                else {
                    myLover.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole);
                }
            }
        }

        void OnCheckGameEnd(EndCriteriaMetEvent ev)
        {
            var favorite = GetMyFavorite();
            if (favorite == null) return;
            if (!MyPlayer.IsDead && !favorite.IsDead && ev.Winners.Test(favorite)) ev.TryOverwriteEnd(NebulaGameEnd.ScarletWin, GameEndReason.Special);
        }

        void OnGameEnd(GameEndEvent ev)
        {
            if (
                AmOwner &&
                (GetMyFavorite()?.AmOwner ?? false) &&
                ev.EndState.EndCondition == NebulaGameEnd.ScarletWin &&
                ev.EndState.Winners.Test(MyPlayer) &&
                NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.AmOwner).All(p => !ev.EndState.Winners.Test(p))
                )
                new StaticAchievementToken("scarlet.love");
                
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
        {
            var favorite = GetMyFavorite();
            if (favorite == null) return;
            ev.SetWinIf(ev.GameEnd == NebulaGameEnd.ScarletWin && ev.LastWinners.Test(favorite) && !favorite.IsDead);
        }

        void FixVote(PlayerFixVoteHostEvent ev)
        {
            if (MyPlayer.IsDead) return;
            if (MyPlayer.PlayerId == MyRole.MeetingFixedScarlet)
            {
                var scarletArea = MeetingHud.Instance.playerStates.FirstOrDefault(p => p.TargetPlayerId == MyPlayer.PlayerId);

                if (MyPlayer.PlayerId == ev.Player.PlayerId)
                {
                    if (scarletArea?.DidVote ?? false)
                        RpcUseCommand.Invoke(MyPlayer);
                }
                else if (IsMyLover(ev.Player))
                {
                    if (scarletArea?.DidVote ?? false)
                    {
                        if (!ev.DidVote) ev.Vote = 1;
                        ev.VoteTo = NebulaGameManager.Instance?.GetPlayer(scarletArea.VotedFor);
                    }
                }

            }
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            //ミーティング指定を初期化
            MyRole.MeetingFixedScarlet = -1;
        }

        [OnlyMyPlayer]
        void EditGuessable(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (IsMyLover(ev.Guesser)) ev.CanGuess = false;
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            Color loverColor = Lover.Colors[0];

            if (IsMyLover(GamePlayer.LocalPlayer)) name += " ♡".Color(loverColor);
        }

        static private RemoteProcess<GamePlayer> RpcCommand = new("ScarletCommand", (p, _) =>
        {
            MyRole.MeetingFixedScarlet = p.PlayerId;
        });

        static private RemoteProcess<GamePlayer> RpcUseCommand = new("ConsumeScarletCommand", (p, _) =>
        {
            MyRole.MeetingFixedScarlet = p.PlayerId;
            (p.Role as Instance).DoIf(scarlet => scarlet.LeftMeeting--);
        });
    }
}


public class ScarletLover : DefinedModifierTemplate, DefinedModifier
{
    private ScarletLover() : base("scarletLover", Scarlet.MyTeam.Color, [], true, ()=>false)
    {
    }

    static public ScarletLover MyRole = new ScarletLover();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0), arguments.Get(1, 0) == 1);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        int flirtatiousId;
        public int FlirtatiousId => flirtatiousId;
        bool amFavorite;
        public bool AmFavorite => amFavorite;
        Scarlet.Instance? MyScarlet => NebulaGameManager.Instance?.AllPlayerInfo.FirstOrDefault(p => p.Role is Scarlet.Instance f && f.FlirtatiousId == flirtatiousId)?.Role as Scarlet.Instance;
        public Instance(GamePlayer player, int flirtatiousId, bool amFavorite) : base(player)
        {
            this.flirtatiousId = flirtatiousId;
            this.amFavorite = amFavorite;
        }

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            Color loverColor = Lover.Colors[0];
            
            var myFlirtatious = MyScarlet;
            RuntimeRole? myFlirtatiousRuntimeRole = myFlirtatious;

            bool canSee = false;
            bool canSeeAll = false;

            if (AmOwner) canSee = true;
            if ((myFlirtatiousRuntimeRole?.AmOwner ?? false) || canSeeAllInfo) canSeeAll = true;
            
            if (canSee || canSeeAll) name += ((canSeeAll && amFavorite) ? " ♥" : " ♡").Color(loverColor);
        }

        [OnlyMyPlayer, Local]
        void OnDead(PlayerDieEvent ev)
        {
            if (!amFavorite)
            {
                new StaticAchievementToken("scarlet.another1");
                return;
            }
        }

        [OnlyMyPlayer, OnlyHost]
        void OnDeadHost(PlayerDieOrDisconnectEvent ev) { 
            var myScarlet = MyScarlet as RuntimeRole;
            if (!(myScarlet?.MyPlayer.IsDead ?? true) && AmFavorite)
            {
                if (ev is PlayerMurderedEvent or PlayerDisconnectEvent)
                {
                    myScarlet.MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                }
                else if (ev is PlayerExiledEvent pee)
                {
                    myScarlet.MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(myScarlet.MyPlayer.VanillaPlayer, PlayerState.Suicide, PlayerState.Suicide);
                }
                else
                {
                    myScarlet.MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole);
                }
            }
        }

        [OnlyMyPlayer]
        void ShowMyRoleForScarlet(PlayerCheckRoleInfoVisibilityLocalEvent ev) => ev.CanSeeRole |= !AmFavorite && ((MyScarlet as RuntimeAssignable)?.AmOwner ?? false);

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= AmFavorite && (MyScarlet as RuntimeAssignable)!.MyPlayer.IsDead;

        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (!AmFavorite) return;

            if (ev.Phase != ExtraWinCheckPhase.ScarletPhase) return;
            if (ev.GameEnd != NebulaGameEnd.ScarletWin) return;

            var scarlet = MyScarlet;
            if (MyScarlet == null) return;
            var scarletPlayer = (scarlet as RuntimeAssignable)!.MyPlayer;
            if (!scarletPlayer.IsDead && ev.WinnersMask.Test(scarletPlayer))
            {
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraLoversWin);
                ev.IsExtraWin = true;
            }            
        }
    }
}
