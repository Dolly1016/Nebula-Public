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

public class Jailer : DefinedRoleTemplate, DefinedRole
{
    private Jailer() : base("jailer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [CanMoveWithMapWatchingOption, CanUseAdminOnMeetingOption, CanIdentifyDeadBodiesOption, CanIdentifyImpostorsOption, InheritAbilityOnDyingOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder?.ScheduleAddRelated(() => [JailerModifier.MyRole.ConfigurationHolder!]);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public BoolConfiguration CanMoveWithMapWatchingOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canMoveWithMapWatching", false);
    static public BoolConfiguration CanUseAdminOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canUseAdminOnMeeting", true);
    static public BoolConfiguration CanIdentifyDeadBodiesOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canIdentifyDeadBodies", false);
    static public BoolConfiguration CanIdentifyImpostorsOption = NebulaAPI.Configurations.Configuration("options.role.jailer.canIdentifyImpostors", false);
    static public BoolConfiguration InheritAbilityOnDyingOption = NebulaAPI.Configurations.Configuration("options.role.jailer.inheritAbilityOnDying", false);

    static public Jailer MyRole = new Jailer();

    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable != JailerModifier.MyRole;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
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

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                //JailerAbilityを獲得していなければ登録
                if ((GameOperatorManager.Instance?.AllOperators.All(e => e is not JailerAbility) ?? false))
                {
                    new JailerAbility(CanIdentifyImpostorsOption, CanIdentifyDeadBodiesOption, CanMoveWithMapWatchingOption, CanUseAdminOnMeetingOption).Register(this);
                }

                acTokenCommon2 ??= new("jailer.common2", 0f, (val, _) => val > 10f);
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
            if (InheritAbilityOnDyingOption && !localPlayer.IsDead && localPlayer.IsImpostor && (GameOperatorManager.Instance?.AllOperators.All(e => e is not JailerAbility) ?? false))
            {
                new JailerAbility(CanIdentifyImpostorsOption, CanIdentifyDeadBodiesOption, CanMoveWithMapWatchingOption, CanUseAdminOnMeetingOption).Register(this);
            }

        }
    }
}

public class JailerModifier : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private JailerModifier() : base("jailer", "JLR", new(Palette.ImpostorRed), [Jailer.CanMoveWithMapWatchingOption, Jailer.CanUseAdminOnMeetingOption, Jailer.CanIdentifyDeadBodiesOption, Jailer.CanIdentifyImpostorsOption], allocateToNeutral: false, allocateToCrewmate: false)
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Jailer.MyRole.ConfigurationHolder!]);
    }
    string DefinedAssignable.InternalName => "jailerModifier";
    string DefinedAssignable.GeneralColoredBlurb => (Jailer.MyRole as DefinedAssignable).GeneralColoredBlurb;

    //割り当てる役職が変更されてしまうので、一番最後に割り当てる
    int HasAssignmentRoutine.AssignPriority => 2;

    static public JailerModifier MyRole = new JailerModifier();


    // このモディファイアは実際に割り当てられることはない
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
            if(AmOwner && (GameOperatorManager.Instance?.AllOperators.All(e => e is not JailerAbility) ?? false))
            {
                new JailerAbility(Jailer.CanIdentifyImpostorsOption, Jailer.CanIdentifyDeadBodiesOption, Jailer.CanMoveWithMapWatchingOption, Jailer.CanUseAdminOnMeetingOption).Register(this);
            }
        }
    }
}