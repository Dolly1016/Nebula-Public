using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Security.Cryptography;
using Virial.Events.Game;
using Virial.Game;
using static UnityEngine.UI.Image;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
public class LightPatch
{
    public static float lastRange = 1f;
    public static float lastRangeForDive = 1f;
    public static float LastCalculatedRange = 1f;


    public static bool Prefix(ref float __result, ShipStatus __instance, [HarmonyArgument(0)] NetworkedPlayerInfo? player)
    {
        if (__instance == null)
        {
            lastRange = 1f;
            return true;
        }

        if (player == null || player.IsDead)
        {
            __result = __instance.MaxLightRadius;
            return false;
        }

        if ((NebulaGameManager.Instance?.GameState ?? NebulaGameStates.NotStarted) == NebulaGameStates.NotStarted) return true;

        ISystemType? systemType = __instance.Systems.ContainsKey(SystemTypes.Electrical) ? __instance.Systems[SystemTypes.Electrical] : null;
        SwitchSystem? switchSystem = systemType?.TryCast<SwitchSystem>();

        float t = (float)(switchSystem?.Value ?? 255f) / 255f;

        var info = GamePlayer.LocalPlayer;
        var modinfo = info?.Unbox();
        bool hasImpostorVision = modinfo?.Role.HasImpostorVision ?? false;
        bool ignoreBlackOut = (modinfo?.Role.IgnoreBlackout ?? true) || (info?.AllAbilities.Any(a => a.IgnoreBlackout) ?? false);

        if (ignoreBlackOut) t = 1f;

        float radiusRate = Mathn.Lerp(__instance.MinLightRadius, __instance.MaxLightRadius, t);
        float range = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(hasImpostorVision ? FloatOptionNames.ImpostorLightMod : FloatOptionNames.CrewLightMod);
        var ev = GameOperatorManager.Instance?.Run(new LightRangeUpdateEvent(1f));
        float rate = ev?.LightRange ?? 1f;
        float quickRate = ev?.LightQuickRange ?? 1f;

        rate *= GamePlayer.LocalPlayer?.Unbox().CalcAttributeVal(PlayerAttributes.Eyesight) ?? 1f;

        lastRange -= (lastRange - rate).Delta(0.7f * (ev?.LightSpeed ?? 1f), 0.005f);
        __result = radiusRate * range * lastRange;
        LastCalculatedRange = __result;

        if (info?.IsDived ?? false)
            lastRangeForDive = 0f;
        else
            lastRangeForDive += (1f - lastRangeForDive).Delta(6f, 0.005f);

        __result *= lastRangeForDive;
        __result *= quickRate;

        return false;
    }
}

//影貫通

[HarmonyPatch(typeof(LightSourceGpuRenderer), nameof(LightSourceGpuRenderer.GPUShadows))]
public static class LightSourceGpuRendererPatch
{
    static Il2CppReferenceArray<Collider2D> origArray = null!;
    static Il2CppReferenceArray<Collider2D> zeroArray = new(0L);

    public static bool Prefix(LightSourceGpuRenderer __instance, [HarmonyArgument(0)] Vector2 origin)
    {
        //追加の前処理
        origArray = __instance.hits;
        if (NebulaGameManager.Instance?.IgnoreWalls ?? false) __instance.hits = zeroArray;

        //本来の処理の改変ここから
        __instance.ClearEdges();
        var lightSource = __instance.lightSource;
        var viewDistance3 = lightSource.ViewDistance * 3f;
        lightSource.LightChild.transform.localScale = new Vector3(viewDistance3, viewDistance3, 1f);
        Camera main = Camera.main;
        Vector2 vector = main.transform.position - lightSource.transform.position;
        Vector2 vector2 = new(main.orthographicSize * main.aspect, main.orthographicSize);
        float num = vector2.magnitude + vector.magnitude;
        float num2 = Mathf.Min(lightSource.ViewDistance, num);
        num2 *= NebulaGameManager.Instance?.WideCamera?.CurrentRate ?? 1f; //改変箇所
        foreach (NoShadowBehaviour noShadowBehaviour in LightSource.NoShadows.Values)
        {
            noShadowBehaviour.CheckHit(num2, origin);
        }
        int num3 = Physics2D.OverlapCircleNonAlloc(origin, num2, __instance.hits, Constants.ShadowMask);
        for (int i = 0; i < num3; i++)
        {
            Collider2D collider2D = __instance.hits[i];
            NoShadowBehaviour noShadowBehaviour2;
            OneWayShadows oneWayShadows;
            if (!collider2D.isTrigger && (!LightSource.NoShadows.TryGetValue(collider2D.gameObject, out noShadowBehaviour2) || !(noShadowBehaviour2.hitOverride == collider2D)) && (!LightSource.OneWayShadows.TryGetValue(collider2D.gameObject, out oneWayShadows) || !oneWayShadows.IsIgnored(lightSource)))
            {
                EdgeCollider2D? edgeCollider2D = collider2D.TryCast<EdgeCollider2D>();
                if (edgeCollider2D)
                {
                    Vector2[] points = edgeCollider2D!.points;
                    for (int j = 0; j < points.Length - 1; j++)
                    {
                        Vector3 vector3 = edgeCollider2D.transform.TransformPoint(points[j]);
                        Vector3 vector4 = edgeCollider2D.transform.TransformPoint(points[j + 1]);
                        __instance.AddEdge.Invoke(vector3, vector4);
                    }
                }
                else
                {
                    PolygonCollider2D? polygonCollider2D = collider2D.TryCast<PolygonCollider2D>();
                    if (polygonCollider2D)
                    {
                        Vector2[] points2 = polygonCollider2D!.points;
                        for (int k = 0; k < points2.Length; k++)
                        {
                            int num4 = k + 1;
                            if (num4 == points2.Length)
                            {
                                num4 = 0;
                            }
                            Vector3 vector5 = polygonCollider2D.transform.TransformPoint(points2[k]);
                            Vector3 vector6 = polygonCollider2D.transform.TransformPoint(points2[num4]);
                            __instance.AddEdge.Invoke(vector5, vector6);
                        }
                    }
                    else
                    {
                        BoxCollider2D? boxCollider2D = collider2D.TryCast<BoxCollider2D>();
                        if (boxCollider2D)
                        {
                            Vector2 vector7 = boxCollider2D!.size / 2f;
                            Vector2 vector8 = boxCollider2D.transform.TransformPoint(boxCollider2D.offset - vector7);
                            Vector2 vector9 = boxCollider2D.transform.TransformPoint(boxCollider2D.offset + vector7);
                            Vector3 vector10 = vector8;
                            Vector3 vector11 = vector8;
                            vector11.y = vector9.y;
                            __instance.AddEdge.Invoke(vector10, vector11);
                            vector10.y = vector9.y;
                            vector11.x = vector9.x;
                            __instance.AddEdge.Invoke(vector10, vector11);
                            vector10 = vector9;
                            vector11.y = vector8.y;
                            __instance.AddEdge.Invoke(vector10, vector11);
                            vector10.y = vector8.y;
                            vector11 = vector8;
                            __instance.AddEdge.Invoke(vector10, vector11);
                        }
                    }
                }
            }
        }
        __instance.UpdateOccMesh();
        __instance.DrawOcclusion(num2);
        //本来の処理の改変ここまで

        //追加の後処理
        if (__instance.hits != origArray) __instance.hits = origArray;

        return false;
    }
}


[HarmonyPatch(typeof(LightSourceRaycastRenderer), nameof(LightSourceRaycastRenderer.RaycastShadows))]
public static class LightSourceRaycastRendererPatch
{
    public static bool Prefix(LightSourceRaycastRenderer __instance, [HarmonyArgument(0)] Vector2 origin)
    {
        var ignoreWalls = (NebulaGameManager.Instance?.IgnoreWalls ?? false);
        //以降すべて本体処理の改変

        __instance.vertCount = 0;
        __instance.lightSource.LightChild.transform.localScale = Vector3.one;
        int num = ignoreWalls ? 0 : Physics2D.OverlapCircleNonAlloc(origin, __instance.GetValidViewDistance(), __instance.hits, Constants.ShadowMask); //改変箇所(障害物の無視)
        for (int i = 0; i < num; i++)
        {
            Collider2D collider2D = __instance.hits[i];
            if (!collider2D.isTrigger)
            {
                EdgeCollider2D? edgeCollider2D = collider2D.TryCast<EdgeCollider2D>();
                if (edgeCollider2D)
                {
                    Vector2[] points = edgeCollider2D!.points;
                    for (int j = 0; j < points.Length; j++)
                    {
                        Vector2 vector = edgeCollider2D.transform.TransformPoint(points[j]);
                        __instance.del = new(vector.x - origin.x, vector.y - origin.y);
                        __instance.TestBothSides(origin);
                    }
                }
                else
                {
                    PolygonCollider2D? polygonCollider2D = collider2D.TryCast<PolygonCollider2D>();
                    if (polygonCollider2D)
                    {
                        Vector2[] points2 = polygonCollider2D!.points;
                        for (int k = 0; k < points2.Length; k++)
                        {
                            Vector2 vector2 = polygonCollider2D.transform.TransformPoint(points2[k]);
                            __instance.del = new(vector2.x - origin.x, vector2.y - origin.y);
                            __instance.TestBothSides(origin);
                        }
                    }
                    else
                    {
                        BoxCollider2D? boxCollider2D = collider2D.TryCast<BoxCollider2D>();
                        if (boxCollider2D)
                        {
                            Vector2 vector3 = boxCollider2D!.size / 2f;
                            Vector2 vector4 = (Vector2)boxCollider2D.transform.TransformPoint(boxCollider2D.offset - vector3) - origin;
                            Vector2 vector5 = (Vector2)boxCollider2D.transform.TransformPoint(boxCollider2D.offset + vector3) - origin;
                            __instance.del = new(vector4.x, vector4.y);
                            __instance.TestBothSides(origin);
                            __instance.del = new(vector5.x, __instance.del.y);
                            __instance.TestBothSides(origin);
                            __instance.del = new(__instance.del.x, vector5.y);
                            __instance.TestBothSides(origin);
                            __instance.del = new(vector4.x, __instance.del.y);
                            __instance.TestBothSides(origin);
                        }
                    }
                }
            }
        }
        float num2 = __instance.GetValidViewDistance() * 1.05f;
        for (int l = 0; l < __instance.requiredDels.Length; l++)
        {
            Vector2 vector6 = num2 * __instance.requiredDels[l];
            if (ignoreWalls)
            {
                //壁を無視する場合
                float v_num = __instance.GetValidViewDistance() * 1.5f;
                var normalized = vector6.normalized;
                __instance.GetEmptyVert().Complete(normalized.x * v_num, normalized.y * v_num);
            }
            else
            {
                __instance.CreateVert(origin, ref vector6);
            }
        }
        __instance.verts.Sort(0, __instance.vertCount, LightSourceRaycastRenderer.AngleComparer.Instance.Cast<Il2CppSystem.Collections.Generic.IComparer<LightSourceRaycastRenderer.VertInfo>>());
        __instance.myMesh.Clear();
        if (__instance.vec == null || __instance.vec.Length < __instance.vertCount + 1)
        {
            __instance.vec = new Vector3[__instance.vertCount + 1];
            __instance.uvs = new Vector2[__instance.vec.Length];
        }
        __instance.vec[0] = Vector3.zero;
        __instance.uvs[0] = new Vector2(__instance.vec[0].x, __instance.vec[0].y);
        for (int m = 0; m < __instance.vertCount; m++)
        {
            int num3 = m + 1;
            __instance.vec[num3] = __instance.verts[m].Position;
            __instance.uvs[num3] = new Vector2(__instance.vec[num3].x, __instance.vec[num3].y);
        }
        int num4 = __instance.vertCount * 3;
        if (num4 > __instance.triangles.Length)
        {
            __instance.triangles = new int[num4];
            Debug.LogWarning("Resized triangles to: " + num4.ToString());
        }
        int num5 = 0;
        for (int n = 0; n < __instance.triangles.Length; n += 3)
        {
            if (n < num4)
            {
                __instance.triangles[n] = 0;
                __instance.triangles[n + 1] = num5 + 1;
                if (n == num4 - 3)
                {
                    __instance.triangles[n + 2] = 1;
                }
                else
                {
                    __instance.triangles[n + 2] = num5 + 2;
                }
                num5++;
            }
            else
            {
                __instance.triangles[n] = 0;
                __instance.triangles[n + 1] = 0;
                __instance.triangles[n + 2] = 0;
            }
        }
        __instance.myMesh.vertices = __instance.vec;
        __instance.myMesh.uv = __instance.uvs;
        __instance.myMesh.SetIndices(__instance.triangles, 0, 0);

        return false;
    }
}

[HarmonyPatch(typeof(OneWayShadows), nameof(OneWayShadows.IsIgnored))]
public static class OneWayShadowsPatch
{
    public static bool Prefix(OneWayShadows __instance, ref bool __result, [HarmonyArgument(0)] LightSource lightSource)
    {
        var info = GamePlayer.LocalPlayer;
        if (info == null) return true;

        __result = (__instance.IgnoreImpostor && info.Role.HasImpostorVision) || __instance.RoomCollider.OverlapPoint(lightSource.transform.position);
        return false;
    }
}

[HarmonyPatch(typeof(LightSourceRaycastRenderer), nameof(LightSourceRaycastRenderer.GetValidViewDistance))]
public static class ValidViewDistancePatch
{
    public static void Postfix(LightSourceRaycastRenderer __instance, ref float __result)
    {
        __result *= NebulaGameManager.Instance?.WideCamera.CurrentRate ?? 1f;
    }
}
