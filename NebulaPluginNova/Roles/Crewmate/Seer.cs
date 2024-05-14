using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

[NebulaPreLoad]
file class Ghost : INebulaScriptComponent, IGameOperator
{
    SpriteRenderer renderer;
    static XOnlyDividedSpriteLoader ghostSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Ghost.png", 160f, 9);
    private float time;
    private float indexTime;
    private int index;
    private AchievementToken<bool>? commonToken;
    public Ghost(Vector2 pos, float time, AchievementToken<bool>? commonToken, bool canSeeGhostsInShadow) : base()
    {
        this.commonToken = commonToken;

        renderer = UnityHelper.CreateObject<SpriteRenderer>("Ghost", null, (Vector3)pos + new Vector3(0, 0, -1f));
        this.time = time;
        renderer.gameObject.layer = canSeeGhostsInShadow ? LayerExpansion.GetObjectsLayer() : LayerExpansion.GetDefaultLayer();
        renderer.sprite = ghostSprite.GetSprite(0);
    }

    void Update(GameUpdateEvent ev)
    {
        if (commonToken != null && !commonToken.Value && !commonToken.Achievement.IsCleared)
        {
            if (!Helpers.AnyNonTriggersBetween(PlayerControl.LocalPlayer.GetTruePosition(), renderer.transform.localPosition, out var vec) && vec.magnitude < 1f)
                commonToken.Value = true;
        }

        if (time > 0f && AmongUsUtil.InMeeting) time -= Time.deltaTime;
        indexTime -= Time.deltaTime;

        if (indexTime < 0f)
        {
            index = time > 0f ? (index + 1) % 3 : index + 1;
            indexTime = 0.2f;

            if (index < 9) renderer.sprite = ghostSprite.GetSprite(index);
            else this.ReleaseIt();
        }
    }

    void IGameOperator.OnReleased()
    {
        if (renderer) GameObject.Destroy(renderer.gameObject);
    }
}

public class GhostAndFlashAbility : IGameOperator
{
    public bool CanSeeGhostInShadow { get; set; } = true;
    public Color FlashColor { get; set; } = Color.white;
    public AchievementToken<bool>? CommonToken;
    public float GhostDuration { get; set; } = 60f;

    void OnPlayerMurdered(PlayerMurderedEvent ev)
    {
        if (MeetingHud.Instance || ExileController.Instance) return;

        
        if (!ev.Dead.HasAttribute(PlayerAttributes.BuskerEffect))
        {
            new Ghost(ev.Dead.VanillaPlayer.transform.position, GhostDuration, CommonToken, CanSeeGhostInShadow);
            AmongUsUtil.PlayFlash(FlashColor);
        }
    }
}

public class Seer : ConfigurableStandardRole, HasCitation, DefinedRole
{
    static public Seer MyRole = new Seer();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    string DefinedAssignable.LocalizedName => "seer";
    public override Color RoleColor => new Color(73f / 255f, 166f / 255f, 104f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Crewmate.Team;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration GhostDurationOption = null!;
    private NebulaConfiguration CanSeeGhostsInShadowOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        GhostDurationOption = new(RoleConfig, "ghostDuration", null, 15f, 300f, 15f, 90f, 90f) { Decorator = NebulaConfiguration.SecDecorator };
        CanSeeGhostsInShadowOption = new(RoleConfig, "canSeeGhostsInShadow", null, false, false);
    }

    public class Instance : Crewmate.Instance, RuntimeRole
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        private AchievementToken<(bool noMissFlag, int meetings)>? acTokenChallenge;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                AchievementToken<bool> acTokenCommon = new("seer.common1", false, (val, _) => val);
                acTokenChallenge = Bind(new AchievementToken<(bool noMissFlag, int meetings)>("seer.challenge2", (true, 0), (val, _) => val.noMissFlag && val.meetings >= 3));

                new GhostAndFlashAbility() { CanSeeGhostInShadow = MyRole.CanSeeGhostsInShadowOption, FlashColor = MyRole.RoleColor, GhostDuration = MyRole.GhostDurationOption.GetFloat(), CommonToken = acTokenCommon }.Register(this);
            }
        }

        [OnlyMyPlayer]
        void OnVotedLocal(PlayerVoteDisclosedLocalEvent ev)
        {
            if (acTokenChallenge != null)
                acTokenChallenge.Value.noMissFlag &= (ev.VoteFor?.Role.Role.Category ?? RoleCategory.CrewmateRole) != RoleCategory.CrewmateRole;
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenChallenge != null && !MyPlayer.IsDead) acTokenChallenge.Value.meetings++;
        }
    }
}


