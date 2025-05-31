using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Behavior;
using Nebula.Map;
using Nebula.Modules.GUIWidget;
using TMPro;
using UnityEngine.UIElements;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;
using static Nebula.Roles.Impostor.Cannon;

namespace Nebula.Roles.Impostor;

public class Disturber : DefinedSingleAbilityRoleTemplate<Disturber.Ability>, DefinedRole
{
    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    [NebulaRPCHolder]
    public class DisturbPole : NebulaSyncStandardObject
    {
        public static string MyTag = "DisturbPole";

        static SpriteLoader mySprite = SpriteLoader.FromResource("Nebula.Resources.ElecPole.png", 145f);
        static public Image PoleImage => mySprite;
        private bool isActivated = false;
        public bool IsActivated => isActivated;
        private void Activate()
        {
            Color = Color.white;
            isActivated = true;

            try
            {
                NebulaManager.Instance.StartCoroutine(ManagedEffects.CoDisappearEffect(LayerExpansion.GetObjectsLayer(), null, Position.AsVector3(-1f)));
            }
            catch { }
        }

        public DisturbPole(Vector2 pos) : base(pos, ZOption.Just, true, mySprite.GetSprite(), true)
        {
        }

        public override void OnInstantiated()
        {
            if (!AmOwner) Color = Color.clear;
        }

        public override void OnReleased()
        {
            base.OnReleased();

            try
            {
                NebulaManager.Instance.StartCoroutine(ManagedEffects.CoDisappearEffect(LayerExpansion.GetObjectsLayer(), null, Position.AsVector3(-1f)));
            }
            catch { }
        }

        static DisturbPole()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new DisturbPole(new(args[0], args[1])));
        }

        static public DisturbPole GeneratePole(Vector2 pos)
        {
            return (NebulaSyncObject.RpcInstantiate(MyTag, new float[] { pos.x, pos.y }).SyncObject as DisturbPole)!;
        }

        static public RemoteProcess<int> RpcActivate = new("ActivatePole", (id, _) => NebulaSyncObject.GetObject<DisturbPole>(id)?.Activate());
    }

    private Disturber() : base("disturber", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [DisturbCoolDownOption, DisturbDurationOption, MaxNumOfPolesOption, MaxDistanceBetweenPolesOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagDifficult, ConfigurationTags.TagFunny);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static private readonly FloatConfiguration DisturbCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.disturber.disturbCoolDown", (10f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration DisturbDurationOption = NebulaAPI.Configurations.Configuration("options.role.disturber.disturbDuration", (5f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration MaxNumOfPolesOption = NebulaAPI.Configurations.Configuration("options.role.disturber.maxNumOfPoles", (2, 15), 7);
    static private readonly FloatConfiguration MaxDistanceBetweenPolesOption = NebulaAPI.Configurations.Configuration("options.role.disturber.maxDistanceBetweenPoles", (2f, 6f, 1f), 5f, FloatConfigurationDecorator.Ratio);

    static public readonly Disturber MyRole = new();
    static private readonly GameStatsEntry StatsPole = NebulaAPI.CreateStatsEntry("stats.disturber.pole", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsDisturb = NebulaAPI.CreateStatsEntry("stats.disturber.disturb", GameStatsCategory.Roles, MyRole);


    public class DisturberMapLayer : MonoBehaviour
    {
        public record DisturbPolesSet(LineRenderer renderer, DisturbPole[] poles);
        static DisturberMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<DisturberMapLayer>();

        private List<DisturbPolesSet> poles = null!;
        private Camera camera;

        private LineRenderer lineRenderer;
        private CircleCollider2D collider;
        private SpriteRenderer circleRenderer;
        private SpriteRenderer poleRenderer;
        public List<(Vector3 minimapPos, Vector3 worldPos, DisturbPole pole)> Positions;
        private PassiveButton clickButton;
        private Disturber.Ability disturber;

        static private readonly Image whiteCircleSprite = SpriteLoader.FromResource("Nebula.Resources.WhiteCircle.png", 100f);

        private int CurrentPolesIncludingUnactivated => MaxNumOfPolesOption - disturber.CurrentPoles - (Positions?.Count ?? 0);
        public void SetDisturber(Disturber.Ability disturber)
        {
            this.disturber = disturber;
            disturber.UpdatePoleText(CurrentPolesIncludingUnactivated);
        }
        void Awake()
        {
            poles = new();
            Positions = new();

            float scale = MaxDistanceBetweenPolesOption / VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);
            collider = UnityHelper.CreateObject<CircleCollider2D>("Click", transform, new(0f, 0f, -5f));
            collider.radius = scale;
            collider.isTrigger = true;
            circleRenderer = UnityHelper.CreateObject<SpriteRenderer>("CircleRenderer", collider.transform, new(0f, 0f, -20f));
            circleRenderer.transform.localScale = Vector3.one * scale;
            circleRenderer.sprite = EffectCircle.OuterCircleImage.GetSprite();
            circleRenderer.gameObject.SetActive(false);
            var dotRenderer = UnityHelper.CreateObject<SpriteRenderer>("DotRenderer", collider.transform, new(0f, 0f, -25f));
            dotRenderer.sprite = whiteCircleSprite.GetSprite();
            dotRenderer.color = Color.green;
            dotRenderer.transform.localScale = Vector3.one * 0.45f;

            clickButton = collider.gameObject.SetUpButton(false);

            clickButton.OnMouseOver.AddListener(() =>
            {
                NebulaManager.Instance.SetHelpWidget(clickButton, 
                new NoSGameObjectGUIWrapper(Virial.Media.GUIAlignment.Center, () =>
                {
                    var mesh = UnityHelper.CreateMeshRenderer("MeshRenderer", transform, new(0, -0.08f, -1), null);
                    mesh.filter.CreateRectMesh(new(2f, 1.2f));
                    mesh.renderer.sharedMaterial.mainTexture = camera.SetCameraRenderTexture(200, 120);
                    mesh.renderer.transform.localScale = MapBehaviourExtension.GetMinimapFlippedScale();
                    return (mesh.renderer.gameObject, new(2f, 1.2f));
                }), true);
            });
            clickButton.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(clickButton));
            var exButton = clickButton.gameObject.AddComponent<ExtraPassiveBehaviour>();
            exButton.OnRightClicked = () =>
            {
                if (Positions.Count > 0)
                {
                    NebulaSyncObject.RpcDestroy(Positions[Positions.Count - 1].pole.ObjectId);
                    Positions.RemoveAt(Positions.Count - 1);
                    UpdateLine();
                }
            };

            void TryPlacePoleHere()
            {
                if (CurrentPolesIncludingUnactivated <= 0) return;

                var screenPosAsWorld = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer());
                var worldPosOnMinimap = transform.InverseTransformPoint(screenPosAsWorld);
                worldPosOnMinimap.z = -5f;
                var worldPos = VanillaAsset.ConvertFromMinimapPosToWorld(worldPosOnMinimap, AmongUsUtil.CurrentMapId);

                bool canPlace = MapData.GetCurrentMapData().CheckMapArea(worldPos, 0.06f);
                if (canPlace)
                {
                    Positions.Add((worldPosOnMinimap, worldPos.AsVector3(0f), DisturbPole.GeneratePole(worldPos)));
                    lineRenderer.SetColors(Color.green, Color.green);
                    UpdateLine();

                    collider.transform.localPosition = worldPosOnMinimap;

                    disturber.UpdatePoleText(CurrentPolesIncludingUnactivated);
                }
            }

            void UpdateLine()
            {
                lineRenderer.positionCount = Positions.Count;
                lineRenderer.SetPositions(Positions.Select(p => p.minimapPos).ToArray());
                disturber.UpdatePoleText(CurrentPolesIncludingUnactivated);
            }

            clickButton.OnClick.AddListener(() => TryPlacePoleHere());
            camera = UnityHelper.CreateRenderingCamera("DisturberCamera", null, Vector3.zero, 1.6f, LayerExpansion.GetLayerMask(LayerExpansion.GetDefaultLayer(), LayerExpansion.GetObjectsLayer(), LayerExpansion.GetShortObjectsLayer(), LayerExpansion.GetShipLayer()));
            poleRenderer = UnityHelper.CreateObject<SpriteRenderer>("PoleImage", camera.transform, new(0f, 0f, -10f), LayerExpansion.GetDefaultLayer());
            poleRenderer.sprite = DisturbPole.PoleImage.GetSprite();
            lineRenderer = UnityHelper.SetUpLineRenderer("PoleLine", transform, new(0f, 0f, -10f), LayerExpansion.GetUILayer(), width: 0.035f);
        }
        void Update()
        {
            //z: -10くらいのところに閉じるボタンがあるので、背景のクリックガードは-5くらいに置けばよい
            // 背景クリックガード :-5, 線及び点のクリック: -10
            // EdgeCollider2D.EdgeRadiousが使えそう

            var screenPosAsWorld = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer());
            var worldPosOnMinimap = transform.InverseTransformPoint(screenPosAsWorld);
            var worldPos = VanillaAsset.ConvertFromMinimapPosToWorld(worldPosOnMinimap, AmongUsUtil.CurrentMapId);
            camera.transform.position = worldPos;

            if(Positions.Count == 0)
            {
                worldPosOnMinimap.z = -5f;
                collider.transform.localPosition = worldPosOnMinimap;
                circleRenderer.gameObject.SetActive(false);

                collider.gameObject.SetActive(MapData.GetCurrentMapData().CheckMapArea(worldPos, 0f));
            }
            else
            {
                circleRenderer.gameObject.SetActive(true);
                collider.gameObject.SetActive(true);
                collider.transform.localPosition = ((Vector2)Positions[Positions.Count - 1].minimapPos).AsVector3(-5f);
            }

            bool canPlace = MapData.GetCurrentMapData().CheckMapArea(worldPos, 0.06f);
            poleRenderer.color = (canPlace ? Color.Lerp(Color.cyan, Color.green, 0.3f) : Color.red).AlphaMultiplied(0.5f);
        }

        public void Clear(bool destroyPoles)
        {
            if(destroyPoles) foreach (var p in Positions) NebulaSyncObject.RpcDestroy(p.pole.ObjectId);
            Positions.Clear();
            lineRenderer.positionCount = 0;
        }

        public void RegisterPoles(DisturbPole[] poles, List<DisturbPole[]> list)
        {
            var positions = poles.Select(p => VanillaAsset.ConvertToMinimapPos(p.Position, AmongUsUtil.CurrentMapId)).ToArray();

            var renderer = UnityHelper.SetUpLineRenderer("PoleLine", transform, new(0f, 0f, -8f), LayerExpansion.GetUILayer(), width: 0.035f);
            renderer.positionCount = poles.Length;
            renderer.SetPositions(positions.Select(p => p.AsVector3(0f)).ToArray());
            Color col = Color.green.RGBMultiplied(0.65f);
            renderer.SetColors(col, col);

            var collider = UnityHelper.CreateObject<EdgeCollider2D>("LineButton", renderer.transform, new(0f, 0f, -4f));
            
            Il2CppSystem.Collections.Generic.List<Vector2> points = new();
            foreach (var pos in positions) points.Add(pos);
            collider.SetPoints(points);
            collider.edgeRadius = 0.1f;

            var button = collider.gameObject.SetUpButton(true);
            Color hovered = Color.Lerp(Color.green.RGBMultiplied(0.9f), Color.yellow, 0.5f);
            button.OnMouseOver.AddListener(() =>
            {
                renderer.SetColors(hovered, hovered);
                NebulaManager.Instance.SetHelpWidget(button, Language.Translate("role.disturber.ui.removePoles"));
            });
            button.OnMouseOut.AddListener(() =>
            {
                renderer.SetColors(col, col);
                NebulaManager.Instance.HideHelpWidgetIf(button);
            });

            button.OnClick.AddListener(() =>
            {
                using (RPCRouter.CreateSection("DisturberDestroy"))
                {
                    poles.Do(p => NebulaSyncObject.RpcDestroy(p.ObjectId));
                }
                list.Remove(poles);
                GameObject.Destroy(renderer.gameObject);

                disturber.UpdatePoleText(CurrentPolesIncludingUnactivated);
            });
        }

        void OnDestroy()
        {
            if (camera) GameObject.Destroy(camera.gameObject);
            Clear(true);
        }

        void OnEnable()
        {
            poleRenderer.enabled = true;
        }

        void OnDisable()
        {
            poleRenderer.enabled = false;

            NebulaManager.Instance.ScheduleDelayAction(() =>
            {
                if (Positions.Count >= 2) disturber.PlacePoles();
            });
        }
    }


    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static float PoleDistanceMin = 0.8f;
        static float PoleDistanceMax => MaxDistanceBetweenPolesOption;


        static public Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ElecPolePlaceButton.png", 115f);
        static public Image disturbButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DisturbButton.png", 115f);

        static private Image elecAnimHSprite = SpriteLoader.FromResource("Nebula.Resources.ElecAnim.png", 100f);
        static private Image elecAnimVSprite = SpriteLoader.FromResource("Nebula.Resources.ElecAnimSub.png", 100f);

        private DisturberMapLayer mapLayer = null!;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                AchievementToken<(IHudOverrideTask? cmTask, ElectricTask? elTask, float time, int dead, bool ability, bool isCleared)> acTokenChallenge = new("disturber.challenge", (null, null, 0f, 0, false, false), (val, _) => val.isCleared);

                ModAbilityButton disturbButton = null!;

                var openMapButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "disturber.place",
                    0f, "place", placeButtonSprite, _ => !disturbButton.IsInEffect, _ => !MapBehaviour.Instance || !MapBehaviour.Instance.IsOpen);
                openMapButton.OnClick = button => {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        HudManager.Instance.InitMap();
                        MapBehaviour.Instance.ShowNormalMap();
                        MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);
                    });
                };

                var placeButton = NebulaAPI.Modules.AbilityButton(this, alwaysShow: true)
                    .BindKey(Virial.Compat.VirtualKeyInput.Ability, "disturber.place")
                    .SetImage(placeButtonSprite).ShowUsesIcon(0, "").SetLabel("place").SetAsUsurpableButton(this);
                placeButton.Availability = (button) => mapLayer && mapLayer.Positions.Count >= 2;
                placeButton.Visibility = (button) => !MyPlayer.IsDead && MapBehaviour.Instance && MapBehaviour.Instance.IsOpen && mapLayer && mapLayer.gameObject.active;
                placeButton.OnClick = button => PlacePoles();
                updatePoleTextFunc = (num) => placeButton.UpdateUsesIcon(num.ToString());

                disturbButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "disturber.disturb",
                    DisturbCoolDownOption, DisturbDurationOption, "disturb", disturbButtonSprite).SetAsUsurpableButton(this);
                disturbButton.Availability = (button) => poles.Count > 0 && (!MapBehaviour.Instance || !MapBehaviour.Instance.IsOpen);
                disturbButton.OnEffectStart = (button) =>
                {
                    new StaticAchievementToken("disturber.common1");

                    using (RPCRouter.CreateSection("Disturb"))
                    {
                        foreach (var p in poles) RpcDisturb.Invoke(p.Select(pole => pole.Position).ToArray());
                    }

                    if (acTokenChallenge != null) acTokenChallenge.Value.ability = true;
                    CheckChallengeAchievement();
                    StatsDisturb.Progress();
                };
                disturbButton.OnEffectEnd = (button) => button.StartCoolDown();

                GameOperatorManager.Instance?.Subscribe<PlayerSabotageTaskAddLocalEvent>(ev =>
                {
                    if (ev.Player.AmOwner)
                    {
                        acTokenChallenge.Value.cmTask = ev.SystemTask.TryCast<IHudOverrideTask>();
                        acTokenChallenge.Value.elTask = ev.SystemTask.TryCast<ElectricTask>();
                        acTokenChallenge.Value.time = NebulaGameManager.Instance?.CurrentTime ?? 0f;
                        acTokenChallenge.Value.dead = 0;
                        acTokenChallenge.Value.ability = disturbButton.IsInEffect;
                    }
                }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerTaskRemoveLocalEvent>(ev =>
                {
                    if (ev.Player.AmOwner)
                    {
                        CheckChallengeAchievement();
                        if (acTokenChallenge.Value.cmTask == ev.Task.TryCast<IHudOverrideTask>()) acTokenChallenge.Value.cmTask = null;
                        if (acTokenChallenge.Value.elTask == ev.Task.TryCast<ElectricTask>()) acTokenChallenge.Value.elTask = null;
                    }
                }, this);
                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                {
                    CheckChallengeAchievement();
                    acTokenChallenge.Value.cmTask = null;
                    acTokenChallenge.Value.elTask = null;
                    acTokenChallenge.Value.dead = 0;
                    acTokenChallenge.Value.ability = false;
                }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerMurderedEvent>(ev =>
                {
                    acTokenChallenge.Value.dead++;
                    CheckChallengeAchievement();
                }, this);

                void CheckChallengeAchievement()
                {
                    if (AmOwner && acTokenChallenge != null && acTokenChallenge.Value.dead >= 3 && acTokenChallenge.Value.time + 40f < (NebulaGameManager.Instance?.CurrentTime ?? 0f) && (acTokenChallenge.Value.elTask != null || acTokenChallenge.Value.cmTask != null) && acTokenChallenge.Value.ability) acTokenChallenge.Value.isCleared = true;
                }
            }
        }

        List<DisturbPole[]> poles = new();
        public int CurrentPoles => poles.Sum(p => p.Length);

        Action<int>? updatePoleTextFunc = null;
        public void UpdatePoleText(int num) => updatePoleTextFunc?.Invoke(num);

        public void PlacePoles()
        {
            foreach (var p in mapLayer.Positions)
            {
                DisturbPole.RpcActivate.Invoke(p.pole.ObjectId);
            }
            if (mapLayer.Positions.Count >= 6) new StaticAchievementToken("disturber.common2");
            StatsPole.Progress(mapLayer.Positions.Count);

            var array = mapLayer.Positions.Select(p => p.pole).ToArray();
            poles.Add(array);
            mapLayer.RegisterPoles(array, poles);
            mapLayer.Clear(false);
        }


        [Local]
        void OnOpenMap(AbstractMapOpenEvent ev)
        {
            if (ev is MapOpenNormalEvent && !IsUsurped)
            {
                if (!mapLayer)
                {
                    mapLayer = UnityHelper.CreateObject<DisturberMapLayer>("DisturberLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    this.BindGameObject(mapLayer.gameObject);
                    mapLayer.SetDisturber(this);
                    poles.Do(p => mapLayer.RegisterPoles(p, poles));
                }
                mapLayer.gameObject.SetActive(true);
            }
            else
            {
                if(mapLayer) mapLayer.gameObject.SetActive(false);
            }
        }

        static private void InstantiateCollider(Vector2 pos1, Vector2 pos2, float duration)
        {
            float absX = Mathf.Abs(pos1.x - pos2.x);
            float absY = Mathf.Abs(pos1.y - pos2.y);
            bool isVertical = absX > 0f ? (absY / absX) > 1.75f : true;

            if(isVertical ? pos1.y > pos2.y : pos1.x > pos2.x) {
                var temp = pos1;
                pos1 = pos2;
                pos2 = temp;
            }

            //ポール中心の位置
            //pos1.y -= 0.3f;
            //pos2.y -= 0.3f;


            var obj = new GameObject("ElecBarrior");
            var meshFilter = obj.AddComponent<MeshFilter>();
            var meshRenderer = obj.AddComponent<MeshRenderer>();
            var colliderObj = UnityHelper.CreateObject("Collider", obj.transform, Vector3.zero, LayerExpansion.GetShipLayer());
            var collider = colliderObj.AddComponent<EdgeCollider2D>();

            //UV座標を更新
            void UpdateUV(int num)
            {
                float fNum1 = (float)num / 3f;
                float fNum2 = (float)(num + 1) / 3f;

                meshFilter.mesh.SetUVs(0, (Vector2[])[new(fNum1, 0f), new(fNum2, 0f), new(fNum2, 1f), new(fNum1, 1f)]);
            }

            //障壁の座標
            Vector3 colliderPos = (pos1 + pos2) * 0.5f;
            colliderPos.z = Mathf.Max(pos1.y, pos2.y) / 1000f;
            obj.transform.localPosition = colliderPos;

            Vector2 pos1Rel = pos1 - (Vector2)colliderPos;
            Vector2 pos2Rel = pos2 - (Vector2)colliderPos;

            meshFilter.mesh = new();

            Vector3[] vertices = isVertical ? 
                [
                pos1Rel + new Vector2(-0.22f,0.2f),
                pos2Rel + new Vector2(-0.22f,0.5f),
                pos2Rel + new Vector2(0.22f,0.5f),
                pos1Rel + new Vector2(0.22f,0.2f)
                ] : 
                [
                pos1Rel + new Vector2(0f,0.42f),
                pos2Rel + new Vector2(0f,0.42f),
                pos2Rel + new Vector2(0f,-0.2f),
                pos1Rel + new Vector2(0f,-0.2f)
                ];
            meshFilter.mesh.SetVertices(vertices);
            UpdateUV(0);
            meshFilter.mesh.SetIndices((int[])[0, 1, 2, 2, 3, 0], MeshTopology.Triangles, 0);

            meshRenderer.material = UnityHelper.GetMeshRendererMaterial(); //new Material(Shader.Find("Sprites/Default"));
            meshRenderer.material.mainTexture = (isVertical ? elecAnimVSprite : elecAnimHSprite).GetSprite().texture;
            meshRenderer.gameObject.layer = LayerExpansion.GetObjectsLayer();

            //当たり判定
            collider.points = (Vector2[])[
                pos1Rel + new Vector2(0f, -0.22f),
                pos2Rel + new Vector2(0f, -0.22f),
                pos2Rel + new Vector2(0f, 0.05f),
                pos1Rel + new Vector2(0f, 0.05f),
                pos1Rel + new Vector2(0f, -0.22f)
            ];
            collider.edgeRadius = 0.08f;
            
            IEnumerator CoUpdate()
            {
                float timer = 0f;
                int counter = 0;
                while(duration > 0f)
                {
                    duration -= Time.deltaTime;
                    timer -= Time.deltaTime;

                    if(timer < 0f)
                    {
                        counter = (counter + 1) % 3;
                        UpdateUV(counter);
                        timer = 0.1f;
                    }

                    yield return null;
                }

                GameObject.Destroy(obj);
                yield break;
            }

            NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
        }

        static public RemoteProcess<Vector2[]> RpcDisturb = new("Disturb", (message, _) =>
        {
            float duration = DisturbDurationOption;
            for (int i = 0; i < message.Length - 1; i++) InstantiateCollider(message[i], message[i + 1], duration);
        });
    }
}
