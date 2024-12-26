using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Runtime;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class DebugTools
{
    static DebugTools() => saver.TrySave();
    
    private static DataSaver saver = new("DevSettings");

    private static BooleanDataEntry debugMode = new("DebugMode", saver, false, shouldWrite: false);
    public static bool DebugMode => debugMode.Value;
    private static BooleanDataEntry showConfigurationId = new("ShowConfigurationId", saver, false, shouldWrite: false);
    private static BooleanDataEntry releaseAllAchievement = new("ReleaseAllAchievement", saver, false, shouldWrite: false);
    private static BooleanDataEntry writeAllAchievementsData = new("WriteAllAchievementsData", saver, false, shouldWrite: false);
    public static bool ShowConfigurationId => DebugMode && showConfigurationId.Value;
    public static bool WriteAllAchievementsData => DebugMode && writeAllAchievementsData.Value;
    public static bool ReleaseAllAchievement => DebugMode && releaseAllAchievement.Value;


}
