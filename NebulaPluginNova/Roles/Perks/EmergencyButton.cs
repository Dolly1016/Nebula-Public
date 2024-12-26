using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game.Meeting;
using Virial.Events.Game;
using JetBrains.Annotations;

namespace Nebula.Roles.Perks;

internal class EmergencyButton : PerkFunctionalInstance
{
    static PerkFunctionalDefinition Def = new("emergencyButton", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("emergencyButton", 2, 8, new(206,198,42)), (def, instance) => new EmergencyButton(def, instance));


    private EmergencyButton(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (!MyPlayer.VanillaPlayer.CanMove) return;

        var prefab = ShipStatus.Instance.EmergencyButton.MinigamePrefab;

        PlayerControl.LocalPlayer.NetTransform.Halt();
        Minigame minigame = GameObject.Instantiate<Minigame>(prefab);
        minigame.transform.SetParent(Camera.main.transform, false);
        minigame.transform.localPosition = new Vector3(0f, 0f, -50f);
        minigame.Begin(null);
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(MyPlayer.VanillaPlayer.CanMove ? Color.white : Color.gray);
    }
}
