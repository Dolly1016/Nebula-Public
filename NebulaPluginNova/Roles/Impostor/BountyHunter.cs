using Nebula.Game.Statistics;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class BountyHunter : DefinedSingleAbilityRoleTemplate<BountyHunter.Ability>, HasCitation, DefinedRole
{
    private BountyHunter() : base("bountyHunter", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [BountyKillCoolDownOption, OthersKillCoolDownOption, ChangeBountyIntervalOption, ShowBountyArrowOption, ArrowUpdateIntervalOption]) { }
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));

    static private readonly IRelativeCoolDownConfiguration BountyKillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.bountyHunter.bountyKillCoolDown", CoolDownType.Ratio, (5f, 60f, 2.5f), 10f, (-30f, 30f, 2.5f), -10f, (0.125f, 2f, 0.125f), 0.5f);
    static private readonly IRelativeCoolDownConfiguration OthersKillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.bountyHunter.othersKillCoolDown", CoolDownType.Ratio, (5f, 60f, 2.5f), 40f, (-30f, 30f, 2.5f), 20f, (0.125f, 2f, 0.125f), 2f);
    static private readonly BoolConfiguration ShowBountyArrowOption = NebulaAPI.Configurations.Configuration("options.role.bountyHunter.showBountyArrow", true);
    static private readonly FloatConfiguration ArrowUpdateIntervalOption = NebulaAPI.Configurations.Configuration("options.role.bountyHunter.arrowUpdateInterval", (5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second, () => ShowBountyArrowOption);
    static private readonly FloatConfiguration ChangeBountyIntervalOption = NebulaAPI.Configurations.Configuration("options.role.bountyHunter.changeBountyInterval", (5f, 120f, 5f), 45f, FloatConfigurationDecorator.Second);

    static public readonly BountyHunter MyRole = new();
    bool DefinedRole.IsJackalizable => true;

    static private readonly GameStatsEntry StatsBountyKill = NebulaAPI.CreateStatsEntry("stats.bountyHunter.bountyKill", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsOthersKill = NebulaAPI.CreateStatsEntry("stats.bountyHunter.othersKill", GameStatsCategory.Roles, MyRole);
    float MaxKillCoolDown => Mathf.Max(BountyKillCoolDownOption.CoolDown, OthersKillCoolDownOption.CoolDown, AmongUsUtil.VanillaKillCoolDown);

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButtonImpl? killButton = null;
        bool IPlayerAbility.HideKillButton => true;

        private AchievementToken<bool>? acTokenKillBounty;
        private AchievementToken<bool>? acTokenKillNonBounty;
        private AchievementToken<(bool cleared,Queue<float> history)>? acTokenChallenge;

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                acTokenKillBounty = new("bountyHunter.common1", false, (val, _) => val);
                acTokenKillNonBounty = new("bountyHunter.another1", false, (val, _) => val);
                acTokenChallenge = new("bountyHunter.challenge", (false, new()), (val, _) => val.cleared);

                bountyTimer = new TimerImpl(ChangeBountyIntervalOption).Start().Register(this);
                arrowTimer = new TimerImpl(ArrowUpdateIntervalOption).Start().Register(this);

                var killTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.KillablePredicate(MyPlayer), null, Impostor.CanKillHidingPlayerOption).Register(this);

                killButton = new ModAbilityButtonImpl(false, true).KeyBind(Virial.Compat.VirtualKeyInput.Kill).Register(this);
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.Visibility = (button) => !MyPlayer.IsDead;
                killButton.OnClick = (button) => {
                    MyPlayer.MurderPlayer(killTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);

                    if (IsUsurped)
                    {
                        button.CoolDownTimer!.Start(MyPlayer.TeamKillCooldown);
                    }
                    else
                    {
                        if (killTracker.CurrentTarget!.PlayerId == currentBounty)
                        {
                            ChangeBounty(killTracker.CurrentTarget);
                            button.CoolDownTimer!.Start(BountyKillCoolDownOption.CoolDown);
                            acTokenKillBounty.Value = true;
                            StatsBountyKill.Progress();
                        }
                        else
                        {
                            button.CoolDownTimer!.Start(OthersKillCoolDownOption.CoolDown);
                            acTokenKillNonBounty.Value = true;
                            StatsOthersKill.Progress();
                        }
                    }

                    acTokenChallenge.Value.history.Enqueue(Time.time);
                    if (acTokenChallenge.Value.history.Count >= 3)
                    {
                        float first = acTokenChallenge.Value.history.DequeAt(3);
                        float last = Time.time;
                        if (last - first < 30f) acTokenChallenge.Value.cleared = true;
                    }

                };
                killButton.CoolDownTimer = new AdvancedTimer(AmongUsUtil.VanillaKillCoolDown, MyRole.MaxKillCoolDown).SetDefault(AmongUsUtil.VanillaKillCoolDown).SetAsKillCoolDown().Start(10f).Register(this);
                killButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                killButton.SetLabel("kill");

                var iconHolder = HudContent.InstantiateContent("BountyHolder", true);
                this.BindGameObject(iconHolder.gameObject);
                bountyIcon = AmongUsUtil.GetPlayerIcon(MyPlayer.Unbox().DefaultOutfit.Outfit.outfit, iconHolder.transform, Vector3.zero, Vector3.one * 0.5f);
                bountyIcon.ToggleName(true);
                bountyIcon.SetName("", Vector3.one * 4f, Color.white, -1f);

                if (ShowBountyArrowOption) bountyArrow = new Arrow().SetColor(Palette.ImpostorRed).Register(this);

                ChangeBounty();
            }
        }

        private byte currentBounty = 0;

        PoolablePlayer bountyIcon = null!;
        TimerImpl bountyTimer = null!;
        TimerImpl arrowTimer = null!;
        Arrow bountyArrow = null!;
        
        void ChangeBounty(GamePlayer? excluded = null)
        {
            var arr = PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => !p.AmOwner && p.PlayerId != excluded?.PlayerId && !p.Data.IsDead && MyPlayer.CanKill(p.GetModInfo()!)).ToArray();
            if (arr.Length == 0) currentBounty = byte.MaxValue;
            else currentBounty = arr[System.Random.Shared.Next(arr.Length)].PlayerId;

            if (currentBounty == byte.MaxValue)
                bountyIcon.gameObject.SetActive(false);
            else
            {
                bountyIcon.gameObject.SetActive(true);
                bountyIcon.UpdateFromPlayerOutfit(NebulaGameManager.Instance!.GetPlayer(currentBounty)!.Unbox().DefaultOutfit.Outfit.outfit, PlayerMaterial.MaskType.None, false, true);
            }

            if (ShowBountyArrowOption) UpdateArrow();

            bountyTimer.Start();
        }

        void UpdateArrow()
        {
            var target = NebulaGameManager.Instance?.GetPlayer(currentBounty);
            if (target==null)
            {
                bountyArrow.IsActive= false;
            }
            else
            {
                bountyArrow.IsActive= true;
                bountyArrow.TargetPos = target.VanillaPlayer.transform.localPosition;
            }

            arrowTimer.Start();
        }

        void UpdateTimer()
        {
            if (!bountyTimer.IsProgressing)
            {
                ChangeBounty();
            }
            bountyIcon.SetName(Mathf.CeilToInt(bountyTimer.CurrentTime).ToString());

            if (ShowBountyArrowOption && !arrowTimer.IsProgressing)
            {
                UpdateArrow();
            }
        }

        [Local]
        void LocalUpdate(GameUpdateEvent ev) => UpdateTimer();
        

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            //死亡しているプレイヤーであれば切り替える
            if (NebulaGameManager.Instance?.GetPlayer(currentBounty)?.IsDead ?? true) ChangeBounty();
        }
    }
}
