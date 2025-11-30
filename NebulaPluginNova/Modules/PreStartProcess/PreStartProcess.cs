using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.PreStartProcess;

internal interface IPreStartProcess
{
    void Start(Virial.Game.Game game);
}
