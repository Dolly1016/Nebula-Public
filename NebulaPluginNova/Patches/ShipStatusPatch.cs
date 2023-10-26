using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ShipStatus),nameof(ShipStatus.Awake))]
public class ShipStatusPatch
{
    static public void Postfix(ShipStatus __instance)
    {
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