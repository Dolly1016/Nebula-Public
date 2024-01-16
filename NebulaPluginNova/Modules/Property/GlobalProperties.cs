using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.Property;

[NebulaPreLoad]
static public class GlobalProperties
{
    static public void Load()
    {
        new NebulaGlobalFunctionProperty("myPuid", () =>
        {
            try
            {
                return PlayerControl.LocalPlayer.Data.Puid;
            }
            catch
            {
                return "";
            }
        }, () => 0f);

        new NebulaGlobalFunctionProperty("addons", () => "" , () => 0f,
            (arg) => {
                var addon = NebulaAddon.GetAddon(arg);
                return new NebulaInstantProperty() { IntegerProperty = addon?.Build ?? -1, StringProperty = addon?.Build.ToString() ?? "-" };
            });
    }
}
