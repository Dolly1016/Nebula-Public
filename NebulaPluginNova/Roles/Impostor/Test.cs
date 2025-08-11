using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

/*
internal class Test : DefinedSingleAbilityRoleTemplate<Test.Ability>, DefinedRole
{
    private Test() : base("test", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [])
    {
        
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Test MyRole = new();
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
            }
        }

        void OnHudUpdate(GameHudUpdateEvent ev)
        {
            if (Input.GetKeyDown(KeyCode.Y))
            {
                bool used = false;
                var killButton = NebulaAPI.Modules.PlayerlikeKillButton(this, MyPlayer, false, Virial.Compat.VirtualKeyInput.None, null,
                    0f, "kill", Virial.Components.ModAbilityButton.LabelType.Impostor, null,
                    (p, button) =>
                    {
                        MyPlayer.MurderPlayer(p, PlayerState.Dead, null, KillParameter.NormalKill);
                        used = true;
                    }, null, null, _ => !used).SetAsUsurpableButton(this);
            }
        }
    }
}
*/