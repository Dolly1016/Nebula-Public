using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.Modifier;

public class Evolver : ConfigurableStandardModifier
{
    static public Evolver MyRole = null!;// new Evolver();
    public override string LocalizedName => "evolver";
    public override string CodeName => "EVL";
    public override Color RoleColor => new Color(231f / 255f, 135f / 255f, 135f / 255f);

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);
    }
    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;
        
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        public override void OnActivated()
        {
            var perkHolderContent = HudContent.InstantiateContent("PerkIcons", true, true, false, true).SetPriority(-10);
            this.Bind(perkHolderContent.gameObject);

            var holder = perkHolderContent.gameObject;
        }


    }
}
