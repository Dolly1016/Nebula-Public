using Cpp2IL.Core.Extensions;
using Nebula.Behavior;
using Nebula.Roles.Complex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game;
using Virial.Events.Game.Minimap;
using Virial.Game;

namespace Nebula.Modifications.Map;

static internal class FungleModification
{
    private static SpriteLoader fungleLight1Sprite = SpriteLoader.FromResource("Nebula.Resources.FungleLightConsole1.png", 100f);
    private static SpriteLoader fungleLight2Sprite = SpriteLoader.FromResource("Nebula.Resources.FungleLightConsole2.png", 100f);
    private static SpriteLoader fungleLight3Sprite = SpriteLoader.FromResource("Nebula.Resources.FungleLightConsole3.png", 100f);


    public static void ModifyEarliest(ShipStatus ship)
    {
        int fungleVariation = GeneralConfigurations.FungleVariation;
        if (fungleVariation == 1) FungleMidnight.ModifyEarliest(ship);
        if (fungleVariation == 2) FunglePoisoned.ModifyEarliest(ship);
    }

    public static void ModifyEarlier(ShipStatus ship)
    {
        SetUpLightOutConsolesIfNeccesary();

        int fungleVariation = GeneralConfigurations.FungleVariation;
        if (fungleVariation == 1) FungleMidnight.ModifyEarlier(ship);
        if (fungleVariation == 2) FunglePoisoned.ModifyEarlier(ship);
    }

    private static bool ShouldAddJungleLightConsole() => GeneralConfigurations.FungleLightJungleOption.Value;
    private static bool ShouldAddDropshipLightConsole(int variation) => GeneralConfigurations.FungleLightDropshipOption.Value && variation != 2;
    private static bool ShouldAddMiningPitLightConsole(int variation) => GeneralConfigurations.FungleLightMiningPitOption.Value && variation != 1;
    private static bool FungleHasLightSabotage(int variation) => ShouldAddJungleLightConsole() || ShouldAddDropshipLightConsole(variation) || ShouldAddMiningPitLightConsole(variation);
        
    
    
    public static void Modify(ShipStatus ship)
    {
        // しばらくの措置として見た目チェンジサボ廃止
        ship.MapPrefab.infectedOverlay.transform.GetChild(6).GetChild(0).gameObject.SetActive(false);

        int fungleVariation = GeneralConfigurations.FungleVariation;

        if (fungleVariation == 1) FungleMidnight.Modify(ship);
        if (fungleVariation == 2) FunglePoisoned.Modify(ship);

        // Tint色変更
        if (fungleVariation > 0
            && ship.transform.TryFindChild(out var tint, "Backgrounds", "Base", "OverlayTint")
            && tint.TryGetComponent<SpriteRenderer>(out var tintRenderer))
        {
            tintRenderer.color = fungleVariation == 1 ? new(0.12f, 0.2f, 0.6f, 0.0884f) : new(0.04f, 0.7f, 0.06f, 0.0984f);
        }

        // 停電サボタージュ
        if (FungleHasLightSabotage(fungleVariation)) SetUpLightOutSabotageButtonsOnMinimap(ship);

        // ミニマップの表示物調整
        GameOperatorManager.Instance!.Subscribe<MapInstantiateEvent>(ev => {
            var map = MapBehaviour.Instance;
            if(map.transform.TryFindChild(out var roomeNames, "RoomNames"))
            {
                if (map.transform.TryFindChild(out var reactor, "Reactor")) reactor.transform.localPosition += new Vector3(0f, 0.4f, 0f);
                if (map.transform.TryFindChild(out var engine, "UpperEngine")) engine.transform.localPosition += new Vector3(-0.2f, -0.3f, 0f);
                if (map.transform.TryFindChild(out var miningPit, "MiningPit")) miningPit.transform.localPosition += new Vector3(0f, -0.08f, 0f);
            }
            if(map.countOverlay.transform.TryFindChild(out var engineCount, "UpperEngine")) engineCount.transform.localPosition += new Vector3(-0.2f, -0.3f, 0f);
        }, NebulaAPI.CurrentGame!);


        if (GeneralConfigurations.FungleSimpleLaboratoryOption.CurrentValue) ModifyLaboratory();

        {
            //鉱山下部の影
            ShipModification.AddModShadow([new(11.79f, 6.78f), new(9.91f, 7.07f)]);
            ShipModification.AddModShadow([new(13.65f, 6.77f), new(14.39f, 6.66f), new(15.73f, 7.16f)]);
        }

        if (fungleVariation == 0 && GeneralConfigurations.CurrentGameMode != GameModes.AeroGuesser && GeneralConfigurations.FungleForClassicGameOption.CurrentValue) ModifyFungleForClassicGame();

        if (GeneralConfigurations.FungleThinFogOption.CurrentValue)
        {
            var renderer = ship.transform.FindChild("FungleJungleShadow").GetChild(0).GetComponent<MeshRenderer>();
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
        if (GeneralConfigurations.FungleGlowingMushroomOption.CurrentValue && fungleVariation != 1)
        {
            void SetUpGlowingMush(GameObject obj)
            {
                var light = AmongUsUtil.GenerateCustomLight(new(0, 0));
                light.transform.SetParent(obj.transform);
                light.material.SetColor("_Color", Color.white.AlphaMultiplied(0.35f));
                light.transform.localScale = Vector3.one * 1.9f;
                light.transform.localPosition = new Vector3(0f, 0f, -15f);
            }
            ship.transform.FindChild("Outside").GetChild(0).GetChild(5).gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)SetUpGlowingMush);
            SetUpGlowingMush(ship.FastRooms[SystemTypes.Reactor].transform.FindChild("GlowingMushroom").gameObject);
        }

        // リアクターの制限時間
        ship.Systems[SystemTypes.Reactor].Cast<ReactorSystemType>().ReactorDuration = GeneralConfigurations.FungleReactorDurationOption.CurrentValue;

        // ベントの矢印が建物に隠れてしまう問題を修正
        if (ship.AllVents.Find(v => v.gameObject.name == "NorthWestJungleVent", out var vent)) vent.transform.SetLocalZ(-0.2f);

        //Storageの見た目がおかしい問題を修正
        PatchStorageBarrels(ship);

        //Storageの影を調整
        PatchStorageShadow(ship);

        //壁のすり抜け対策
        ShipModification.CreateAxeWall("MiningPit Right", [new(14.14f, 5.98f), new(16.25f, 7.58f), new(16.25f, 13.5f)]);
        ShipModification.CreateAxeWall("Jungle Right", [new(13.44f, -6.17f), new(13.09f, 0f)]);
    }

    #region LightOut
    static private void SetUpLightOutConsolesIfNeccesary()
    {
        int lightConsoleId = 0;
        Console SetUpAsLightConsole(Console console)
        {
            console.AllowImpostor = true;
            console.GhostsIgnored = true;
            console.TaskTypes = new TaskTypes[] { TaskTypes.FixLights };
            console.ConsoleId = lightConsoleId++;
            return console;
        }

        int fungleVariation = GeneralConfigurations.FungleVariation;
        if (ShouldAddJungleLightConsole()) SetUpAsLightConsole(ShipModification.CreateConsole(SystemTypes.Electrical, "lightJungle", fungleLight1Sprite.GetSprite(), new(-6.9618f, -5.9982f), -0.5f));
        if (ShouldAddDropshipLightConsole(fungleVariation)) SetUpAsLightConsole(ShipModification.CreateConsole(SystemTypes.Electrical, "lightDropship", fungleLight2Sprite.GetSprite(), new(-7.8f, 13.46f)));
        if (ShouldAddMiningPitLightConsole(fungleVariation))
        {
            var miningConsole = SetUpAsLightConsole(ShipModification.CreateConsole(SystemTypes.Electrical, "lightMining", fungleLight3Sprite.GetSprite(), new(13.68f, 7.83f)));
            UnityHelper.CreateObject<BoxCollider2D>("Collider", miningConsole.transform, new(0f, 0f, 0f)).size = new(0.6f, 0.3f);
        }
    }

    static private void SetUpLightOutSabotageButtonsOnMinimap(ShipStatus ship)
    {
        GameOperatorManager.Instance!.Subscribe<MapInstantiateEvent>(ev =>
        {
            var infected = MapBehaviour.Instance.infectedOverlay;
            var lightOut = GameObject.Instantiate(VanillaAsset.MapAsset[0].MapPrefab.infectedOverlay.transform.GetChild(3).GetChild(1).gameObject, infected.transform);
            lightOut.transform.localPosition = GeneralConfigurations.FungleVariation == 2 ? new(-0.117f, -0.8363f, -2f) : new(-1.2788f, 1.5801f, -2f);
            lightOut.transform.localScale = new(0.8f, 0.8f, 1f);
            var renderer = lightOut.GetComponent<SpriteRenderer>();
            renderer.SetCooldownNormalizedUvs();
            var button = lightOut.GetComponent<ButtonBehavior>();
            button.OnClick = new();
            button.OnClick.AddListener(() =>
            {
                if (!infected.CanUseSabotage) return;
                ship.RpcUpdateSystem(SystemTypes.Sabotage, (byte)SystemTypes.Electrical);
            });
            var allButtons = infected.allButtons.ToList();
            allButtons.Add(button);
            infected.allButtons = allButtons.ToArray();

            GameOperatorManager.Instance.Subscribe<GameHudUpdateEvent>(ev =>
            {
                if (infected.sabSystem != null)
                {
                    var perc = infected.DoorsPreventingSabotage ? 1f : infected.sabSystem.PercentCool;
                    if (renderer.AsBoolFast()) renderer.material.SetFloat("_Percent", perc);
                }

            }, new GameObjectLifespan(MapBehaviour.Instance.gameObject));
        }, Virial.NebulaAPI.CurrentGame!);
        var switchSystem = new SwitchSystem();
        ship.Systems[SystemTypes.Electrical] = switchSystem.CastFast<ISystemType>();
        ship.Systems[SystemTypes.Sabotage].CastFast<SabotageSystemType>().specials.Add(switchSystem.CastFast<IActivatable>());
        var specialTasks = ship.SpecialTasks.ToList();
        specialTasks.Add(VanillaAsset.MapAsset[0].SpecialTasks[1]);
        ship.SpecialTasks = specialTasks.ToArray();
    }
    #endregion

    #region SimplifiedLaboratory
    static private SpriteLoader customLaboratorySprite = SpriteLoader.FromResource("Nebula.Resources.FungleCustomLaboratory.png", 100f);
    static private SpriteLoader customLaboratoryWallSprite = SpriteLoader.FromResource("Nebula.Resources.FungleCustomLaboratoryWall.png", 100f);
    static private void ModifyLaboratory()
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        Transform laboratory = ship.FastRooms[SystemTypes.Laboratory].transform;

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
        GameObject collectSampleConsole = ship.transform.GetChild(5).GetChild(0).GetChild(2).GetChild(0).gameObject;
        var labSample = GameObject.Instantiate(collectSampleConsole, collectSampleConsole.transform.parent);
        labSample.name = "CollectSamples (Lab)";
        labSample.transform.GetChild(0).GetComponent<Console>().ConsoleId = 8;
        labSample.transform.localPosition = new(-8.393f, -2.4575f, 0.05f);

        //行き止まりを作成
        var jungleOutside = ship.transform.GetChild(5).GetChild(0);
        var obstacle = jungleOutside.GetChild(6).GetChild(2);
        var crystal = GameObject.Instantiate(obstacle.GetChild(0), ShipModification.ReverseScaler);
        crystal.localPosition = new(-6.2764f, -11.9491f, -1.0115f);
        crystal.localScale = new(-1f, 1f, 1f);
        ShipModification.AddModWall([new(-5.90f, -11.46f), new(-5.89f, -12.50f), new(-6.75f, -12.44f), new(-6.71f, -11.37f)]);
    }
    #endregion

    #region ClassicFungle
    static private TextureReplacer greenhouseReplacer = new(new ResourceTextureLoader("Nebula.Resources.ShipModification.Greenhouse.png"));
    static private TextureReplacer fungleMeetingReplacer = new(new ResourceTextureLoader("Nebula.Resources.ShipModification.MeetingRoom.png"));
    static private Image fungleCustomLadderImage = SpriteLoader.FromResource("Nebula.Resources.ShipModification.FungleLadder.png", 100f);
    static private Image fungleJungleOverlay1Image = SpriteLoader.FromResource("Nebula.Resources.ShipModification.JungleOverlay1.png", 100f);
    static private Image fungleJungleOverlay2Image = SpriteLoader.FromResource("Nebula.Resources.ShipModification.JungleOverlay2.png", 100f);
    static private Image fungleJungleReplaced1Image = SpriteLoader.FromResource("Nebula.Resources.ShipModification.JungleReplaced1.png", 100f);
    static private Image fungleWallImage = SpriteLoader.FromResource("Nebula.Resources.ShipModification.FungleWall.png", 100f);
    static private void ModifyFungleForClassicGame()
    {
        var ship = ShipStatus.Instance;
        var shipTransform = ship.transform;

        var jungleOutside = shipTransform.GetChild(5).GetChild(0);
        var jungleWalls = jungleOutside.GetChild(0);

        var labBack1 = jungleWalls.GetChild(6);
        var labBottom1 = jungleWalls.GetChild(7);
        var labLeft1 = jungleWalls.GetChild(8);
        var labRight1 = jungleWalls.GetChild(9);
        var greenhouse_lowerleft = jungleWalls.GetChild(16);
        var greenhouse_lowerright = jungleWalls.GetChild(17);
        var greenhouse_upperleft = jungleWalls.GetChild(18);
        var uppermid_bottom = jungleWalls.GetChild(22);
        var lab_bottom1 = jungleWalls.GetChild(7);

        var mushroom3 = jungleOutside.GetChild(1).GetChild(2);

        mushroom3.GetComponent<Mushroom>().origPosition = new(10.7582f, -14.7846f, 0.5f);
        mushroom3.transform.position = new(10.7582f, -14.7846f, 0.5f);

        {
            var overlay = greenhouse_upperleft.GetChild(1).GetComponent<SpriteRenderer>();
            overlay.sprite = fungleJungleOverlay1Image.GetSprite();
            overlay.transform.localPosition = new(-0.005f, 0.155f, -1.111f);
        }

        //キノコの壁
        greenhouse_lowerleft.gameObject.SetActive(false);
        greenhouse_lowerright.GetChild(0).gameObject.SetActive(false);
        GameObject.Instantiate(labBack1, shipTransform).localPosition = new Vector3(4.0736f, -12.6781f, -1.0119f);
        var belowGreenhouse = GameObject.Instantiate(uppermid_bottom, shipTransform);
        belowGreenhouse.localPosition = new Vector3(8.0559f, -14.8162f, -1.1f);
        belowGreenhouse.GetChild(0).gameObject.SetActive(false);

        //ミーティング下
        {
            var belowMeeting = GameObject.Instantiate(lab_bottom1, shipTransform);
            belowMeeting.localPosition = new Vector3(-1.79f, -4.7546f, -0.05f);
            belowMeeting.localScale = new(-1f, 1f, 1f);
            var overlay = belowMeeting.GetChild(1);
            overlay.transform.localPosition = new(-0.025f, 0.74f, -1f);
            overlay.GetComponent<SpriteRenderer>().sprite = fungleJungleOverlay2Image.GetSprite();
        }

        //ラボ上改変
        var replacedLabBack = UnityHelper.CreateSpriteRenderer("replacedImage", labBack1, new(-0.29f, 0f, 0f));
        replacedLabBack.sprite = fungleJungleReplaced1Image.GetSprite();
        labBack1.GetComponent<SpriteRenderer>().enabled = false;
        labBack1.SetLocalZ(-0.155f);
        labLeft1.SetLocalZ(-0.156f); //少しずつ手前にずらす
        labBottom1.SetLocalZ(-0.157f); //少しずつ手前にずらす
        labRight1.SetLocalZ(-1f);

        //ミーティング下の影
        ShipModification.AddModShadow([new(-3.74f, -3.53f), new(-3.52f, -3.84f), new(-2.63f, -3.95f), new(-2.00f, -3.87f), new(-1.94f, -3.55f)]);
        fungleMeetingReplacer.ReplaceSprite(ship.FastRooms[SystemTypes.MeetingRoom].transform.GetChild(0).GetChild(0).GetComponent<SpriteRenderer>());

        //温室左下の採集タスク
        {
            var sampleTask = jungleOutside.GetChild(2).GetChild(1);
            sampleTask.localPosition = new(2.5655f, -6.6045f, -0.01f);
            sampleTask.GetChild(0).GetComponent<CircleCollider2D>().isTrigger = true;
            var newCollider = sampleTask.GetChild(0).gameObject.AddComponent<CircleCollider2D>();
            newCollider.radius = 0.1f;
            newCollider.offset = new(0f, -0.19f);
        }

        //温室下部の小物
        var obstacle = jungleOutside.GetChild(6).GetChild(2);
        obstacle.localPosition = new(1.6472f, -10.8309f, 0.004f);

        //温室の画像差し替え
        {
            var greenhouseFloor = ship.FastRooms[SystemTypes.Greenhouse].transform.GetChild(0).GetChild(1).GetComponent<SpriteRenderer>();
            greenhouseReplacer.ReplaceSprite(greenhouseFloor);

            ShipModification.AddModShadow([new(7.04f, -12.75f), new(7.31f, -12.08f), new(7.32f, -11.87f)]);
        }

        //梯子の一方通行化
        {
            var ladders = shipTransform.GetChild(5).GetChild(1).GetChild(2);

            var leftLadder = ladders.GetChild(0);
            leftLadder.GetChild(0).gameObject.SetActive(false);

            var rightLadder = ladders.GetChild(1);
            rightLadder.GetChild(1).gameObject.SetActive(false);
            rightLadder.GetChild(3).GetComponent<SpriteRenderer>().sprite = fungleCustomLadderImage.GetSprite();
        }

        //高台の壁
        {
            var wall = UnityHelper.CreateSpriteRenderer("highlandWall", shipTransform, new(14.2091f, 4.4364f, 0f));
            wall.sprite = fungleWallImage.GetSprite();

            ShipModification.AddModShadow([new(14.41f, 2.85f), new(14.38f, 3.82f), new(14.15f, 4.98f), new(14.10f, 5.74f), new(14.32f, 6.66f), new(16.13f, 7.25f)]);
            ShipModification.AddModWall([new(14.23f, 2.53f), new(14.02f, 4.63f), new(13.88f, 6.45f)]);
            ShipModification.AddModWall([new(14.57f, 2.70f), new(14.39f, 4.76f), new(14.28f, 5.79f)]);
        }

        //追加ベント
        {
            var miningPitVent = ShipModification.CreateVent(SystemTypes.MiningPit, "MiningPitVent", new UnityEngine.Vector2(0.62f, 0.68f));
            var gemVent = ShipModification.CreateVent(SystemTypes.MiningPit, "GemVent", new UnityEngine.Vector2(2.20f, -4.57f));
            miningPitVent.Right = gemVent;
            gemVent.Left = miningPitVent;
        }
    }
    #endregion

    #region SomeImprovements
    static private void PatchStorageShadow(ShipStatus ship)
    {
        if (ship.transform.TryFindChild(out var outsideShadows, "Outside", "OutsideHighlands", "Shadows"))
        {
            if (outsideShadows.TryFindChild(out var top, "OnewayShadow-Top")) top.gameObject.SetActive(false);
            if (outsideShadows.TryFindChild(out var ledge, "OnewayShadow-Top+Ledge")) ledge.gameObject.SetActive(false);
        }
        var collider = ship.FastRooms[SystemTypes.Storage].transform.GetChild(1).GetComponent<EdgeCollider2D>();
        Vector2[] points = collider.points;
        var subArray1 = points.SubArray(0, 5);
        var subArray2 = points.SubArray(20, 27);

        collider.SetPoints(subArray1.ToIl2CppList());
        var newCollider = collider.gameObject.AddComponent<EdgeCollider2D>();
        newCollider.isTrigger = false;
        newCollider.SetPoints(subArray2.ToIl2CppList());
    }

    static private void PatchStorageBarrels(ShipStatus ship)
    {
        var storageBarrels = ship.FastRooms[SystemTypes.Storage].transform.GetChild(0).GetChild(3);
        var localPos = storageBarrels.localPosition;
        localPos.z = -1.095f;
        storageBarrels.localPosition = localPos;
    }
    #endregion
}
