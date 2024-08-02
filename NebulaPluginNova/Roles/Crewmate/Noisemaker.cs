using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial;

namespace Nebula.Roles.Crewmate;

public class Noisemaker : DefinedRoleTemplate, HasCitation, DefinedRole
{

    private Noisemaker() : base("noisemaker", new(160, 131, 187), RoleCategory.CrewmateRole, Crewmate.MyTeam, [NoiseDurationOption])
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Nebula.Roles.Ghost.Complex.Noiseghost.MyRole.ConfigurationHolder!]);
    }

    Citation? HasCitation.Citaion => Citations.AmongUs;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public FloatConfiguration NoiseDurationOption = NebulaAPI.Configurations.Configuration("options.role.noisemaker.noiseDuration", (1f, 10f, 1f), 3f, FloatConfigurationDecorator.Second);

    static public Noisemaker MyRole = new Noisemaker();

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated() { }


        bool isDeadAtLastTaskpPhase = false;
        [OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (AmOwner)
            {
                new StaticAchievementToken("noisemaker.common1");
                isDeadAtLastTaskpPhase = true;
            }

            if (!NebulaGameManager.Instance!.LocalPlayerInfo.Role.IgnoreNoisemakerNotification)
            {
                AmongUsUtil.InstantiateNoisemakerArrow(ev.Player.VanillaPlayer.transform.position, true).arrow.SetDuration(NoiseDurationOption);
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (isDeadAtLastTaskpPhase)
            {
                if (ev.Exiled.Contains(MyPlayer)) new StaticAchievementToken("noisemaker.challenge");
                isDeadAtLastTaskpPhase = false;
            }
        }
    }
}

