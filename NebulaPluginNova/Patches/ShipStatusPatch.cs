using Hazel;
using Nebula.Behavior;
using Nebula.Map;
using Nebula.Modules.CustomMap;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ShipStatus),nameof(ShipStatus.Awake))]
public class ShipStatusPatch
{
    static public void Prefix(ShipStatus __instance)
    {
        //ModdedMap
        if (false)
        {
            ModShipStatus.CleanOriginalShip(__instance);
        }
    }

    static public void Postfix(ShipStatus __instance)
    {
        if (__instance.Type == (ShipStatus.MapType)6) return;
        ModifyEarlier();
        __instance.StartCoroutine(Effects.Sequence(Effects.Wait(0.1f),ManagedEffects.Action(Modify).WrapToIl2Cpp()));
    }

    static private void ModifyEarlier()
    {
        ShipExtension.PatchEarlierModification(AmongUsUtil.CurrentMapId);

        foreach(var vectors in MapData.GetCurrentMapData().RaiderIgnoreArea)
        {
            var collider = UnityHelper.CreateObject<PolygonCollider2D>("RaiderIgnoreArea", null, Vector3.zero, LayerExpansion.GetRaiderColliderLayer());
            collider.SetPath(0, vectors);
            collider.isTrigger = true;
        }
    }

    static private void Modify() { 
        ShipExtension.PatchModification(AmongUsUtil.CurrentMapId);
        if (GeneralConfigurations.SilentVentOption)
        {
            //ベントを見えなくする
            foreach (var vent in ShipStatus.Instance.AllVents)
            {
                GameObject shadowObj = new GameObject("ShadowVent");
                shadowObj.transform.SetParent(vent.transform);
                shadowObj.transform.localPosition = new Vector3(0f, 0f, 0f);
                shadowObj.transform.localScale = new Vector3(1f, 1f, 1f);

                Sprite sprite = null!;

                if (AmongUsUtil.CurrentMapId == 5)
                {
                    var renderer = vent.transform.GetChild(3);
                    sprite = renderer.GetComponent<SpriteRenderer>().sprite;
                    shadowObj.transform.localPosition = renderer.transform.localPosition + new Vector3(0,0,-0.1f);
                }
                else
                    sprite = vent.GetComponent<SpriteRenderer>().sprite;

                shadowObj.AddComponent<SpriteRenderer>().sprite = sprite;
                shadowObj.layer = LayerExpansion.GetShadowLayer();

                vent.gameObject.layer = LayerExpansion.GetDefaultLayer();
            }
        }
    }
}

//ヘリサボタージュ　ここだけ定数なのでパッチングで対応
[HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.UpdateSystem))]
class HeliSabotageSystemPatch
{
    static void Postfix(HeliSabotageSystem __instance, [HarmonyArgument(1)] MessageReader msgReader)
    {
        if((msgReader.GetPrevByte() & 240) == (int)HeliSabotageSystem.Tags.DamageBit)
        {
            __instance.Countdown = GeneralConfigurations.AirshipHeliDurationOption.CurrentValue;
        }
    }
}
