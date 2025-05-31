using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;

namespace Nebula.Roles.Modifier;

internal class Madmate : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Madmate() : base("madmate", "MDM", new(Palette.ImpostorRed), [], allocateToImpostor: false)
    {
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Madmate.png");
        ConfigurationHolder?.ScheduleAddRelated(()=> [Crewmate.Madmate.MyRole.ConfigurationHolder!]);
    }

    string DefinedAssignable.InternalName => "madmateModifier";
    string DefinedAssignable.GeneralBlurb => (Crewmate.Madmate.MyRole as DefinedAssignable).GeneralBlurb;

    static public Madmate MyRole = new Madmate();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.MadmatePhase) return;
            ev.IsExtraWin |= ev.GameEnd == NebulaGameEnd.ImpostorWin;
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= ev.GameEnd != NebulaGameEnd.ImpostorWin;

        void RuntimeAssignable.OnActivated() { }

        string RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort)
        {
            return Language.Translate("role.madmate.prefix").Color(MyRole.UnityColor) + lastRoleName;
        }

        //bool RuntimeAssignable.CanKill(Virial.Game.Player player) => !(player.Role is Jackal.Instance ji && ji.IsSameTeam(MyPlayer));
        bool RuntimeModifier.MyCrewmateTaskIsIgnored => true;
        bool RuntimeModifier.IsMadmate => false;
    }
}


