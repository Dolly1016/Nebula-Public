using Nebula.VoiceChat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modifications.Map;

static internal class PolusModification
{
    static public void Modify(ShipStatus ship)
    {
        {
            var mat = ship.FastRooms[SystemTypes.Weapons].transform.GetChild(0).GetComponent<Renderer>().material;
            var parallaxBg = ship.transform.GetChild(2);
            parallaxBg.GetChild(0).GetComponent<Renderer>().material = mat;
            var background = ship.transform.GetChild(3);
            background.GetComponent<Renderer>().material = mat;
            background.GetChild(1).GetComponent<Renderer>().material = mat;
            var cliff = background.GetChild(0);
            cliff.SetLocalZ(-15f);
            cliff.GetComponent<Renderer>().material = mat;
        }
        {
            var labo = ship.FastRooms[SystemTypes.Laboratory].transform;
            labo.GetChild(9).SetLocalZ(-2f);
            labo.GetChild(10).SetLocalZ(-2f);

            var o2 = ship.FastRooms[SystemTypes.LifeSupp].transform;
            o2.GetChild(10).SetLocalZ(-4f);
        }


        var commRoom = ship.FastRooms[SystemTypes.Comms];
        var commPos = commRoom.transform.localPosition;
        commPos.z = 0.0001f;
        commRoom.transform.localPosition = commPos;

        if (GeneralConfigurations.PolusSpecimenVentOption.CurrentValue) ShipModification.CreateVent(SystemTypes.Specimens, "SpecimenVent", new UnityEngine.Vector2(-1f, -1.35f));

        if (!GeneralConfigurations.PolusAdminOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.Admin].gameObject.transform.FindChild("mapTable").gameObject;
            GameObject.Destroy(obj.transform.GetChild(0).GetComponent<BoxCollider2D>());
            GameObject.Destroy(obj.transform.GetChild(1).GetComponent<BoxCollider2D>());
            GameObject.Destroy(obj.transform.GetChild(2).gameObject);
        }

        ship.Systems[SystemTypes.Laboratory].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.PolusReactorDurationOption.CurrentValue;

        //崩壊したオブジェクトの前後関係を修正
        ship.FastRooms[SystemTypes.Electrical].transform.FindChild("fencebot").SetLocalZ(-1.0136f);

        if (GeneralConfigurations.UseVoiceChatOption)
        {
            CameraVC.Activate((new(29.04f, -15.44f), 0), (new(15.54f, -15.44f), 1), (new(24.51f, -8.61f), 2), (new(17.09f, -20.39f), 3), (new(4.96f, -22.63f), 4), (new(11.41f, -8.31f), 5));
        }
    }
}
