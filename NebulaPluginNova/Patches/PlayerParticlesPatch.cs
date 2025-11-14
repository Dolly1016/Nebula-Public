using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;

namespace Nebula.Patches;


[HarmonyPatch(typeof(PlayerParticles), nameof(PlayerParticles.Start))]
public static class PlayerParticlesPatch
{
    static void Prefix(PlayerParticles __instance)
    {
        var pool = __instance.pool;
        pool.poolSize = 18;
        while(pool.NotInUse < pool.poolSize) pool.CreateOneInactive(pool.Prefab);
    }
}

[HarmonyPatch(typeof(PlayerParticle), nameof(PlayerParticle.Update))]
public static class PlayerParticleUpdatePatch
{
    static bool Prefix(PlayerParticle __instance)
    {
        if(PlayerParticlesPlacePatch.curveDic.TryGetValue(__instance.transform.GetSiblingIndex(), out var angle))
        {
            __instance.velocity = __instance.velocity.Rotate(angle * Time.deltaTime);
        }

        if (MainMenuManagerInstance.MainMenu)
        {
            var transform = MainMenuManagerInstance.MainMenu!.screenMask.transform;
            var xScale = transform.lossyScale.x * 0.5f + 1.6f;
            var yScale = transform.lossyScale.y * 0.5f + 1.6f;
            var xPos = transform.position.x;
            var yPos = transform.position.y;

            var myX = __instance.transform.position.x;
            var myY = __instance.transform.position.y;

            if (Mathn.Abs(myX - xPos) > xScale || Mathn.Abs(myY - yPos) > yScale) {
                var dot = Vector2.Dot(__instance.velocity.normalized, ((Vector2)transform.position - (Vector2)__instance.transform.position).normalized);
                if (dot < 0.1f)
                {
                    __instance.OwnerPool.Reclaim(__instance);
                    return false;
                }
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerParticles), nameof(PlayerParticles.PlacePlayer))]
public static class PlayerParticlesPlacePatch
{
    internal static Dictionary<int, float> curveDic = [];
    static void Postfix(PlayerParticles __instance, [HarmonyArgument(0)] PlayerParticle part, [HarmonyArgument(1)] bool initial)
    {
        float GetRandomAccelAngle() => (System.Random.Shared.NextSingle() * 16f) - 8f;
        curveDic[part.transform.GetSiblingIndex()] = GetRandomAccelAngle();
        //ランダムに一人の向きを変える
        curveDic[System.Random.Shared.Next(__instance.transform.childCount)] = GetRandomAccelAngle();
        if (MainMenuManagerInstance.MainMenu && !initial)
        {
            var transform = MainMenuManagerInstance.MainMenu!.screenMask.transform;
            var xScale = transform.lossyScale.x * 0.5f + 1.6f;
            var yScale = transform.lossyScale.y * 0.5f + 1.6f;
            var xPos = transform.position.x;
            var yPos = transform.position.y;

            var myX = part.transform.position.x;
            var myY = part.transform.position.y;

            var diffX = myX - xPos;
            var diffY = myY - yPos;
            if (Mathn.Abs(diffX) > xScale + 0.8f) myX = diffX < 0f ? xPos - xScale : xPos + xScale;
            if (Mathn.Abs(diffY) > yScale + 0.8f) myY = diffY < 0f ? yPos - yScale : yPos + yScale;

            part.transform.position = new(myX, myY, part.transform.position.z);
        }
    }
}