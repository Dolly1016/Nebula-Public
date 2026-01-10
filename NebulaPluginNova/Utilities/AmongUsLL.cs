using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Internal;

namespace Nebula.Utilities;

internal class AmongUsLLImpl : AmongUsLL
{
    static internal AmongUsLL Instance { get; } = new AmongUsLLImpl();

    float AmongUsLL.VanillaKillCooldown => GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
    float AmongUsLL.VanillaKillDistance => GameManager.Instance.LogicOptions.GetKillDistance();
}
