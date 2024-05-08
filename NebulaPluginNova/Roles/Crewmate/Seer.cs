using Virial;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

[NebulaPreLoad]
file class Ghost : INebulaScriptComponent, IGameEntity
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

    void IGameEntity.Update()
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

    void IGameEntity.OnReleased()
    {
        if (renderer) GameObject.Destroy(renderer.gameObject);
    }
}

public class GhostAndFlashAbility : IGameEntity
{
    public bool CanSeeGhostInShadow { get; set; } = true;
    public Color FlashColor { get; set; } = Color.white;
    public AchievementToken<bool>? CommonToken;
    public float GhostDuration { get; set; } = 60f;

    void IGameEntity.OnPlayerMurdered(Virial.Game.Player dead, Virial.Game.Player murder)
    {
        if (MeetingHud.Instance || ExileController.Instance) return;

        var player = dead.Unbox();

        if (!player.HasAttribute(PlayerAttributes.BuskerEffect))
        {
            new Ghost(player.MyControl.transform.position, GhostDuration, CommonToken, CanSeeGhostInShadow);
            AmongUsUtil.PlayFlash(FlashColor);
        }
    }
}

public class Seer : ConfigurableStandardRole, HasCitation
{
    static public Seer MyRole = new Seer();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "seer";
    public override Color RoleColor => new Color(73f / 255f, 166f / 255f, 104f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration GhostDurationOption = null!;
    private NebulaConfiguration CanSeeGhostsInShadowOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        GhostDurationOption = new(RoleConfig, "ghostDuration", null, 15f, 300f, 15f, 90f, 90f) { Decorator = NebulaConfiguration.SecDecorator };
        CanSeeGhostsInShadowOption = new(RoleConfig, "canSeeGhostsInShadow", null, false, false);
    }

    public class Instance : Crewmate.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        private AchievementToken<(bool noMissFlag, int meetings)>? acTokenChallenge;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                AchievementToken<bool> acTokenCommon = new("seer.common1", false, (val, _) => val);
                acTokenChallenge = Bind(new AchievementToken<(bool noMissFlag, int meetings)>("seer.challenge2", (true, 0), (val, _) => val.noMissFlag && val.meetings >= 3));

                new GhostAndFlashAbility() { CanSeeGhostInShadow = MyRole.CanSeeGhostsInShadowOption, FlashColor = MyRole.RoleColor, GhostDuration = MyRole.GhostDurationOption.GetFloat(), CommonToken = acTokenCommon }.Register(this);
            }
        }

        void IGameEntity.OnVotedLocal(PlayerControl? votedFor,bool isExiled)
        {
            if (AmOwner)
            {
                if (acTokenChallenge != null)
                {
                    acTokenChallenge.Value.noMissFlag &= (votedFor?.GetModInfo()?.Role.Role.Category ?? RoleCategory.CrewmateRole) != RoleCategory.CrewmateRole;
                }
            }
        }

        void IGameEntity.OnMeetingEnd(GamePlayer[] exiled)
        {
            if (acTokenChallenge != null && !MyPlayer.IsDead) acTokenChallenge.Value.meetings++;
        }
    }
}


