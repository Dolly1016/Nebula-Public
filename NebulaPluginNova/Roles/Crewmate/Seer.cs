using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using static MeetingHud;
using static UnityEngine.ParticleSystem.PlaybackState;

namespace Nebula.Roles.Crewmate;

public class Seer : ConfigurableStandardRole
{
    static public Seer MyRole = new Seer();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "seer";
    public override Color RoleColor => new Color(73f / 255f, 166f / 255f, 104f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration GhostDurationOption = null!;
    private NebulaConfiguration CanSeeGhostsInShadowOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        GhostDurationOption = new(RoleConfig, "ghostDuration", null, 15f, 300f, 15f, 90f, 90f) { Decorator = NebulaConfiguration.SecDecorator };
        CanSeeGhostsInShadowOption = new(RoleConfig, "canSeeGhostsInShadow", null, false, false);
    }

    [NebulaPreLoad]
    public class Ghost : INebulaScriptComponent
    {
        SpriteRenderer renderer;
        static XOnlyDividedSpriteLoader ghostSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Ghost.png", 160f, 9);
        private float time;
        private float indexTime;
        private int index;
        private AchievementToken<bool> commonToken;
        public Ghost(Vector2 pos, float time, AchievementToken<bool> commonToken) : base()
        {
            this.commonToken = commonToken;

            renderer = UnityHelper.CreateObject<SpriteRenderer>("Ghost", null, (Vector3)pos + new Vector3(0, 0, -1f));
            this.time = time;
            renderer.gameObject.layer = MyRole.CanSeeGhostsInShadowOption ? LayerExpansion.GetObjectsLayer() : LayerExpansion.GetDefaultLayer();
            renderer.sprite = ghostSprite.GetSprite(0);
        }

        public override void Update()
        {
            if(!commonToken.Value && !commonToken.Achievement.IsCleared)
            {
                if(!Helpers.AnyNonTriggersBetween(PlayerControl.LocalPlayer.GetTruePosition(), renderer.transform.localPosition, out var vec) && vec.magnitude < 1f)
                    commonToken.Value = true;
            }

            if (time > 0f && AmongUsUtil.InMeeting) time -= Time.deltaTime;
            indexTime -= Time.deltaTime;

            if (indexTime < 0f)
            {
                index = time > 0f ? (index + 1) % 3 : index + 1;
                indexTime = 0.2f;

                if (index < 9) renderer.sprite = ghostSprite.GetSprite(index);
                else Release();
            }
        }

        public override void OnReleased()
        {
            if(renderer)GameObject.Destroy(renderer.gameObject);
        }
    }

    public class Instance : Crewmate.Instance
    {
        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        private AchievementToken<bool>? acTokenCommon;
        private AchievementToken<(bool noMissFlag, int meetings)>? acTokenChallenge;

        public override void OnActivated()
        {
            acTokenCommon = new("seer.common1", false, (val,_)=>val);
            acTokenChallenge = new("seer.challenge", (true, 0), (val, _) => val.noMissFlag && val.meetings >= 3);
        }

        public override void OnAnyoneDeadLocal(PlayerControl dead)
        {
            if (MeetingHud.Instance || ExileController.Instance) return;

            new Ghost(dead.transform.position, MyRole.GhostDurationOption.GetFloat(), acTokenCommon!);
            AmongUsUtil.PlayFlash(MyRole.RoleColor);
        }

        public override void OnVotedLocal(PlayerControl? votedFor)
        {
            if (acTokenChallenge != null) {
                acTokenChallenge.Value.noMissFlag &= (votedFor?.GetModInfo()?.Role.Role.Category ?? RoleCategory.CrewmateRole) != RoleCategory.CrewmateRole;
            }
        }

        public override void OnMeetingEnd()
        {
            if (acTokenChallenge != null && !MyPlayer.IsDead) acTokenChallenge.Value.meetings++;
        }
    }
}


