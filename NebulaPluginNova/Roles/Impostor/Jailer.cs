using Hazel.Udp;
using Nebula.Roles.Abilities;
using Nebula.Roles.Assignment;
using Nebula.Roles.Complex;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Jailer : DefinedSingleAbilityRoleTemplate<Jailer.Ability>, DefinedRole
{
    private Jailer() : base("jailer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [CanMoveWithMapWatchingOption, CanUseAdminOnMeetingOption, CanIdentifyDeadBodiesOption, CanIdentifyImpostorsOption, InheritAbilityOnDyingOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder?.ScheduleAddRelated(() => [JailerModifier.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Jailer.png");
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static public BoolConfiguration CanMoveWithMapWatchingOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canMoveWithMapWatching", false);
    static public BoolConfiguration CanUseAdminOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canUseAdminOnMeeting", true);
    static public BoolConfiguration CanIdentifyDeadBodiesOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canIdentifyDeadBodies", false);
    static public BoolConfiguration CanIdentifyImpostorsOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canIdentifyImpostors", false);
    static public BoolConfiguration InheritAbilityOnDyingOption = NebulaAPI.Configurations.Configuration("options.role.jailer.inheritAbilityOnDying", false);

    static public Jailer MyRole = new Jailer();

    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable != JailerModifier.MyRole;

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                JailerAbility.TryAddAndBind(() => !this.IsDeadObject && !this.IsUsurped);
                acTokenCommon2 ??= new("jailer.common2", 0f, (val, _) => val > 10f);
            }
        }

        AchievementToken<bool>? acTokenCommon = null;
        AchievementToken<float>? acTokenCommon2 = null;
        AchievementToken<int>? acTokenChallenge = null;

        [Local]
        void OnOpenSabotageMap(MapOpenSabotageEvent ev)
        {
            acTokenCommon ??= new("jailer.common1", false, (val, _) => val);
        }

        [OnlyMyPlayer, Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (acTokenCommon != null) acTokenCommon.Value = true;

            if (acTokenChallenge != null)
            {
                var pos = PlayerControl.LocalPlayer.GetTruePosition();
                Collider2D? room = null;
                foreach (var entry in ShipStatus.Instance.FastRooms)
                {
                    if (entry.value.roomArea.OverlapPoint(pos))
                    {
                        room = entry.value.roomArea;
                        break;
                    }
                }

                if (room != null && Helpers.AllDeadBodies().Any(d => d.ParentId != ev.Dead.PlayerId && room.OverlapPoint(d.TruePosition)))
                    acTokenChallenge!.Value++;
            }
        }

        [Local, OnlyMyPlayer]
        void Update(GameUpdateEvent ev)
        {
            if(acTokenCommon2 != null && !MeetingHud.Instance && !ExileController.Instance)
            {
                if(Helpers.AllDeadBodies().Any(d => d.transform.position.Distance(MyPlayer.VanillaPlayer.transform.position) < 5f))
                    acTokenCommon2.Value += Time.deltaTime;
                else if(acTokenCommon2.Value < 10f)
                    acTokenCommon2.Value = 0f;
            }
        }

        [OnlyMyPlayer]
        void InheritAbilityOnDead(PlayerDieEvent ev)
        {
            var localPlayer = Virial.NebulaAPI.CurrentGame?.LocalPlayer;

            if (localPlayer == null) return;

            //継承ジェイラーの対象で、JailerAbilityを獲得していなければ登録
            if (InheritAbilityOnDyingOption && !localPlayer.IsDead && localPlayer.IsImpostor)
            {
                JailerAbility.TryAddAndBind(() => !this.IsDeadObject && !this.IsUsurped);
            }

        }
    }
}

public class JailerModifier : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private JailerModifier() : base("jailer", "JLR", new(Palette.ImpostorRed), [Jailer.CanMoveWithMapWatchingOption, Jailer.CanUseAdminOnMeetingOption, Jailer.CanIdentifyDeadBodiesOption, Jailer.CanIdentifyImpostorsOption], allocateToNeutral: false, allocateToCrewmate: false)
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Jailer.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Jailer.png");
    }
    string DefinedAssignable.InternalName => "jailerModifier";
    string DefinedAssignable.GeneralBlurb => (Jailer.MyRole as DefinedAssignable).GeneralBlurb;

  
    int HasAssignmentRoutine.AssignPriority => 2;

    static public JailerModifier MyRole = new JailerModifier();

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        public Instance(GamePlayer myPlayer) : base(myPlayer)
        {
        }

        DefinedModifier RuntimeModifier.Modifier => MyRole;

        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort)
        {
            if (isShort) return null;
            return lastRoleName + " " + (this as RuntimeModifier).DisplayColoredName;
        }

        void RuntimeAssignable.OnActivated()
        {
            if(AmOwner) JailerAbility.TryAddAndBind(() => !this.IsDeadObject);
        }
    }
}