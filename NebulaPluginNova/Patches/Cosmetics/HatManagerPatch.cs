using Nebula.Modules.Cosmetics;

namespace Nebula.Patches.Cosmetics;

// Get○○ByIdの書き換え
// all○○フィールド書き換えの代替策。
// all○○フィールドに新たな配列を代入すると、いろいろのキャストが狂いだす。
// おそらく、Il2CppReferenceArrayは生成してはいけない。

/// <summary>
/// Hat
/// </summary>
[HarmonyPatch(typeof(HatManager), nameof(HatManager.GetHatById))]
public class HatManagerGetHatByIdPatch
{
    public static bool Prefix(HatManager __instance, ref HatData __result, [HarmonyArgument(0)] string hatId)
    {
        if(MoreCosmic.AllHats.TryGetValue(hatId, out var value)){
            __result = value.MyHat;
            return false;
        }
        return true;
    }
}

/// <summary>
/// Visor
/// </summary>
[HarmonyPatch(typeof(HatManager), nameof(HatManager.GetVisorById))]
public class HatManagerGetVisorByIdPatch
{
    public static bool Prefix(HatManager __instance, ref VisorData __result, [HarmonyArgument(0)] string visorId)
    {
        if (MoreCosmic.AllVisors.TryGetValue(visorId, out var value))
        {
            __result = value.MyVisor;
            return false;
        }
        return true;
    }
}

/// <summary>
/// Nameplate
/// </summary>
[HarmonyPatch(typeof(HatManager), nameof(HatManager.GetNamePlateById))]
public class HatManagerGetNamePlateByIdPatch
{
    public static bool Prefix(HatManager __instance, ref NamePlateData __result, [HarmonyArgument(0)] string namePlateId)
    {
        if (MoreCosmic.AllNameplates.TryGetValue(namePlateId, out var value))
        {
            __result = value.MyPlate;
            return false;
        }
        return true;
    }
}

//HatManager.GetUnlocked○○ は使用しない。(あるいは、バニラの解放済みハットを取得するためだけに使用する。)
//HatManager.All○○ は使用しない。(あるいは、バニラのハットを取得するためだけに使用する。)
