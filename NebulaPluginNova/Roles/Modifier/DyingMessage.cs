using Nebula.Map;
using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Media;

namespace Nebula.Roles.Modifier;

public class DyingMessage : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, IAssignableDocument
{
    private DyingMessage() : base("dyingMessage", "DYM", new(DyingMessageCanvas.BloodColor), [MessageDurationOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }

    static public float MessageDuration => MessageDurationOption;
    static private FloatConfiguration MessageDurationOption = NebulaAPI.Configurations.Configuration("options.role.dyingMessage.messageDuration", (0.5f, 10f, 0.5f), 2f, FloatConfigurationDecorator.Second);

    static public DyingMessage MyRole = new DyingMessage();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    bool IAssignableDocument.HasTips => true;
    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        bool RuntimeAssignable.CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (canSeeAllInfo || inEndScene) name += MyRole.GetRoleIconTagSmall();
        }

        [OnlyMyPlayer, Local]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.WithDeadBody && ev.Murderer != ev.Dead && ev.DeadBodyPos.HasValue)
            {
                var pos = ev.DeadBodyPos.Value;

                NebulaManager.Instance.StartDelayAction(1f, () => DyingMessages.GenerateCanvas(pos, MessageDuration, MyRole, _ => { 
                    new StaticAchievementToken("dyingMessage.common1"); 
                    if(ShipStatus.Instance.AllRooms.Find(room => room.roomArea.OverlapPoint(pos), out var found) && (found.RoomId == SystemTypes.Cafeteria || found.RoomId == SystemTypes.Kitchen))
                    {
                        new StaticAchievementToken("dyingMessage.common2");
                    }
                    GameOperatorManager.Instance?.SubscribeSingleListener<MeetingEndEvent>(_ =>
                    {
                        if (ev.Murderer.PlayerState == PlayerState.Exiled) new StaticAchievementToken("dyingMessage.challenge");
                    });
                }, () => new StaticAchievementToken("dyingMessage.another1")));
            }
        }
    }
}