using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Nebula.Patches;

file static class MultipleExileHelper
{
    public static PoolablePlayer SpawnMultiplePlayer(ExileController __instance, NetworkedPlayerInfo exiled)
    {
        var result = GameObject.Instantiate(__instance.Player, __instance.Player.transform.parent);
        result.UpdateFromEitherPlayerDataOrCache(exiled, PlayerOutfitType.Default, PlayerMaterial.MaskType.Exile, false, (Il2CppSystem.Action)(()=>
        {
            SkinViewData skin = AmongUsLLImpl.ShipStatusInstance.CosmeticsCache.GetSkin(exiled.Outfits[PlayerOutfitType.Default].SkinId);
            if (!DestroyableSingleton<HatManager>.Instance.CheckLongModeValidCosmetic(exiled.Outfits[PlayerOutfitType.Default].SkinId, result.GetIgnoreLongMode()))
            {
                skin = AmongUsLLImpl.ShipStatusInstance.CosmeticsCache.GetSkin("skin_None");
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
        foreach (var p in MeetingHudExtension.ExiledAllModCache ?? [])
        {
            if (p.PlayerId != (__instance.initData.networkedPlayer?.PlayerId ?? byte.MaxValue))
            {
                var display = MultipleExileHelper.SpawnMultiplePlayer(__instance, GameData.Instance.GetPlayerById(p.PlayerId));
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

    public static IEnumerator CoSpin(PoolablePlayer p, int i, VVector2 from, VVector2 to, VVector2 diffVectorNorm, float duration, AnimationCurve curve, float rotateSpeed, Action<VVector2>? callback = null)
    {
        float random = System.Random.Shared.NextSingle() * Math.Max(5f, rotateSpeed * 0.5f);
        float randomVec = 0.3f + System.Random.Shared.NextSingle() * 0.8f;
        if (i % 2 == 0) randomVec *= -1f;
        from += diffVectorNorm * randomVec;
        to += diffVectorNorm * randomVec;
        callback?.Invoke(diffVectorNorm * randomVec);

        var pTransform = p.transform;

        pTransform.localScale *= 0.75f;

        yield return ManagedEffects.Lerp(duration, num =>
        {
            float t = num * duration;
            pTransform.localPosition = VVector2.Lerp(from, to, curve.Evaluate(num)).AsUnityVector3(0f);
            float num2 = (t + 0.75f) * 25f / Mathn.Exp(t * 0.75f + 1f);
            pTransform.Rotate(new Vector3(0f, 0f, num2 * Time.deltaTime * (rotateSpeed + random)));
        });
    }

    public static void SetExiledStampShower(PoolablePlayer p, bool vertical, VVector2 diff)
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
            yield return MultipleExileHelper.CoSpin(p, i, VVector2.Left * l, VVector2.Right * l, VVector2.Up, __instance.Duration, __instance.LerpCurve, 30f);
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p =>
        {
            MultipleExileHelper.SetExiledStampShower(p, false, new(0.7f, 0.18f));
            p.cosmetics.GetComponent<NebulaCosmeticsLayer>().IsExileCut = true;
        });
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
            yield return MultipleExileHelper.CoSpin(p, i, VVector2.Up * l, VVector2.Down * l, VVector2.Left, __instance.Duration, __instance.LerpCurve, 5f);
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p =>
        {
            MultipleExileHelper.SetExiledStampShower(p, false, new(0.55f, 0.18f));
            p.cosmetics.GetComponent<NebulaCosmeticsLayer>().IsExileCut = true;
        });
    }
}

[HarmonyPatch(typeof(PbExileController), nameof(PbExileController.Animate))]
internal class PbMultipleExilePatch
{
    public static void Postfix(PbExileController __instance)
    {
        IEnumerator CoWaitAndPlaySplash(PoolablePlayer p, VVector2 vec)
        {
            var sploosher = GameObject.Instantiate(__instance.Sploosher, __instance.Sploosher.transform.parent);
            
            sploosher.gameObject.SetActive(false);
            sploosher.transform.localPosition += vec.AsUnityVector3();

            while(p.transform.localPosition.y > -1.7f) yield return null;
            
            sploosher.gameObject.SetActive(true);
            sploosher.Play(__instance.Sploosh, 1f);
        }

        IEnumerator CoAnimate(PoolablePlayer p, int i)
        {
            yield return Effects.Wait(0.95f + System.Random.Shared.NextSingle() * 0.4f);

            p.gameObject.SetActive(true);

            float l = Camera.main.orthographicSize + 1f;
            yield return MultipleExileHelper.CoSpin(p, i, VVector2.Up * l, VVector2.Down * 2.81f, VVector2.Left, __instance.Duration / 1.4f, __instance.LerpCurve, 5f, (vec) => __instance.StartCoroutine(CoWaitAndPlaySplash(p, vec).WrapToIl2Cpp()));
        }

        var mask = __instance.transform.FindChild("New Sprite Mask").GetComponent<SpriteMask>();
        mask.isCustomRangeActive = true;
        mask.backSortingOrder = -1000;
        mask.frontSortingOrder = 0;
        
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {
            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p =>
        {
            MultipleExileHelper.SetExiledStampShower(p, true, new(0.1f, 0.5f));
            p.cosmetics.GetComponent<NebulaCosmeticsLayer>().IsExileCut = true;
        });
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

            var pTransform = p.transform;

            p.gameObject.SetActive(true);
            PlayerMaterial.SetColors(p.ColorId, pTransform.GetChild(3).GetComponent<SpriteRenderer>());
            pTransform.localScale *= 0.76f;

            Camera main = Camera.main;
            float num = main.orthographicSize + 1.5f;
            float num2 = main.orthographicSize * main.aspect;
            VVector2 sourcePos = VVector2.Up * num + VVector2.Right * num2 / 4f;
            VVector2 targetPos = VVector2.Down * num + VVector2.Left * num2 / 4f;

            float randomVec = 0.3f + System.Random.Shared.NextSingle() * 0.8f;
            if (i % 2 == 0) randomVec *= -1f;
            sourcePos += VVector2.Right * randomVec;
            targetPos += VVector2.Right * randomVec;

            VVector2 vector = (targetPos - sourcePos) / 2f;
            VVector2 anchor = sourcePos + vector + vector.Rotate(-90f).Normalized * 0.5f;
            float d = __instance.Duration;
            for (float t = 0f; t <= d; t += Time.deltaTime * 1.05f)
            {
                float num3 = t / d;
                Vector2 vector2 = Effects.Bezier(num3, sourcePos, targetPos, anchor);
                pTransform.localPosition = vector2.AsVector3(i * 0.5f);
                float num4 = Mathn.Lerp(0f, 80f, num3);
                pTransform.localEulerAngles = new Vector3(0f, 0f, num4);
                yield return null;
            }
            yield break;
        }
        MultipleExileHelper.SpawnMultiplePlayers(__instance, (p, i) => {

            __instance.StartCoroutine(CoAnimate(p, i).WrapToIl2Cpp());
        }, p =>
        {
            MultipleExileHelper.SetExiledStampShower(p, false, new(0.6f, 0.18f));
            p.cosmetics.GetComponent<NebulaCosmeticsLayer>().IsExileCut = true;
        });
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