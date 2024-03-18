using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Effacer : ConfigurableStandardRole, HasCitation
{
    static public Effacer MyRole = null;//new Effacer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "effacer";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EffaceCoolDownOption = null!;
    private NebulaConfiguration EffaceDurationOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        EffaceCoolDownOption = new NebulaConfiguration(RoleConfig, "effaceCoolDown", null, 10f, 60f, 2.5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        EffaceDurationOption = new NebulaConfiguration(RoleConfig, "effaceDuration", null, 5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? cleanButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CleanButton.png", 115f);
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                var effaceTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, p => ObjectTrackers.StandardPredicate(p) && (p.GetModInfo()?.VisibilityLevel ?? 2) == 0));

                cleanButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                cleanButton.SetSprite(buttonSprite.GetSprite());
                cleanButton.Availability = (button) => effaceTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                cleanButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                cleanButton.OnClick = (button) => {
                    (effaceTracker.CurrentTarget.GetModInfo() as GamePlayer)?.GainAttribute(PlayerAttributes.InvisibleElseImpostor, MyRole.EffaceDurationOption.GetFloat(), false, 0);
                    cleanButton.StartCoolDown();
                };
                cleanButton.CoolDownTimer = Bind(new Timer(MyRole.EffaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                cleanButton.SetLabel("clean");
            }
        }
    }
}
