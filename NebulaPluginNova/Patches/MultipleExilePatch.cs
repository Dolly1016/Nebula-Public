using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Events;

namespace Nebula.Patches;

file static class MultipleExileHelper
{
    public static PoolablePlayer SpawnMultiplePlayer(ExileController __instance, PlayerControl exiled)
    {
        var result = GameObject.Instantiate(__instance.Player, __instance.Player.transform.parent);
        result.UpdateFromEitherPlayerDataOrCache(exiled.Data, PlayerOutfitType.Default, PlayerMaterial.MaskType.Exile, false, (Il2CppSystem.Action)(()=>
        {
            SkinViewData skin = ShipStatus.Instance.CosmeticsCache.GetSkin(exiled.Data.Outfits[PlayerOutfitType.Default].SkinId);
            if (!DestroyableSingleton<HatManager>.Instance.CheckLongModeValidCosmetic(exiled.Data.Outfits[PlayerOutfitType.Default].SkinId, result.GetIgnoreLongMode()))
            {
                skin = ShipStatus.Instance.CosmeticsCache.GetSkin("skin_None");
            }
            result.FixSkinSprite(__instance.useIdleAnim ? skin.IdleFrame : skin.EjectFrame);
        }));
        result.ToggleName(false);
        if (!__instance.useIdleAnim)
        {
            result.SetCustomHatPosition(__instance.exileHatPosition);
            result.SetCustomVisorPosition(__instance.exileVisorPosition);
        }
        return result;
    }

    public static void SpawnMultiplePlayers(ExileController __instance, Action<PoolablePlayer, int> setup, Action<PoolablePlayer>? allPlayerPostfix = null)
    {
        int num = 0;
        foreach (var p in MeetingHudExtension.ExiledAll ?? [])
        {
            if (p.PlayerId != (__instance.initData.networkedPlayer?.PlayerId ?? byte.MaxValue))
            {
                var display = MultipleExileHelper.SpawnMultiplePlayer(__instance, p);
                setup.Invoke(display, num);
                num++;

                allPlayerPostfix?.Invoke(display);
            }
            else
            {
                allPlayerPostfix?.Invoke(__instance.Player);
            }
        }
    }

    public static IEnumerator CoSpin(PoolablePlayer p, int i, Vector2 from, Vector2 to, Vector2 diffVectorNorm, float duration, AnimationCurve curve, float rotateSpeed, Action<Vector2>? callback = null)
    {
        float random = System.Random.Shared.NextSingle() * Math.Max(5f, rotateSpeed * 0.5f);
        float randomVec = 0.3f + System.Random.Shared.NextSingle() * 0.8f;
        if (i % 2 == 0) randomVec *= -1f;
        from += diffVectorNorm * randomVec;
        to += diffVectorNorm * randomVec;
        callback?.Invoke(diffVectorNorm * randomVec);

        p.transform.localScale *= 0.75f;

        for (float t = 0f; t <= duration; t += Time.deltaTime)
        {
            float num = t / duration;
            p.transform.localPosition = Vector2.Lerp(from, to, curve.Evaluate(num));
            float num2 = (t + 0.75f) * 25f / Mathf.Exp(t * 0.75f + 1f);
            p.transform.Rotate(new Vector3(0f, 0f, num2 * Time.deltaTime * (rotateSpeed + random)));
            yield return null;
        }

    }

    public static void SetExiledStampShower(PoolablePlayer p, bool vertical, Vector2 diff)
    {
        var modPlayer = GamePlayer.GetPlayer((byte)p.ColorId);
        if (modPlayer == null) return;
        modPlayer.Unbox().SpecialStampShower = PopupStampShower.GetExiledShower(p, diff, null, vertical);
    }
}

[HarmonyPatch(typeof(SkeldExileController), nameof(SkeldExileController.Animate))]
internal class SkeldMultipleExilePatch
{
    public static void Postfix(SkeldExileController __instance)
    {
        IEnumerator CoAnimate(PoolablePlayer p, int i)
        {
            yield return Effects.Wait(1.5f);

            p.gameObject.SetActive(true);

            float l = Camera.main.orthographicSize * Camera.main.aspect + 1f;
            yield return MultipleExileHelper.CoSpin(p, i, Vector2.left * l, Vector2.right * l, Vector2.up, __instance.Duration, __instance.LerpCurve, 30f);
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p => MultipleExileHelper.SetExiledStampShower(p, false, new(0.7f, 0.18f)));
    }
}

[HarmonyPatch(typeof(MiraExileController), nameof(MiraExileController.Animate))]
internal class MiraMultipleExilePatch
{
    public static void Postfix(MiraExileController __instance)
    {
        IEnumerator CoAnimate(PoolablePlayer p, int i)
        {
            yield return Effects.Wait(1.5f);

            p.gameObject.SetActive(true);

            float l = Camera.main.orthographicSize + 1f;
            yield return MultipleExileHelper.CoSpin(p, i, Vector2.up * l, Vector2.down * l, Vector2.left, __instance.Duration, __instance.LerpCurve, 5f);
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p => MultipleExileHelper.SetExiledStampShower(p, false, new(0.55f, 0.18f)));
    }
}

[HarmonyPatch(typeof(PbExileController), nameof(PbExileController.Animate))]
internal class PbMultipleExilePatch
{
    public static void Postfix(PbExileController __instance)
    {
        IEnumerator CoWaitAndPlaySplash(PoolablePlayer p, Vector2 vec)
        {
            var sploosher = GameObject.Instantiate(__instance.Sploosher, __instance.Sploosher.transform.parent);
            
            sploosher.gameObject.SetActive(false);
            sploosher.transform.localPosition += (Vector3)vec;

            while(p.transform.localPosition.y > -1.7f) yield return null;
            
            sploosher.gameObject.SetActive(true);
            sploosher.Play(__instance.Sploosh, 1f);
        }

        IEnumerator CoAnimate(PoolablePlayer p, int i)
        {
            yield return Effects.Wait(0.95f + System.Random.Shared.NextSingle() * 0.4f);

            p.gameObject.SetActive(true);

            float l = Camera.main.orthographicSize + 1f;
            yield return MultipleExileHelper.CoSpin(p, i, Vector2.up * l, Vector2.down * 2.81f, Vector2.left, __instance.Duration / 1.4f, __instance.LerpCurve, 5f, (vec) => __instance.StartCoroutine(CoWaitAndPlaySplash(p, vec).WrapToIl2Cpp()));
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p => MultipleExileHelper.SetExiledStampShower(p, true, new(0.1f, 0.5f)));
    }
}

[HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.Animate))]
internal class AirshipMultipleExilePatch
{
    public static void Postfix(AirshipExileController __instance)
    {
        IEnumerator CoAnimate(PoolablePlayer p, int i)
        {
            yield return Effects.Wait(1.9f);

            p.gameObject.SetActive(true);
            PlayerMaterial.SetColors(p.ColorId, p.transform.GetChild(3).GetComponent<SpriteRenderer>());
            p.transform.localScale *= 0.76f;

            Camera main = Camera.main;
            float num = main.orthographicSize + 1.5f;
            float num2 = main.orthographicSize * main.aspect;
            Vector2 sourcePos = Vector2.up * num + Vector2.right * num2 / 4f;
            Vector2 targetPos = Vector2.down * num + Vector2.left * num2 / 4f;

            float randomVec = 0.3f + System.Random.Shared.NextSingle() * 0.8f;
            if (i % 2 == 0) randomVec *= -1f;
            sourcePos += Vector2.right * randomVec;
            targetPos += Vector2.right * randomVec;

            Vector2 vector = (targetPos - sourcePos) / 2f;
            Vector2 anchor = sourcePos + vector + vector.Rotate(-90f).normalized * 0.5f;
            float d = __instance.Duration;
            for (float t = 0f; t <= d; t += Time.deltaTime * 1.05f)
            {
                float num3 = t / d;
                Vector2 vector2 = Effects.Bezier(num3, sourcePos, targetPos, anchor);
                p.transform.localPosition = vector2.AsVector3(i * 0.5f);
                float num4 = Mathf.Lerp(0f, 80f, num3);
                p.transform.localEulerAngles = new Vector3(0f, 0f, num4);
                yield return null;
            }
            yield break;
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p => MultipleExileHelper.SetExiledStampShower(p, false, new(0.6f, 0.18f)));
    }
}

[HarmonyPatch(typeof(FungleExileController), nameof(FungleExileController.Animate))]
internal class FungleMultipleExilePatch
{
    public static void Postfix(FungleExileController __instance)
    {
        List<PoolablePlayer> extraPlayers = new();
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {
            extraPlayers.Add(p);
            __instance.StartCoroutine(Effects.Sequence(Effects.Wait(1.7f), Effects.Action((Il2CppSystem.Action)(() => p.FadeBlackAll(1.5f)))));
        }, p=>MultipleExileHelper.SetExiledStampShower(p,true, new(0.2f, 0.5f)));

        int i = 0;
        float left = extraPlayers.Count * 0.5f * -0.85f;
        
        foreach(var p in extraPlayers.Prepend(__instance.Player))
        {
            p.transform.localPosition += new Vector3(left + 0.85f * i, 0f, i * -0.1f);
            i++;
        }
    }
}