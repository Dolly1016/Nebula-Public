using BepInEx.Unity.IL2CPP;
using Cpp2IL.Core.Extensions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Behavior;
using UnityEngine;
using Virial;
using Virial.Events.Game;
using Virial.Events.Game.Minimap;

namespace Nebula.Extensions;

public static class ShipCache
{
    static private ShipStatus lastChecked = null!;
    static private MapConsole[] mapConsoles = [];
    static private void SearchAll()
    {
        if (lastChecked) return;
        lastChecked = ShipStatus.Instance;
        if (!lastChecked) return;

        mapConsoles = lastChecked.GetComponentsInChildren<MapConsole>();
    }

    static public MapConsole[] MapConsoles { get
        {
            SearchAll();
            return mapConsoles;
        } 
    }
}

public static class ShipExtension
{


    public static void PatchModification(byte mapId)
    {
        switch (mapId)
        {
            case 0:
                ModifySkeld();
                break;
            case 1:
                ModifyMira();
                break;
            case 2:
                ModifyPolus();
                break;
            case 4:
                ModifyAirship();
                break;
            case 5:
                ModifyFungle();
                break;
        }
    }

    public static void PatchEarlierModification(byte mapId)
    {
        switch (mapId)
        {
            case 0:
                ModifyEarlierSkeld();
                break;
            case 1:
                ModifyEarierMira();
                break;
            case 2:
                ModifyEarlierPolus();
                break;
            case 4:
                ModifyEarlierAirship();
                break;
            case 5:
                ModifyEarlierFungle();
                break;
        }
    }

    private static void ModifyEarlierSkeld() { }
    private static void ModifySkeld()
    {
        if (GeneralConfigurations.SkeldCafeVentOption.CurrentValue) CreateVent(SystemTypes.Cafeteria, "CafeUpperVent", new UnityEngine.Vector2(-2.1f, 3.8f));
        if (GeneralConfigurations.SkeldStorageVentOption.CurrentValue) CreateVent(SystemTypes.Storage, "StorageVent", new UnityEngine.Vector2(0.45f, -3.6f));

        if (!GeneralConfigurations.SkeldAdminOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.Admin].gameObject.transform.parent.GetChild(0).GetChild(3).gameObject;
            GameObject.Destroy(obj.transform.GetChild(1).GetComponent<CircleCollider2D>());
        }

        ShipStatus.Instance.Systems[SystemTypes.LifeSupp].Cast<LifeSuppSystemType>().LifeSuppDuration = GeneralConfigurations.SkeldO2DurationOption.CurrentValue;
        ShipStatus.Instance.Systems[SystemTypes.Reactor].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.SkeldReactorDurationOption.CurrentValue;

        for (int i = 0; i < ShipStatus.Instance.AllDoors.Count; i++) ShipStatus.Instance.AllDoors[i].Id = i;
    }

    private static void ModifyEarierMira() { }
    private static void ModifyMira()
    {
        if (!GeneralConfigurations.MiraAdminOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.Admin].gameObject.transform.FindChild("MapTable").gameObject;
            GameObject.Destroy(obj.transform.GetChild(0).gameObject);
        }

        ShipStatus.Instance.Systems[SystemTypes.LifeSupp].Cast<LifeSuppSystemType>().LifeSuppDuration = GeneralConfigurations.MiraO2DurationOption.CurrentValue;
        ShipStatus.Instance.Systems[SystemTypes.Reactor].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.MiraReactorDurationOption.CurrentValue;
    }

    private static void ModifyEarlierPolus() { }
    private static void ModifyPolus()
    {
        var commRoom = ShipStatus.Instance.FastRooms[SystemTypes.Comms];
        var commPos = commRoom.transform.localPosition;
        commPos.z = 0.0001f;
        commRoom.transform.localPosition = commPos;

        if (GeneralConfigurations.PolusSpecimenVentOption.CurrentValue) CreateVent(SystemTypes.Specimens, "SpecimenVent", new UnityEngine.Vector2(-1f, -1.35f));

        if (!GeneralConfigurations.PolusAdminOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.Admin].gameObject.transform.FindChild("mapTable").gameObject;
            GameObject.Destroy(obj.transform.GetChild(0).GetComponent<BoxCollider2D>());
            GameObject.Destroy(obj.transform.GetChild(1).GetComponent<BoxCollider2D>());
            GameObject.Destroy(obj.transform.GetChild(2).gameObject);
        }

        ShipStatus.Instance.Systems[SystemTypes.Laboratory].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.PolusReactorDurationOption.CurrentValue;

        //崩壊したオブジェクトの前後関係を修正
        ShipStatus.Instance.FastRooms[SystemTypes.Electrical].transform.FindChild("fencebot").SetLocalZ(-1.0136f);
    }

    private static SpriteLoader medicalWiringSprite = SpriteLoader.FromResource("Nebula.Resources.AirshipWiringM.png",100f);

    private static void ModifyEarlierAirship() {
        //配線タスク
        ActivateWiring("task_wiresHallway2", 2);
        if (GeneralConfigurations.AirshipArmoryWireOption.CurrentValue) ActivateWiring("task_electricalside2", 3).Room = SystemTypes.Armory;
        ActivateWiring("task_wireShower", 4);
        ActivateWiring("taks_wiresLounge", 5);
        if (GeneralConfigurations.AirshipMedicalWireOption.CurrentValue)
        {
            CreateConsole(SystemTypes.Medical, "task_wireMedical", medicalWiringSprite.GetSprite(), new Vector2(-0.84f, 5.63f), 0f);
            ActivateWiring("task_wireMedical", 6).Room = SystemTypes.Medical;
        }
        if (GeneralConfigurations.AirshipHallwayWireOption.CurrentValue) ActivateWiring("panel_wireHallwayL", 7);
        ActivateWiring("task_wiresStorage", 8);
        if (GeneralConfigurations.AirshipVaultWireOption.CurrentValue) ActivateWiring("task_electricalSide", 9).Room = SystemTypes.VaultRoom;
        ActivateWiring("task_wiresMeeting", 10);
    }
    private static void ModifyAirship()
    {
        //宿舎下ダウンロード
        EditConsole(SystemTypes.Engine, "panel_data", (c) =>
        {
            c.checkWalls = true;
            c.usableDistance = 0.9f;
        });

        //写真現像タスク
        EditConsole(SystemTypes.MainHall, "task_developphotos", (c) => c.checkWalls = true);

        //シャワータスク
        EditConsole(SystemTypes.Showers, "task_shower", (c) => c.checkWalls = true);

        //ラウンジゴミ箱タスク
        EditConsole(SystemTypes.Lounge, "task_garbage5", (c) => c.checkWalls = true);

        ShipStatus.Instance.FastRooms[SystemTypes.HallOfPortraits].transform.GetChild(3).gameObject.layer = LayerExpansion.GetShipLayer();

        var ventilation = ShipStatus.Instance.FastRooms[SystemTypes.Ventilation].transform;
        ventilation.GetChild(5).gameObject.layer = LayerExpansion.GetShortObjectsLayer();
        ventilation.GetChild(6).gameObject.layer = LayerExpansion.GetShortObjectsLayer();

        //メイン暗室
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.MainHall].gameObject;
            var collider = obj.transform.GetChild(0).GetComponent<BoxCollider2D>();
            var size = collider.size;
            size.y = 4.399f;
            collider.size = size;
        }

        if (GeneralConfigurations.AirshipMeetingVentOption.CurrentValue) CreateVent(SystemTypes.MeetingRoom, "MeetingVent", new Vector2(-3.1f, -1.6f)).transform.localPosition += new Vector3(0, 0, 2);
        if (GeneralConfigurations.AirshipElectricalVentOption.CurrentValue) CreateVent(SystemTypes.Electrical, "ElectricalVent", new Vector2(-0.275f, -1.7f)).transform.localPosition += new Vector3(0, 0, 1);

        if (GeneralConfigurations.AirshipOneWayMeetingRoomOption.CurrentValue) ModifyMeetingRoom();

        if (!GeneralConfigurations.AirshipCockpitAdminOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.Cockpit].gameObject;
            GameObject.Destroy(obj.transform.FindChild("cockpit_mapfloating").gameObject);
            GameObject.Destroy(obj.transform.FindChild("panel_cockpit_map").GetComponent<BoxCollider2D>());
        }
        if (!GeneralConfigurations.AirshipRecordAdminOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.Records].gameObject;
            GameObject.Destroy(obj.transform.FindChild("records_admin_map").gameObject);
        }

        if (GeneralConfigurations.AirshipHarderDownloadOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.GapRoom].gameObject;
            var panel = obj.transform.FindChild("panel_data");
            panel.localPosition = new Vector3(4.52f, -3.95f, 0.1f);
        }

        if (GeneralConfigurations.AirshipBetterImpostorVisonOption.CurrentValue || GeneralConfigurations.AirshipShadedLowerFloorOption.CurrentValue)
        {
            var obj = ShipStatus.Instance.FastRooms[SystemTypes.GapRoom].gameObject;

            var ledgeShadow = obj.transform.FindChild("Shadow").FindChild("LedgeShadow").GetComponent<OneWayShadows>();
            //インポスターについてのみ影を無効化
            if(GeneralConfigurations.AirshipBetterImpostorVisonOption.CurrentValue)  ledgeShadow.IgnoreImpostor = true;
            //上下両方から見えないように
            if (GeneralConfigurations.AirshipShadedLowerFloorOption.CurrentValue) ledgeShadow.RoomCollider.enabled = false;
        }

        //エンジンの影を無視したオブジェクトを修正
        ShipStatus.Instance.FastRooms[SystemTypes.Engine].transform.FindChild("engine_pipewheel").SetLocalZ(-2f);

        //Raiderの斧対策で展望間に壁設置
        {
            var collider = UnityHelper.CreateObject<EdgeCollider2D>("DeckWall", null, Vector3.zero, LayerExpansion.GetShipLayer());
            collider.SetPoints(((Vector2[])[new(-12.45f, -13.6f), new(6.0f, -13.6f)]).ToIl2CppList());
        }

        List<PlainDoor> additionalDoors = [];
        
        
        if(GeneralConfigurations.AirshipSecurityDeckDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(false, new(7.09f, -13.8182f, -0.005f), Vector3.one, SystemTypes.Security, 21)); //セキュ展望
        if (GeneralConfigurations.AirshipSecurityKitchenDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(true, new(4.4975f, -12.0855f, 1f), Vector3.one, SystemTypes.Security, 22)); //セキュ左
        if (GeneralConfigurations.AirshipMeetingDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(true, new(14.595f, 15.302f, 0.2f), Vector3.one, SystemTypes.MeetingRoom, 23)); //ミーティング右
        if (GeneralConfigurations.AirshipLoungeDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(true, new(27.79f, 5.572f, 0.2f), new(1.04f, 1f, 1f), SystemTypes.Lounge, 24)); //ラウンジ中
        if (GeneralConfigurations.AirshipLoungeStorageDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(false, new(33.8289f, 4.1328f, 0.09f), Vector3.one, SystemTypes.Lounge, 25)); //ラウンジ下
        if (GeneralConfigurations.AirshipVentilationDoorOption.Value)
        {
            additionalDoors.Add(GenerateExtraDoorInAirship(true, new(29.8046f, -1.3482f, 0.2f), Vector3.one, SystemTypes.Ventilation, 26)); //通気口右
            additionalDoors.Add(GenerateExtraDoorInAirship(true, new(25.0831f, 0.7126f, 0.09f), Vector3.one, SystemTypes.Ventilation, 27)); //通気口左
        }
        if (GeneralConfigurations.AirshipShowerDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(false, new(24.02f, 1.49f, 0.1f), new(0.97f, 1.05f, 1f), SystemTypes.Showers, 28)); //シャワー内
        if (GeneralConfigurations.AirshipMedicalDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(true, new(26.57f, -5.5645f, 1f), new(1.03f, 1f, 1f), SystemTypes.Medical, 29)); //メディカル内
        if (GeneralConfigurations.AirshipMainHallDoorOption.Value)
        {
            additionalDoors.Add(GenerateExtraDoorInAirship(false, new(15.34f, 0.955f, 0.1f), Vector3.one, SystemTypes.MainHall, 30)); //メインホール右上
            additionalDoors.Add(GenerateExtraDoorInAirship(false, new(6.21f, -1.46f, 0.1f), Vector3.one, SystemTypes.MainHall, 31)); //メインホール左下
            additionalDoors.Add(GenerateExtraDoorInAirship(false, new(6.21f, 0.955f, 0.1f), Vector3.one, SystemTypes.MainHall, 32)); //メインホール左上
            additionalDoors.Add(GenerateExtraDoorInAirship(false, new(9.25f, 0.955f, 0.1f), Vector3.one, SystemTypes.MainHall, 33)); //メインホール上(左側)
        }
        if (GeneralConfigurations.AirshipMainElecDoorOption.Value) additionalDoors.Add(GenerateExtraDoorInAirship(false, new(12.3f, -1.5f, 0.1f), Vector3.one, SystemTypes.MainHall, 34)); //メインホール右下

        GameOperatorManager.Instance?.Subscribe<MapInstantiateEvent>(ev => {
            List<MapRoom> mapRooms = [];
            if (GeneralConfigurations.AirshipShowerDoorOption.Value) mapRooms.Add(AddDoorSabotageButtonInAirship("Showers", SystemTypes.Showers, new(1.87f, 0.43f, -1f)));
            if (GeneralConfigurations.AirshipLoungeStorageDoorOption.Value || GeneralConfigurations.AirshipLoungeDoorOption.Value) mapRooms.Add(AddDoorSabotageButtonInAirship("Lounge", SystemTypes.Lounge, new(2.7f, 0.6f, -1f)));
            if (GeneralConfigurations.AirshipVentilationDoorOption.Value) mapRooms.Add(AddDoorSabotageButtonInAirship("Ventilation", SystemTypes.Ventilation, new(2.5f, 0f, -1f)));
            if (GeneralConfigurations.AirshipMeetingDoorOption.Value) mapRooms.Add(AddDoorSabotageButtonInAirship("MeetingRoom", SystemTypes.MeetingRoom, new(1f, 1.8f, -1f)));
            if (GeneralConfigurations.AirshipSecurityDeckDoorOption.Value || GeneralConfigurations.AirshipSecurityKitchenDoorOption.Value) mapRooms.Add(AddDoorSabotageButtonInAirship("Security", SystemTypes.Security, new(0f, -1.6f, -1f)));
            if(mapRooms.Count > 0) MapBehaviour.Instance.infectedOverlay.rooms = MapBehaviour.Instance.infectedOverlay.rooms.Concat(mapRooms).ToArray();
        }, NebulaAPI.CurrentGame!, 200);

        AddDoors(additionalDoors);
    }

    //Vert->左右通行のドア
    static private MapRoom AddDoorSabotageButtonInAirship(string name, SystemTypes room, Vector3 pos)
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
    static private PlainDoor GenerateExtraDoorInAirship(bool isVert, Vector3 position, Vector3 scale, SystemTypes room, int id)
    {
        var orig = ShipStatus.Instance.FastRooms[SystemTypes.Brig].transform.GetChild(isVert ? 5 : 3);
        var door = GameObject.Instantiate(orig, ShipStatus.Instance.transform).GetComponent<PlainDoor>();
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

    private static SpriteLoader fungleLight1Sprite = SpriteLoader.FromResource("Nebula.Resources.FungleLightConsole1.png", 100f);
    private static SpriteLoader fungleLight2Sprite = SpriteLoader.FromResource("Nebula.Resources.FungleLightConsole2.png", 100f);
    private static SpriteLoader fungleLight3Sprite = SpriteLoader.FromResource("Nebula.Resources.FungleLightConsole3.png", 100f);

    private static void ModifyEarlierFungle() {
        int lightConsoleId = 0;
        Console SetUpAsLightConsole(Console console)
        {
            console.AllowImpostor = true;
            console.GhostsIgnored = true;
            console.TaskTypes = new TaskTypes[] { TaskTypes.FixLights };
            console.ConsoleId = lightConsoleId++;
            return console;
        }
        if (GeneralConfigurations.FungleLightJungleOption.Value) SetUpAsLightConsole(CreateConsole(SystemTypes.Electrical, "lightJungle", fungleLight1Sprite.GetSprite(), new(-6.9618f, -5.9982f), 0f));
        if(GeneralConfigurations.FungleLightDropshipOption.Value) SetUpAsLightConsole(CreateConsole(SystemTypes.Electrical, "lightDropship", fungleLight2Sprite.GetSprite(), new(-7.8f, 13.46f), 0f));
        if (GeneralConfigurations.FungleLightMiningPitOption.Value)
        {
            var miningConsole = SetUpAsLightConsole(CreateConsole(SystemTypes.Electrical, "lightMining", fungleLight3Sprite.GetSprite(), new(13.68f, 7.83f), 0f));
            UnityHelper.CreateObject<BoxCollider2D>("Collider", miningConsole.transform, new(0f, 0f, 0f)).size = new(0.6f, 0.3f);
        }
    }

    private static bool FungleHasLightSabotage => GeneralConfigurations.FungleLightJungleOption.Value || GeneralConfigurations.FungleLightDropshipOption.Value || GeneralConfigurations.FungleLightMiningPitOption.Value;
    private static void ModifyFungle()
    {
        //しばらくの措置として見た目チェンジサボ廃止
        ShipStatus.Instance.MapPrefab.infectedOverlay.transform.GetChild(6).GetChild(0).gameObject.SetActive(false);

        //停電サボタージュ
        if(FungleHasLightSabotage)
        {
            GameOperatorManager.Instance!.Subscribe<MapInstantiateEvent>(ev =>
            {
                var infected = MapBehaviour.Instance.infectedOverlay;
                var lightOut = GameObject.Instantiate(VanillaAsset.MapAsset[0].MapPrefab.infectedOverlay.transform.GetChild(3).GetChild(1).gameObject, infected.transform);
                lightOut.transform.localPosition = new(-1.2788f, 1.5801f, -2f);
                lightOut.transform.localScale = new(0.8f, 0.8f, 1f);
                var renderer = lightOut.GetComponent<SpriteRenderer>();
                renderer.SetCooldownNormalizedUvs();
                var button = lightOut.GetComponent<ButtonBehavior>();
                button.OnClick = new();
                button.OnClick.AddListener(() =>
                {
                    if (!infected.CanUseSabotage) return;
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Sabotage, (byte)SystemTypes.Electrical);
                });
                var allButtons = infected.allButtons.ToList();
                allButtons.Add(button);
                infected.allButtons = allButtons.ToArray();

                GameOperatorManager.Instance.Subscribe<GameHudUpdateEvent>(ev =>
                {
                    if (infected.sabSystem != null)
                    {
                        var perc = infected.DoorsPreventingSabotage ? 1f : infected.sabSystem.PercentCool;
                        if (renderer) renderer.material.SetFloat("_Percent", perc);
                    }

                }, new GameObjectLifespan(MapBehaviour.Instance.gameObject));
            }, Virial.NebulaAPI.CurrentGame!);
            var switchSystem = new SwitchSystem();
            ShipStatus.Instance.Systems[SystemTypes.Electrical] = switchSystem.CastFast<ISystemType>();
            ShipStatus.Instance.Systems[SystemTypes.Sabotage].CastFast<SabotageSystemType>().specials.Add(switchSystem.CastFast<IActivatable>());
            var specialTasks = ShipStatus.Instance.SpecialTasks.ToList();
            specialTasks.Add(VanillaAsset.MapAsset[0].SpecialTasks[1]);
            ShipStatus.Instance.SpecialTasks = specialTasks.ToArray();
        }

        if (GeneralConfigurations.FungleSimpleLaboratoryOption.CurrentValue) ModifyLaboratory();

        if (GeneralConfigurations.FungleThinFogOption.CurrentValue)
        {
            var renderer = ShipStatus.Instance.transform.FindChild("FungleJungleShadow").GetChild(0).GetComponent<MeshRenderer>();
            var color = renderer.material.color;
            color.a = 0.35f;
            renderer.material.color = color;
        }
        if (GeneralConfigurations.FungleGlowingCampfireOption.CurrentValue)
        {
            var light = AmongUsUtil.GenerateCustomLight(new(-9.8f, 1.65f));
            var script = light.gameObject.AddComponent<ScriptBehaviour>();
            float t = 0f;
            script.UpdateHandler += () =>
            {
                t -= Time.deltaTime;
                if (t < 0f)
                {
                    light.transform.localScale = Vector3.one * (1.3f + (float)System.Random.Shared.NextDouble() * 0.05f);
                    t = 0.08f;
                }
            };
        }
        if (GeneralConfigurations.FungleGlowingMushroomOption.CurrentValue)
        {
            void SetUpGlowingMush(GameObject obj)
            {
                var light = AmongUsUtil.GenerateCustomLight(new(0, 0));
                light.transform.SetParent(obj.transform);
                light.material.SetColor("_Color", Color.white.AlphaMultiplied(0.35f));
                light.transform.localScale = Vector3.one * 1.9f;
                light.transform.localPosition = new Vector3(0f, 0f, -10f);
            }
            ShipStatus.Instance.transform.FindChild("Outside").GetChild(0).GetChild(5).gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)SetUpGlowingMush);
            SetUpGlowingMush(ShipStatus.Instance.FastRooms[SystemTypes.Reactor].transform.FindChild("GlowingMushroom").gameObject);
        }

        ShipStatus.Instance.Systems[SystemTypes.Reactor].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.FungleReactorDurationOption.CurrentValue;

        if (ShipStatus.Instance.AllVents.Find(v => v.gameObject.name == "NorthWestJungleVent", out var vent)) vent.transform.SetLocalZ(-0.2f);

        //Storageの見た目がおかしい問題を修正
        {
            var storageBarrels = ShipStatus.Instance.FastRooms[SystemTypes.Storage].transform.GetChild(0).GetChild(3);
            var localPos = storageBarrels.localPosition;
            localPos.z = -1.095f;
            storageBarrels.localPosition = localPos;
        }

        //Storageの影を調整
        {
            var outsideShadows = ShipStatus.Instance.transform.TryDig("Outside", "OutsideHighlands", "Shadows");
            outsideShadows?.TryDig("OnewayShadow-Top")?.gameObject.SetActive(false);
            outsideShadows?.TryDig("OnewayShadow-Top+Ledge")?.gameObject.SetActive(false);
            var collider = ShipStatus.Instance.FastRooms[SystemTypes.Storage].transform.GetChild(1).GetComponent<EdgeCollider2D>();
            Vector2[] points = collider.points;
            var subArray1 = points.SubArray(0, 5);
            var subArray2 = points.SubArray(20, 27);

            collider.SetPoints(subArray1.ToIl2CppList());
            var newCollider = collider.gameObject.AddComponent<EdgeCollider2D>();
            newCollider.isTrigger = false;
            newCollider.SetPoints(subArray2.ToIl2CppList());

            //BepInEx.ConsoleManager.StandardOutStream.WriteLine("Colliders: " + collider.pointCount);
        }
    }







   
    private static Vent CreateVent(SystemTypes room, string ventName, Vector2 position)
    {
        var referenceVent = ShipStatus.Instance.AllVents[0];
        Vent vent = UnityEngine.Object.Instantiate<Vent>(referenceVent, ShipStatus.Instance.FastRooms[room].transform);
        vent.transform.localPosition = new Vector3(position.x, position.y, -1);
        vent.Left = null;
        vent.Right = null;
        vent.Center = null;
        vent.Id = ShipStatus.Instance.AllVents.Select(x => x.Id).Max() + 1; // Make sure we have a unique id

        var allVentsList = ShipStatus.Instance.AllVents.ToList();
        allVentsList.Add(vent);
        ShipStatus.Instance.AllVents = allVentsList.ToArray();

        vent.gameObject.SetActive(true);
        vent.name = ventName;
        vent.gameObject.name = ventName;
        var console = vent.GetComponent<VentCleaningConsole>();
        console.Room = room;
        console.ConsoleId = ShipStatus.Instance.AllVents.Length;

        var allConsolesList = ShipStatus.Instance.AllConsoles.ToList();
        allConsolesList.Add(console);
        ShipStatus.Instance.AllConsoles = allConsolesList.ToArray();

        return vent;
    }

    private static void EditConsole(SystemTypes room, string objectName, Action<Console> action)
    {
        if (!ShipStatus.Instance.FastRooms.ContainsKey(room)) return;
        PlainShipRoom shipRoom = ShipStatus.Instance.FastRooms[room];
        Transform transform = shipRoom.transform.FindChild(objectName);
        if (!transform) return;
        GameObject obj = transform.gameObject;
        if (!obj) return;

        Console c = obj.GetComponent<Console>();
        if (c) action.Invoke(c);
    }

    private static Console ActivateWiring(string consoleName, int consoleId)
    {
        Console console = ActivateConsole(consoleName);

        if (!console.TaskTypes.Contains(TaskTypes.FixWiring))
        {
            var list = console.TaskTypes.ToList();
            list.Add(TaskTypes.FixWiring);
            console.TaskTypes = list.ToArray();
        }
        console.ConsoleId = consoleId;
        return console;
    }

    private static Console CreateConsole(SystemTypes room, string objectName, Sprite sprite, Vector2 pos, float z)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(ShipStatus.Instance.FastRooms.TryGetValue(room, out var roomObj) ? roomObj.transform : ShipStatus.Instance.transform);
        obj.transform.localPosition = (Vector3)pos - new Vector3(0, 0, z);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;

        Console c = Consolize<Console>(obj);
        c.Room = room;
        c.Image = renderer;
        return c;
    }

    private static Console ActivateConsole(string objectName)
    {
        GameObject obj = UnityEngine.GameObject.Find(objectName);
        return Consolize<Console>(obj);
    }

    private static Material? highlightMaterial = null;
    private static Material GetHighlightMaterial()
    {
        if (highlightMaterial != null) return new Material(highlightMaterial);
        foreach (var mat in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
        {
            if (mat.name == "HighlightMat")
            {
                highlightMaterial = mat.TryCast<Material>();
                break;
            }
        }
        return new Material(highlightMaterial);
    }

    private static Console Consolize<C>(GameObject obj, SpriteRenderer? renderer = null) where C : Console
    {
        obj.layer = LayerMask.NameToLayer("ShortObjects");
        Console console = obj.GetComponent<Console>();
        PassiveButton button = obj.GetComponent<PassiveButton>();
        Collider2D collider = obj.GetComponent<Collider2D>();
        if (!console)
        {
            console = obj.AddComponent<C>();
            console.checkWalls = true;
            console.usableDistance = 0.7f;
            console.TaskTypes = new TaskTypes[0];
            console.ValidTasks = new Il2CppReferenceArray<TaskSet>(0);
            var list = ShipStatus.Instance.AllConsoles.ToList();
            list.Add(console);
            ShipStatus.Instance.AllConsoles = new Il2CppReferenceArray<Console>(list.ToArray());
        }
        if (console.Image == null)
        {
            if (renderer != null)
            {
                console.Image = renderer;
            }
            else
            {
                console.Image = obj.GetComponent<SpriteRenderer>();
                console.Image.material = GetHighlightMaterial();
            }
        }
        if (!button)
        {
            button = obj.AddComponent<PassiveButton>();
            button.OnMouseOut = new UnityEngine.Events.UnityEvent();
            button.OnMouseOver = new UnityEngine.Events.UnityEvent();
            button._CachedZ_k__BackingField = 0.1f;
            button.CachedZ = 0.1f;
        }

        if (!collider)
        {
            var cCollider = obj.AddComponent<CircleCollider2D>();
            cCollider.radius = 0.4f;
            cCollider.isTrigger = true;
        }

        return console;
    }

    static private SpriteLoader customMeetingSideSprite = SpriteLoader.FromResource("Nebula.Resources.AirshipCustomMeeting.png",100f);
    static private SpriteLoader customMeetingLadderSprite = SpriteLoader.FromResource("Nebula.Resources.AirshipCustomMeetingLadder.png", 100f);
    static private void ModifyMeetingRoom()
    {
        Transform meetingRoom = ShipStatus.Instance.FastRooms[SystemTypes.MeetingRoom].transform;
        Transform gapRoom = ShipStatus.Instance.FastRooms[SystemTypes.GapRoom].transform;

        float diffX = (meetingRoom.position.x - gapRoom.transform.position.x) / 0.7f;
        float[] shadowX = new float[2] { 0f, 0f };

        //画像を更新する
        GameObject customRendererObj = new GameObject("meeting_custom");
        customRendererObj.transform.SetParent(meetingRoom);
        customRendererObj.transform.localPosition = new Vector3(9.58f, -2.86f, 4.8f);
        customRendererObj.transform.localScale = new Vector3(1f, 1f, 1f);
        customRendererObj.AddComponent<SpriteRenderer>().sprite = customMeetingSideSprite.GetSprite(); ;
        customRendererObj.layer = LayerExpansion.GetShipLayer();

        //はしごを生成
        GameObject originalLadderObj = meetingRoom.FindChild("ladder_meeting").gameObject;
        GameObject ladderObj = GameObject.Instantiate(meetingRoom.FindChild("ladder_meeting").gameObject, meetingRoom);
        ladderObj.name = "ladder_meeting_custom";
        ladderObj.transform.position += new Vector3(10.9f, 0);
        ladderObj.GetComponent<SpriteRenderer>().sprite = customMeetingLadderSprite.GetSprite();
        ladderObj.GetComponentsInChildren<Ladder>().Do(l => { l.Id = (byte)(l.Id + 8); ShipStatus.Instance.Ladders = new Il2CppReferenceArray<Ladder>(ShipStatus.Instance.Ladders.Append(l).ToArray()); }) ;

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

        AirshipStatus airship = ShipStatus.Instance.Cast<AirshipStatus>();
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

    static private SpriteLoader customLaboratorySprite = SpriteLoader.FromResource("Nebula.Resources.FungleCustomLaboratory.png", 100f);
    static private SpriteLoader customLaboratoryWallSprite = SpriteLoader.FromResource("Nebula.Resources.FungleCustomLaboratoryWall.png", 100f);
    static private void ModifyLaboratory()
    {
        Transform laboratory = ShipStatus.Instance.FastRooms[SystemTypes.Laboratory].transform;

        //画像を更新する
        laboratory.GetChild(0).GetChild(0).GetChild(0).GetComponent<SpriteRenderer>().sprite = customLaboratorySprite.GetSprite();
        laboratory.GetChild(0).GetChild(2).GetChild(1).GetComponent<SpriteRenderer>().sprite = customLaboratoryWallSprite.GetSprite();

        //壁を編集する
        var collider = laboratory.GetChild(2).gameObject.GetComponent<PolygonCollider2D>();
        var newCollider = laboratory.GetChild(2).gameObject.AddComponent<PolygonCollider2D>();

        var points = collider.points.ToArray();
        var polygon1 = new List<Vector2>();
        polygon1.Add(new(0.45f, -1.71f));
        polygon1.AddRange(points.Skip(17).Take(14));
        polygon1.Add(new(0.45f, -1.89f));

        var polygon2 = new List<Vector2>();
        polygon2.AddRange(points.Take(16));
        polygon2.Add(new(1.2f, -1.71f));
        polygon2.Add(new(1.2f, -1.89f));
        polygon2.AddRange(points.Skip(31).Take(3));

        Il2CppSystem.Collections.Generic.List<Vector2> ToList(Vector2[] array)
        {
            Il2CppSystem.Collections.Generic.List<Vector2> list = new(array.Length);
            foreach (var el in array) list.Add(el);
            return list;
        }

        collider.SetPath(0, ToList(polygon1.ToArray()));
        newCollider.SetPath(0, ToList(polygon2.ToArray()));

        //ラボ上部にサンプル採取タスクを追加
        GameObject collectSampleConsole = ShipStatus.Instance.transform.GetChild(5).GetChild(0).GetChild(2).GetChild(0).gameObject;
        var labSample = GameObject.Instantiate(collectSampleConsole, collectSampleConsole.transform.parent);
        labSample.name = "CollectSamples (Lab)";
        labSample.transform.GetChild(0).GetComponent<Console>().ConsoleId = 8;
        labSample.transform.localPosition = new(-8.393f, -2.4575f, 1f);
    }
}
