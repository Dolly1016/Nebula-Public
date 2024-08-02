using Virial.Runtime;

namespace Nebula.Modules.Property;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
static public class GlobalProperties
{
    static void Preprocess(NebulaPreprocessor preprocessor)
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
