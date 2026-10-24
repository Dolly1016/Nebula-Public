using Nebula.Map;
using Nebula.VoiceChat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modifications.Map;

static internal class SkeldModification
{
    static public void Modify(ShipStatus ship)
    {
        if (GeneralConfigurations.SkeldShadowOption.CurrentValue)
        {
            var mat = ship.FastRooms[SystemTypes.Nav].transform.parent.GetChild(0).GetComponent<Renderer>().material;
            var renderer = ship.transform.GetChild(0).GetComponent<Renderer>();
            renderer.material = mat;
            renderer.gameObject.layer = LayerExpansion.GetShipLayer();

            SkeldData.SkeldShadowEdges.Do(points =>
            {
                ShipModification.AddModShadow(points);
            });
        }
        if (GeneralConfigurations.SkeldCafeVentOption.CurrentValue) ShipModification.CreateVent(SystemTypes.Cafeteria, "CafeUpperVent", new UnityEngine.Vector2(-2.1f, 3.8f));
        if (GeneralConfigurations.SkeldStorageVentOption.CurrentValue) ShipModification.CreateVent(SystemTypes.Storage, "StorageVent", new UnityEngine.Vector2(0.45f, -3.6f));

        if (!GeneralConfigurations.SkeldAdminOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.Admin].gameObject.transform.parent.GetChild(0).GetChild(3).gameObject;
            GameObject.Destroy(obj.transform.GetChild(1).GetComponent<CircleCollider2D>());
        }

        ship.Systems[SystemTypes.LifeSupp].Cast<LifeSuppSystemType>().LifeSuppDuration = GeneralConfigurations.SkeldO2DurationOption.CurrentValue;
        ship.Systems[SystemTypes.Reactor].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.SkeldReactorDurationOption.CurrentValue;

        //ファンが隠れる問題を修正
        var fan = ship.FastRooms[SystemTypes.LifeSupp].transform.parent.GetChild(0).GetChild(3);
        fan.SetLocalZ(-0.09f);
        fan.gameObject.layer = LayerExpansion.GetShipLayer();

        for (int i = 0; i < ship.AllDoors.Count; i++) ship.AllDoors[i].Id = i;

        if (GeneralConfigurations.UseVoiceChatOption)
        {
            CameraVC.Activate((new(-7.15f, 1.67f), 0), (new(-17.67f, -4.54f), 0), (new(0.54f, -6.18f), 0), (new(13.12f, -3.81f), 0));
        }
    }
}
