using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modifications.Map;

static internal class MIRAModification
{
    public static void Modify(ShipStatus ship)
    {
        if (!GeneralConfigurations.MiraAdminOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.Admin].gameObject.transform.FindChild("MapTable").gameObject;
            GameObject.Destroy(obj.transform.GetChild(0).gameObject);
        }

        ship.Systems[SystemTypes.LifeSupp].Cast<LifeSuppSystemType>().LifeSuppDuration = GeneralConfigurations.MiraO2DurationOption.CurrentValue;
        ship.Systems[SystemTypes.Reactor].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.MiraReactorDurationOption.CurrentValue;
    }

}
