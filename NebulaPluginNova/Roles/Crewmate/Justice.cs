using Nebula.Behaviour;
using Nebula.Patches;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Justice : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public Justice MyRole = null;//new Justice();
    private Justice():base("justice", new(255, 128, 0), RoleCategory.CrewmateRole, Crewmate.MyTeam, [PutJusticeOnTheBalanceOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
    }

    Citation? HasCitation.Citaion => Citations.SuperNewRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private BoolConfiguration PutJusticeOnTheBalanceOption = new BoolConfigurationImpl("role.justice.putJusticeOnTheBalance", false);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        static private SpriteLoader meetingSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeIcon.png", 115f);

        void RuntimeAssignable.OnActivated() { }

        bool usedBalance = false;
        bool isMyJusticeMeeting = false;

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            void StartJusticeMeeting(GamePlayer p1, GamePlayer p2)
            {
                MeetingModRpc.RpcChangeVotingStyle.Invoke(((1 << p1.PlayerId) | (1 << p2.PlayerId), false, 100f, true));
                new StaticAchievementToken("justice.common1");
                if(p1.IsImpostor || p2.IsImpostor) new StaticAchievementToken("justice.common2");
                isMyJusticeMeeting = true;
            }

            if (!usedBalance)
            {
                var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
                buttonManager?.RegisterMeetingAction(new(meetingSprite,
                   p =>
                   {
                       if (PutJusticeOnTheBalanceOption)
                       {
                           StartJusticeMeeting(p.MyPlayer,MyPlayer);
                           usedBalance = true;
                       }
                       else
                       {
                           if (p.IsSelected)
                               p.SetSelect(false);
                           else
                           {
                               var selected = buttonManager.AllStates.FirstOrDefault(p => p.IsSelected);

                               if (selected != null)
                               {
                                   selected.SetSelect(false);

                                   StartJusticeMeeting(p.MyPlayer,selected.MyPlayer);
                                   usedBalance = true;
                               }
                               else
                               {
                                   p.SetSelect(true);
                               }
                           }
                       }
                   },
                   p => !usedBalance && !p.MyPlayer.IsDead
                   ));
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (isMyJusticeMeeting)
            {
                if (ev.Exiled.Any(e => e.AmOwner)) new StaticAchievementToken("justice.another1");
                if(ev.Exiled.Count() == 2)
                {
                    new StaticAchievementToken("justice.common3");
                    if(ev.Exiled.All(e => e.IsImpostor)) new StaticAchievementToken("justice.challenge");
                }

                isMyJusticeMeeting = false;
            }
        }
    }
}