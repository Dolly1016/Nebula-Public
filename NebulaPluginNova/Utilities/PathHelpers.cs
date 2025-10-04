using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

static public class PathHelpers
{
    static public string GameRootPath =>
#if PC
        BepInEx.Paths.GameRootPath;
#else
        BepInEx.Paths.BepInExRootPath + Path.DirectorySeparatorChar + "NebulaOnTheShip";
#endif

    static public string BepInExRootPath => BepInEx.Paths.BepInExRootPath;
}
