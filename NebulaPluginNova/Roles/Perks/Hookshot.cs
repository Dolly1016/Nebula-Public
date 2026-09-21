using Nebula.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Game;
using static Nebula.Roles.Crewmate.Climber;

namespace Nebula.Roles.Perks;

internal class Hookshot : PerkFunctionalInstance
{
    static PerkFunctionalDefinition Def = new("hookshot", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("hookshot", 8, 57, Crewmate.Climber.MyRole.Color), (def, instance) => new Hookshot(def, instance));

    bool used = false;
    public Hookshot(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        GamePlayer.LocalPlayer?.AttachAbility(new UseActionBlocker(GamePlayer.LocalPlayer, this));
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (used || !MyPlayer.CanMove || MyPlayer.IsDead || Climber.Hookshot.LocalIsActive) return;

        Climber.SearchPointAndSendJump();
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(used ? Color.gray : Color.white);
    }

    internal class UseActionBlocker : AbstractPlayerAbility, IPlayerAbility
    {
        public UseActionBlocker(GamePlayer player, Hookshot hookshotPerk) : base(player)
        {
            this.Bind(hookshotPerk);
        }

        private Crewmate.Climber.Hookshot? hookshot = null;
        public void SetHookshot(Crewmate.Climber.Hookshot hookshot)
        {
            this.hookshot = hookshot;
        }

        bool IPlayerAbility.BlockUsingUtility => hookshot != null && !hookshot.IsDeadObject && !hookshot.IsDisappearing;
    }
}
