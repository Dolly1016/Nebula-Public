using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Behavior;
using Nebula.VoiceChat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;

namespace Nebula.Modifications.Map;

static internal class AirshipModification
{
    private static SpriteLoader medicalWiringSprite = SpriteLoader.FromResource("Nebula.Resources.AirshipWiringM.png", 100f);

    public static void ModifyEarlier(ShipStatus ship)
    {
        //配線タスク
        ShipModification.ActivateWiring("task_wiresHallway2", 2);
        if (GeneralConfigurations.AirshipArmoryWireOption.CurrentValue) ShipModification.ActivateWiring("task_electricalside2", 3).Room = SystemTypes.Armory;
        ShipModification.ActivateWiring("task_wireShower", 4);
        ShipModification.ActivateWiring("taks_wiresLounge", 5);
        if (GeneralConfigurations.AirshipMedicalWireOption.CurrentValue)
        {
            ShipModification.CreateConsole(SystemTypes.Medical, "task_wireMedical", medicalWiringSprite.GetSprite(), new Vector2(-0.84f, 5.63f));
            ShipModification.ActivateWiring("task_wireMedical", 6).Room = SystemTypes.Medical;
        }
        if (GeneralConfigurations.AirshipHallwayWireOption.CurrentValue) ShipModification.ActivateWiring("panel_wireHallwayL", 7);
        ShipModification.ActivateWiring("task_wiresStorage", 8);
        if (GeneralConfigurations.AirshipVaultWireOption.CurrentValue) ShipModification.ActivateWiring("task_electricalSide", 9).Room = SystemTypes.VaultRoom;
        ShipModification.ActivateWiring("task_wiresMeeting", 10);
    }

    public static void Modify(ShipStatus ship)
    {
        //船体左下がやけに黒い問題を修正
        {
            var exterior = ship.transform.GetChild(23);
            var hull = exterior.GetChild(0);
            var mat = hull.GetChild(9).GetComponent<Renderer>().material;
            var itahull = hull.GetChild(14);
            itahull.GetComponent<Renderer>().material = mat;
            itahull.gameObject.layer = LayerExpansion.GetShipLayer();
        }

        //メインの写真が明るすぎる問題を修正
        {
            var mainHall = ship.FastRooms[SystemTypes.MainHall];
            mainHall.transform.GetChild(2).SetLocalZ(-3f);
        }

        //宿舎下ダウンロード
        ShipModification.EditConsole(SystemTypes.Engine, "panel_data", (c) =>
        {
            c.checkWalls = true;
            c.usableDistance = 0.9f;
        });

        //写真現像タスク
        ShipModification.EditConsole(SystemTypes.MainHall, "task_developphotos", (c) => c.checkWalls = true);

        //シャワータスク
        ShipModification.EditConsole(SystemTypes.Showers, "task_shower", (c) => c.checkWalls = true);

        //ラウンジゴミ箱タスク
        ShipModification.EditConsole(SystemTypes.Lounge, "task_garbage5", (c) => c.checkWalls = true);

        ship.FastRooms[SystemTypes.HallOfPortraits].transform.GetChild(3).gameObject.layer = LayerExpansion.GetShipLayer();

        var ventilation = ship.FastRooms[SystemTypes.Ventilation].transform;
        ventilation.GetChild(5).gameObject.layer = LayerExpansion.GetShortObjectsLayer();
        ventilation.GetChild(6).gameObject.layer = LayerExpansion.GetShortObjectsLayer();

        //メイン暗室
        {
            var obj = ship.FastRooms[SystemTypes.MainHall].gameObject;
            var collider = obj.transform.GetChild(0).GetComponent<BoxCollider2D>();
            var size = collider.size;
            size.y = 4.399f;
            collider.size = size;
        }

        //複数回のタオル拾いタスク
        GameOperatorManager.Instance?.Subscribe<PlayerGetTaskLocalEvent>(ev =>
        {
            if (ev.Task.TaskType == TaskTypes.PickUpTowels)
            {
                foreach (var c in ship.AllConsoles.GetFastEnumerator())
                {
                    if (!c.TaskTypes.Contains(TaskTypes.PickUpTowels)) continue;
                    var ttc = c.CastFast<TowelTaskConsole>();
                    if (!ttc) continue;
                    ttc.Image.color = Color.white;
                }
            }
        }, NebulaAPI.CurrentGame);

        if (GeneralConfigurations.AirshipMeetingVentOption.CurrentValue) ShipModification.CreateVent(SystemTypes.MeetingRoom, "MeetingVent", new Vector2(-3.1f, -1.6f)).transform.localPosition += new Vector3(0, 0, 2);
        if (GeneralConfigurations.AirshipElectricalVentOption.CurrentValue) ShipModification.CreateVent(SystemTypes.Electrical, "ElectricalVent", new Vector2(-0.275f, -1.7f)).transform.localPosition += new Vector3(0, 0, 1);

        if (GeneralConfigurations.AirshipOneWayMeetingRoomOption.CurrentValue) ModifyMeetingRoom();
        if (!GeneralConfigurations.AirshipOneWayMeetingRoomOption.CurrentValue && GeneralConfigurations.PlatformCooldownOption > 0f) ModifyMovingPlatformConsole();

        if (!GeneralConfigurations.AirshipCockpitAdminOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.Cockpit].gameObject;
            GameObject.Destroy(obj.transform.FindChild("cockpit_mapfloating").gameObject);
            GameObject.Destroy(obj.transform.FindChild("panel_cockpit_map").GetComponent<BoxCollider2D>());
        }
        if (!GeneralConfigurations.AirshipRecordAdminOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.Records].gameObject;
            GameObject.Destroy(obj.transform.FindChild("records_admin_map").gameObject);
        }

        if (GeneralConfigurations.AirshipHarderDownloadOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.GapRoom].gameObject;
            var panel = obj.transform.FindChild("panel_data");
            panel.localPosition = new Vector3(4.52f, -3.95f, 0.1f);
        }

        if (GeneralConfigurations.AirshipBetterImpostorVisonOption.CurrentValue || GeneralConfigurations.AirshipShadedLowerFloorOption.CurrentValue)
        {
            var obj = ship.FastRooms[SystemTypes.GapRoom].gameObject;

            var ledgeShadow = obj.transform.FindChild("Shadow").FindChild("LedgeShadow").GetComponent<OneWayShadows>();
            //インポスターについてのみ影を無効化
            if (GeneralConfigurations.AirshipBetterImpostorVisonOption.CurrentValue) ledgeShadow.IgnoreImpostor = true;
            //上下両方から見えないように
            if (GeneralConfigurations.AirshipShadedLowerFloorOption.CurrentValue) ledgeShadow.RoomCollider.enabled = false;
        }

        //エンジンの影を無視したオブジェクトを修正
        ship.FastRooms[SystemTypes.Engine].transform.FindChild("engine_pipewheel").SetLocalZ(-2f);

        //壁のすり抜け対策
        ShipModification.CreateAxeWall("DeckWall", [new(-12.45f, -13.6f), new(6.0f, -13.6f)]);
        ShipModification.CreateAxeWall("Shower-Storage Lower", [new(25.16f, -0.52f), new(25.16f, -3.9f), new(29.66f, -3.9f), new(29.66f, -2.65f)]);
        ShipModification.CreateAxeWall("Shower-Storage Upper", [new(25.16f, 1.5f), new(25.16f, 2.6f), new(29.66f, 2.6f), new(29.66f, -0.42f)]);
        ShipModification.CreateAxeWall("GapUpper", [new(16.41f, 10.23f), new(16.41f, 12.48f), new(5.16f, 12.48f), new(5.16f, 13.7f), new(3.91f, 13.7f), new(3.91f, 12.48f), new(2.85f, 12.48f), new(2.85f, 9.86f)]);
        ShipModification.CreateAxeWall("GapLower", [new(16.41f, 8.36f), new(16.41f, 5.48f), new(2.85f, 5.48f), new(2.85f, 6.11f)]);
        ShipModification.CreateAxeWall("SecurityRight", [new(8.11f, -13.77f), new(-13.77f, -13.31f), new(14.91f, -15.36f)]);

        List<PlainDoor> additionalDoors = [];


        if (GeneralConfigurations.AirshipSecurityDeckDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(false, new(7.09f, -13.8182f, -0.005f), Vector3.one, SystemTypes.Security, 21)); //セキュ展望
        if (GeneralConfigurations.AirshipSecurityKitchenDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(true, new(4.4975f, -12.0855f, 1f), Vector3.one, SystemTypes.Security, 22)); //セキュ左
        if (GeneralConfigurations.AirshipMeetingDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(true, new(14.595f, 15.302f, 0.2f), Vector3.one, SystemTypes.MeetingRoom, 23)); //ミーティング右
        if (GeneralConfigurations.AirshipLoungeDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(true, new(27.79f, 5.572f, 0.2f), new(1.04f, 1f, 1f), SystemTypes.Lounge, 24)); //ラウンジ中
        if (GeneralConfigurations.AirshipLoungeStorageDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(false, new(33.8289f, 4.1328f, 0.09f), Vector3.one, SystemTypes.Lounge, 25)); //ラウンジ下
        if (GeneralConfigurations.AirshipVentilationDoorOption.Value)
        {
            additionalDoors.Add(GenerateExtraDoor(true, new(29.8046f, -1.3482f, 0.2f), Vector3.one, SystemTypes.Ventilation, 26)); //通気口右
            additionalDoors.Add(GenerateExtraDoor(true, new(25.0831f, 0.7126f, 0.09f), Vector3.one, SystemTypes.Ventilation, 27)); //通気口左
        }
        if (GeneralConfigurations.AirshipShowerDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(false, new(24.02f, 1.49f, 0.1f), new(0.97f, 1.05f, 1f), SystemTypes.Showers, 28)); //シャワー内
        if (GeneralConfigurations.AirshipMedicalDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(true, new(26.57f, -5.5645f, 1f), new(1.03f, 1f, 1f), SystemTypes.Medical, 29)); //メディカル内
        if (GeneralConfigurations.AirshipMainHallDoorOption.Value)
        {
            additionalDoors.Add(GenerateExtraDoor(false, new(15.34f, 0.955f, 0.1f), Vector3.one, SystemTypes.MainHall, 30)); //メインホール右上
            additionalDoors.Add(GenerateExtraDoor(false, new(6.21f, -1.46f, 0.1f), Vector3.one, SystemTypes.MainHall, 31)); //メインホール左下
            additionalDoors.Add(GenerateExtraDoor(false, new(6.21f, 0.955f, 0.1f), Vector3.one, SystemTypes.MainHall, 32)); //メインホール左上
            additionalDoors.Add(GenerateExtraDoor(false, new(9.25f, 0.955f, 0.1f), Vector3.one, SystemTypes.MainHall, 33)); //メインホール上(左側)
        }
        if (GeneralConfigurations.AirshipMainElecDoorOption.Value) additionalDoors.Add(GenerateExtraDoor(false, new(12.3f, -1.5f, 0.1f), Vector3.one, SystemTypes.MainHall, 34)); //メインホール右下

        GameOperatorManager.Instance?.Subscribe<MapInstantiateEvent>(ev => {
            List<MapRoom> mapRooms = [];
            if (GeneralConfigurations.AirshipShowerDoorOption.Value) mapRooms.Add(AddDoorSabotageButton("Showers", SystemTypes.Showers, new(1.87f, 0.43f, -1f)));
            if (GeneralConfigurations.AirshipLoungeStorageDoorOption.Value || GeneralConfigurations.AirshipLoungeDoorOption.Value) mapRooms.Add(AddDoorSabotageButton("Lounge", SystemTypes.Lounge, new(2.7f, 0.6f, -1f)));
            if (GeneralConfigurations.AirshipVentilationDoorOption.Value) mapRooms.Add(AddDoorSabotageButton("Ventilation", SystemTypes.Ventilation, new(2.5f, 0f, -1f)));
            if (GeneralConfigurations.AirshipMeetingDoorOption.Value) mapRooms.Add(AddDoorSabotageButton("MeetingRoom", SystemTypes.MeetingRoom, new(1f, 1.8f, -1f)));
            if (GeneralConfigurations.AirshipSecurityDeckDoorOption.Value || GeneralConfigurations.AirshipSecurityKitchenDoorOption.Value) mapRooms.Add(AddDoorSabotageButton("Security", SystemTypes.Security, new(0f, -1.6f, -1f)));
            if (mapRooms.Count > 0) MapBehaviour.Instance.infectedOverlay.rooms = MapBehaviour.Instance.infectedOverlay.rooms.Concat(mapRooms).ToArray();
        }, NebulaAPI.CurrentGame!, 200);

        AddDoors(additionalDoors);

        if (GeneralConfigurations.UseVoiceChatOption)
        {
            CameraVC.Activate((new(-8.24f, 0.0f), 0), (new(-4.06f, 9.43f), 1), (new(16.71f, 10.19f), 2), (new(4.92f, -11.20f), 3), (new(30.29f, -0.49f), 4), (new(3.20f, 16.20f), 5));
        }
    }

    //Vert->左右通行のドア
    static private MapRoom AddDoorSabotageButton(string name, SystemTypes room, Vector3 pos)
    {
        var infectedOverlay = MapBehaviour.Instance.infectedOverlay;
        var orig = infectedOverlay.transform.GetChild(7);
        var obj = GameObject.Instantiate(orig, infectedOverlay.transform);
        obj.name = name;
        var mapRoom = obj.GetComponent<MapRoom>();
        mapRoom.room = room;
        obj.transform.localPosition = pos;
        return mapRoom;
    }

    //Vert->左右通行のドア
    static private PlainDoor GenerateExtraDoor(bool isVert, Vector3 position, Vector3 scale, SystemTypes room, int id)
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        var orig = ship.FastRooms[SystemTypes.Brig].transform.GetChild(isVert ? 5 : 3);
        var door = GameObject.Instantiate(orig, ship.transform).GetComponent<PlainDoor>();
        door.transform.localScale = scale;
        door.transform.position = position;
        door.Room = room;
        door.Id = id;
        return door;
    }

    static private void AddDoors(params IReadOnlyList<OpenableDoor> doors)
    {
        if (doors.Count == 0) return;
        ShipStatus.Instance.AllDoors = new(ShipStatus.Instance.AllDoors.ToArray().Concat(doors).ToArray());
    }

    static private SpriteLoader customMeetingSideSprite = SpriteLoader.FromResource("Nebula.Resources.AirshipCustomMeeting.png", 100f);
    static private SpriteLoader customMeetingLadderSprite = SpriteLoader.FromResource("Nebula.Resources.AirshipCustomMeetingLadder.png", 100f);
    static private void ModifyMeetingRoom()
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        Transform meetingRoom = ship.FastRooms[SystemTypes.MeetingRoom].transform;
        Transform gapRoom = ship.FastRooms[SystemTypes.GapRoom].transform;

        float diffX = (meetingRoom.GetPositionFast().x - gapRoom.transform.GetPositionFast().x) / 0.7f;
        float[] shadowX = new float[2] { 0f, 0f };

        //画像を更新する
        GameObject customRendererObj = new GameObject("meeting_custom");
        var customRendererTransform = customRendererObj.transform;
        customRendererTransform.SetParent(meetingRoom);
        customRendererTransform.localPosition = new Vector3(9.58f, -2.86f, 4.8f);
        customRendererTransform.localScale = new Vector3(1f, 1f, 1f);
        customRendererObj.AddComponent<SpriteRenderer>().sprite = customMeetingSideSprite.GetSprite(); ;
        customRendererObj.layer = LayerExpansion.GetShipLayer();

        //はしごを生成
        GameObject originalLadderObj = meetingRoom.FindChild("ladder_meeting").gameObject;
        GameObject ladderObj = GameObject.Instantiate(originalLadderObj, meetingRoom);
        ladderObj.name = "ladder_meeting_custom";
        ladderObj.transform.position += new Vector3(10.9f, 0);
        ladderObj.GetComponent<SpriteRenderer>().sprite = customMeetingLadderSprite.GetSprite();
        ladderObj.GetComponentsInChildren<Ladder>().Do(l => { l.Id = (byte)(l.Id + 8); ship.Ladders = new Il2CppReferenceArray<Ladder>(ship.Ladders.Append(l).ToArray()); });

        //MeetingRoomの当たり判定に手を加える
        var collider = meetingRoom.FindChild("Walls").GetComponents<EdgeCollider2D>().Where((c) => c.pointCount == 43).FirstOrDefault();
        Il2CppSystem.Collections.Generic.List<Vector2> colliderPosList = new Il2CppSystem.Collections.Generic.List<Vector2>();
        int index = 0;
        float tempX = 0f;
        float tempY = 0f;

        foreach (var p in collider!.points)
        {
            if (index != 30) colliderPosList.Add(p);
            if (index == 29) tempX = p.x;
            if (index == 30)
            {
                tempX = (tempX + p.x) / 2f;
                colliderPosList.Add(new Vector2(tempX, p.y));
                colliderPosList.Add(new Vector2(tempX, -1.8067f));
                colliderPosList.Add(new Vector2(p.x, -1.8067f));
            }
            index++;
        }
        collider.SetPoints(colliderPosList);

        //MeetingRoomの影に手を加える
        collider = meetingRoom.FindChild("Shadows").GetComponents<EdgeCollider2D>().Where((c) => c.pointCount == 46).FirstOrDefault();

        colliderPosList = new Il2CppSystem.Collections.Generic.List<Vector2>();
        index = 0;
        while (index <= 40)
        {
            colliderPosList.Add(collider!.points[index]);
            index++;
        }

        shadowX[0] = collider!.points[41].x;
        shadowX[1] = tempX = (collider.points[40].x + collider.points[41].x) / 2f;
        tempY = (collider.points[40].y + collider.points[41].y) / 2f;
        colliderPosList.Add(new Vector2(tempX, tempY));
        colliderPosList.Add(new Vector2(tempX, tempY - 2.56f));
        var newCollider = meetingRoom.FindChild("Shadows").gameObject.AddComponent<EdgeCollider2D>();
        newCollider.SetPoints(colliderPosList);

        colliderPosList = new Il2CppSystem.Collections.Generic.List<Vector2>();
        index = 41;
        while (index <= 45)
        {
            if (index == 41) colliderPosList.Add(collider.points[41] - new Vector2(0, 2.56f));
            colliderPosList.Add(collider.points[index]);
            index++;
        }
        tempX = collider.points[41].x;
        collider.SetPoints(colliderPosList);

        //GapRoomの影に手を加える
        collider = gapRoom.FindChild("Shadow").GetComponents<EdgeCollider2D>().Where(x => Math.Abs(x.points[0].x + 6.2984f) < 0.1).FirstOrDefault();
        colliderPosList = new Il2CppSystem.Collections.Generic.List<Vector2>();
        index = 0;
        while (index <= 1)
        {
            colliderPosList.Add(collider!.points[index]);
            index++;
        }
        colliderPosList.Add(new Vector2(shadowX[0] + diffX, collider!.points[1].y));
        newCollider = gapRoom.FindChild("Shadow").gameObject.AddComponent<EdgeCollider2D>();
        newCollider.SetPoints(colliderPosList);
        colliderPosList = new Il2CppSystem.Collections.Generic.List<Vector2>();
        index = 2;
        colliderPosList.Add(new Vector2(shadowX[1] + diffX, collider.points[1].y));
        while (index <= 4)
        {
            colliderPosList.Add(collider.points[index]);
            index++;
        }
        collider.SetPoints(colliderPosList);

        AirshipStatus airship = ship.Cast<AirshipStatus>();
        airship.Ladders = new Il2CppReferenceArray<Ladder>(airship.GetComponentsInChildren<Ladder>());

        originalLadderObj.transform.GetChild(0).gameObject.SetActive(false);
        originalLadderObj.transform.GetChild(1).gameObject.SetActive(false);
        ladderObj.transform.GetChild(2).gameObject.SetActive(false);
        ladderObj.transform.GetChild(3).gameObject.SetActive(false);


        //MovingPlatformを無効化する
        airship.GapPlatform.SetSide(true);
        airship.outOfOrderPlat.SetActive(true);
        airship.GapPlatform.transform.localPosition = airship.GapPlatform.DisabledPosition;
    }

    static private void ModifyMovingPlatformConsole()
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;
        var gapRoom = ship.FastRooms[SystemTypes.GapRoom];
        var consoles = gapRoom.gameObject.GetComponentsInChildren<PlatformConsole>();
        List<AirshipPlatformConsoleWithCooldown> modifiedConsoles = [];
        foreach (var console in consoles)
        {
            var modified = console.gameObject.AddComponent<AirshipPlatformConsoleWithCooldown>();
            modified.SetUp(console);
            GameObject.Destroy(console);
            modifiedConsoles.Add(modified);
        }
        GameOperatorManager.Instance.Subscribe<PlayerUseMovingPlatformEvent>(ev =>
        {
            if (!ev.Player.AmOwner) return;
            foreach (var console in modifiedConsoles) console.CoolDown = console.MaxCoolDown;
        }, NebulaAPI.CurrentGame!);
    }
}
