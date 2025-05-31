using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Abilities;
using Nebula.VoiceChat;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

file static class UbiquitousDroneAsset
{
    public static XOnlyDividedSpriteLoader droneSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Drone.png", 100f, 4);
    public static SpriteLoader droneShadeSprite = SpriteLoader.FromResource("Nebula.Resources.DroneShade.png", 150f);
}

public class UbiquitousDrone : MonoBehaviour
{
    SpriteRenderer droneRenderer = null!;
    SpriteRenderer shadeRenderer = null!;
    Rigidbody2D myRigidBody = null!;
    const float DroneHeight = 0.95f;
    static UbiquitousDrone() => ClassInjector.RegisterTypeInIl2Cpp<UbiquitousDrone>();

    
    public void Awake()
    {
        myRigidBody = UnityHelper.CreateObject<Rigidbody2D>("DroneBody", transform.parent, transform.localPosition, LayerExpansion.GetPlayersLayer());
        myRigidBody.velocity = Vector2.zero;
        myRigidBody.gravityScale = 0f;
        myRigidBody.freezeRotation = true;
        myRigidBody.fixedAngle = true;
        myRigidBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        myRigidBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        myRigidBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = myRigidBody.gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.2f;
        collider.isTrigger = false;

        transform.SetParent(myRigidBody.transform, true);

        //レイヤーがデフォルトなら影内のドローンは見えない
        droneRenderer = UnityHelper.CreateObject<SpriteRenderer>("Renderer", transform, Vector3.zero,LayerExpansion.GetShipLayer());
        droneRenderer.sprite = UbiquitousDroneAsset.droneSprite.GetSprite(0);
        
        shadeRenderer = UnityHelper.CreateObject<SpriteRenderer>("ShadeRenderer", myRigidBody.transform, Vector3.zero, LayerExpansion.GetDefaultLayer());
        shadeRenderer.sprite = UbiquitousDroneAsset.droneShadeSprite.GetSprite();

        IEnumerator CoMoveOffset()
        {
            float t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                float p = Mathf.Pow(t / 2f, 0.7f);
                transform.localPosition = new(0f, p * DroneHeight);
                shadeRenderer.color = new(1f, 1f, 1f, 1f - p * 0.5f);
                yield return null;
            }
        }

        StartCoroutine(CoMoveOffset().WrapToIl2Cpp());
    }

    float updateTimer = 0f;
    int imageIndex = 0;
    
    public Vector3 ColliderPosition => myRigidBody.transform.position;

    public int CameraRoughness => 1 << Mathf.Min(5, (int)(PlayerControl.LocalPlayer.transform.position.Distance(transform.position) / 5.2f));

    void UpdateSprite()
    {
        imageIndex = (imageIndex + 1) % 2;

        if (Math.Abs(myRigidBody.velocity.x) > 0.1f)
        {
            //横方向へ移動中
            droneRenderer.flipX = myRigidBody.velocity.x < 0f;
            droneRenderer.sprite = UbiquitousDroneAsset.droneSprite.GetSprite(2 + imageIndex);
        }
        else {
            //静止
            droneRenderer.sprite = UbiquitousDroneAsset.droneSprite.GetSprite(imageIndex);
        }
    }

    public void FixedUpdate()
    {
        bool isOperating = HudManager.Instance.PlayerCam.Target == this;
        if (isOperating)
        {
            var vec = DestroyableSingleton<HudManager>.Instance.joystick.DeltaL * 3.5f;
            var mat = GamePlayer.LocalPlayer!.Unbox().DirectionalPlayerSpeed;
            vec = new(vec.x * mat.x + vec.y * mat.y, vec.x * mat.z + vec.y * mat.w);
            
            myRigidBody.velocity = vec;
        }
        else
            myRigidBody.velocity = Vector2.zero;

        var pos = myRigidBody.transform.position;
        pos.z = pos.y / 1000f;
        myRigidBody.transform.position = pos;
    }

    public void Update()
    {
        updateTimer -= Time.deltaTime;
        if(updateTimer < 0f)
        {
            UpdateSprite();
            updateTimer = 0.15f;
        }

        droneRenderer.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 1.4f) * 0.08f, -3f);
    }

    public void CallBack()
    {
        IEnumerator CoCallBack()
        {
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                float p = Mathf.Pow(t / 0.8f, 4.5f);
                transform.localPosition = new(0f, DroneHeight + p * 1.25f);
                droneRenderer.color = new(1f, 1f, 1f, 1f - p);
                shadeRenderer.color = new(1f, 1f, 1f, 0.5f - p * 0.5f);
                yield return null;
            }

            DestroyDroneObject();
        }
        StartCoroutine(CoCallBack().WrapToIl2Cpp());
    }

    public void DestroyDroneObject()
    {
        GameObject.Destroy(myRigidBody.gameObject);
    }
}

public class UbiquitousDetachedDrone : MonoBehaviour, IVoiceComponent
{
    SpriteRenderer droneRenderer = null!;
    static UbiquitousDetachedDrone() => ClassInjector.RegisterTypeInIl2Cpp<UbiquitousDetachedDrone>();

    public void Awake()
    {
        droneRenderer = UnityHelper.CreateObject<SpriteRenderer>("Renderer", transform, new(0f,0f,-0.5f), LayerExpansion.GetShipLayer());
        droneRenderer.sprite = UbiquitousDroneAsset.droneSprite.GetSprite(0);

        var shadeRenderer = UnityHelper.CreateObject<SpriteRenderer>("ShadeRenderer", transform, Vector3.zero, LayerExpansion.GetDefaultLayer());
        shadeRenderer.sprite = UbiquitousDroneAsset.droneShadeSprite.GetSprite();
        shadeRenderer.color = new(1f, 1f, 1f, 0.5f);

        var pos = transform.position;
        pos.z = pos.y / 1000f;
        transform.position = pos;
    }

    float updateTimer = 0f;
    int imageIndex = 0;

    void UpdateSprite()
    {
        imageIndex = (imageIndex + 1) % 2;
        droneRenderer.sprite = UbiquitousDroneAsset.droneSprite.GetSprite(imageIndex);
    }

    const float DroneHeight = 0.35f;

    float IVoiceComponent.Radious => Ubiquitous.droneMicrophoneRadiousOption;

    float IVoiceComponent.Volume => 0.95f;

    Vector2 IVoiceComponent.Position => transform.position;

    public void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer < 0f)
        {
            UpdateSprite();
            updateTimer = 0.15f;
        }

        droneRenderer.transform.localPosition = new Vector3(0f,  Mathf.Sin(Time.time * 1.4f) * 0.08f + DroneHeight, -3f);
    }

    bool IVoiceComponent.CanPlaySoundFrom(IVoiceComponent mic)
    {
        return (mic != (this as IVoiceComponent)) && mic is UbiquitousDetachedDrone;
    }
}

public class UbiquitousMapLayer : MonoBehaviour
{
    ObjectPool<SpriteRenderer> darkIconPool = null!;
    ObjectPool<SpriteRenderer> lightIconPool = null!;
    List<Vector2> dronePos = null!;
    AchievementToken<bool> challengeToken = null!;

    static UbiquitousMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<UbiquitousMapLayer>();
    public void ReferenceDronePos(List<Vector2> list)
    {
        dronePos = list;
    }

    public void Awake()
    {
        darkIconPool = new(ShipStatus.Instance.MapPrefab.HerePoint, transform);
        darkIconPool.OnInstantiated = icon => PlayerMaterial.SetColors(new Color(0.3f, 0.3f, 0.3f), icon);

        lightIconPool = new(ShipStatus.Instance.MapPrefab.HerePoint, transform);
        lightIconPool.OnInstantiated = icon => PlayerMaterial.SetColors(new Color(1f, 1f, 1f), icon);

        challengeToken = new("ubiquitous.challenge",false,(val,_) => val && Ubiquitous.droneDetectionRadiousOption < 3f);
    }

    public void Update()
    {
        darkIconPool.RemoveAll();
        lightIconPool.RemoveAll();

        var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
        var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);

        int alive = 0, shown = 0;

        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            //自分自身、死んでいる場合は何もしない
            if (p.AmOwner || p.IsDead) continue;

            alive++;

            //不可視のプレイヤーは何もしない
            if(p.Unbox().IsInvisible || p.VanillaPlayer.inVent) continue;

            foreach (var pos in dronePos)
            {
                float d = pos.Distance(p.VanillaPlayer.transform.position);
                if(d < Ubiquitous.droneDetectionRadiousOption)
                {
                    var icon = (DynamicPalette.IsLightColor(Palette.PlayerColors[p.PlayerId]) ? lightIconPool : darkIconPool).Instantiate();
                    icon.transform.localPosition = VanillaAsset.ConvertToMinimapPos(p.VanillaPlayer.transform.position, center, scale);
                    shown++;
                    if (alive >= 10 && alive == shown) challengeToken.Value = true;
                    break;
                }
            }
        }
    }
}

[NebulaRPCHolder]
public class Ubiquitous : DefinedSingleAbilityRoleTemplate<Ubiquitous.Ability>, DefinedRole
{
    private Ubiquitous(): base("ubiquitous", new(56,155,223), RoleCategory.CrewmateRole, Crewmate.MyTeam, [droneCoolDownOption, droneDurationOption, droneMicrophoneRadiousOption, droneDetectionRadiousOption, doorHackCoolDownOption, doorHackRadiousOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Ubiquitous.png");

        MetaAbility.RegisterCircle(new("role.ubiquitous.droneRange", () => droneDetectionRadiousOption, () => null, UnityColor));

        GameActionTypes.UbiquitousInvokeDroneAction = new("ubiquitous.invoke", this, isPlacementAction: true);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));


    static private readonly FloatConfiguration droneCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.ubiquitous.droneCoolDown", (5f, 120f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration droneDurationOption = NebulaAPI.Configurations.Configuration("options.role.ubiquitous.droneDuration", (5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static internal readonly FloatConfiguration droneMicrophoneRadiousOption = NebulaAPI.Configurations.Configuration("options.role.ubiquitous.microphoneRadious", (0f,5f,0.25f),2f, FloatConfigurationDecorator.Ratio);
    static internal readonly FloatConfiguration droneDetectionRadiousOption = NebulaAPI.Configurations.Configuration("options.role.ubiquitous.detectionRadious", (0f, 10f, 0.25f), 3f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration doorHackCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.ubiquitous.doorHackCoolDown", (10f, 120f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration doorHackRadiousOption = NebulaAPI.Configurations.Configuration("options.role.ubiquitous.doorHackRadious", (0f, 10f, 0.25f), 3f, FloatConfigurationDecorator.Ratio);

    static public readonly Ubiquitous MyRole = new();
    static private readonly GameStatsEntry StatsDrones = NebulaAPI.CreateStatsEntry("stats.ubiquitous.drones", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        UbiquitousDrone? myDrone = null;
        UbiquitousMapLayer? mapLayer = null;
        List<Vector2> dronePos = new();

        static private readonly Image droneButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DroneButton.png", 115f);
        static private readonly Image callBackButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DroneCallBackButton.png", 115f);
        static private readonly Image hackButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DroneHackButton.png", 115f);

        [Local]
        void OnOpenNormalMap(MapOpenNormalEvent ev)
        {
            if (mapLayer is null)
            {
                mapLayer = UnityHelper.CreateObject<UbiquitousMapLayer>("UbiquitousLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                mapLayer.ReferenceDronePos(dronePos);
                this.BindGameObject(mapLayer.gameObject);
            }

            mapLayer.gameObject.SetActive(!MeetingHud.Instance);
        }

        [Local]
        void OnOpenAdminMap(MapOpenAdminEvent ev)
        {
            if (mapLayer) mapLayer?.gameObject.SetActive(false);
        }
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (myDrone)
            {
                AmongUsUtil.SetCamTarget();
                RpcSpawnDetachedDrone.Invoke(myDrone!.ColliderPosition);
                dronePos.Add(myDrone!.ColliderPosition);
                myDrone.DestroyDroneObject();
            }
        }
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                int currentSize = 1;
                var cameraObj = HudContent.InstantiateContent("UbiquitousCamera", true, true, false, true);
                this.BindGameObject(cameraObj.gameObject);

                var mesh = UnityHelper.CreateMeshRenderer("MeshRenderer", cameraObj.transform, new(0, -0.08f, -1), LayerExpansion.GetUILayer());
                mesh.filter.CreateRectMesh(new(1.34f, 0.78f), new(0.35f, 0f, 0f));
                Camera droneCam = null!;

                var backMesh = UnityHelper.CreateMeshRenderer("MeshBackRenderer", mesh.renderer.transform, new(0, 0, 0.1f), LayerExpansion.GetUILayer(), MyRole.UnityColor);
                backMesh.filter.CreateRectMesh(new(1.34f + 0.05f, 0.78f + 0.05f), new(0.35f, 0f, 0f));
                
                var droneButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "ubiquitous.drone",
                    droneCoolDownOption, droneDurationOption, "drone", droneButtonSprite,
                    null, _ => {
                        cameraObj.gameObject.SetActive(myDrone && AmongUsUtil.CurrentCamTarget != myDrone && !MeetingHud.Instance);
                        if (cameraObj.gameObject.active)
                        {
                            int level = Mathf.Max(2, myDrone!.CameraRoughness);
                            if (currentSize != level)
                            {
                                currentSize = level;
                                mesh.renderer.sharedMaterial.mainTexture = droneCam.SetCameraRenderTexture(134 / currentSize * 2, 78 / currentSize * 2);
                            }
                        }
                        return !MyPlayer.IsDead;
                    }
                    );
                droneButton.Availability = (button) => MyPlayer.CanMove || (myDrone && AmongUsUtil.CurrentCamTarget == myDrone);
                droneButton.OnClick = _ =>
                {
                    droneButton.StartEffect();
                    if (droneButton.IsInEffect)
                    {
                        if (!myDrone)
                        {
                            NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.UbiquitousInvokeDroneAction);
                            myDrone = UnityHelper.CreateObject<UbiquitousDrone>("Drone", null, MyPlayer.TruePosition.ToUnityVector());
                            StatsDrones.Progress();

                            droneCam = UnityHelper.CreateRenderingCamera("Camera", myDrone.transform, Vector3.zero, 1.4f);
                            var isc = droneCam.gameObject.AddComponent<IgnoreShadowCamera>();
                            isc.ShowNameText = false;
                            droneCam.gameObject.AddComponent<ResetIgnoreShadowCamera>();

                            mesh.renderer.sharedMaterial.mainTexture = droneCam.SetCameraRenderTexture(134, 78);
                        }
                        AmongUsUtil.ToggleCamTarget(myDrone, null);
                    }
                };
                droneButton.OnEffectEnd = (button) =>
                {
                    AmongUsUtil.SetCamTarget(null);
                    droneButton.StartCoolDown();
                };
                droneButton.OnBroken = (button) =>
                {
                    AmongUsUtil.SetCamTarget(null);
                };
                droneButton.SetAsUsurpableButton(this);

                var callBackButton = NebulaAPI.Modules.AbilityButton(this).SetLabel("callBack").SetImage(callBackButtonSprite).SetAsUsurpableButton(this);
                callBackButton.Availability = (button) => MyPlayer.CanMove || (myDrone && AmongUsUtil.CurrentCamTarget == myDrone);
                callBackButton.Visibility = (button) => !MyPlayer.IsDead && myDrone;
                callBackButton.OnClick = (button) =>
                {
                    AmongUsUtil.SetCamTarget();
                    myDrone!.CallBack();
                    myDrone = null;

                    if (droneButton.IsInEffect) droneButton.InterruptEffect();
                };

                AchievementToken<int> totalAchievement = new("ubiquitous.common1", 0, (val, _) => val >= 5);

                var hackButton = NebulaAPI.Modules.AbilityButton(this)
                    .BindKey(Virial.Compat.VirtualKeyInput.SecondaryAbility, "ubiquitous.doorHack")
                    .SetImage(hackButtonSprite)
                    .SetLabel("doorHack");
                hackButton.Availability = (button) => MyPlayer.CanMove || (myDrone && AmongUsUtil.CurrentCamTarget == myDrone);
                hackButton.Visibility = (button) => !MyPlayer.IsDead && myDrone;
                hackButton.OnClick = (button) =>
                {
                    float distance = doorHackRadiousOption;
                    foreach(var door in ShipStatus.Instance.AllDoors)
                    {
                        if (!door.IsOpen && door.Room != SystemTypes.Decontamination && myDrone!.ColliderPosition.Distance(door.transform.position) < distance)
                        {
                            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, (byte)(door.Id | 64));

                            totalAchievement.Value++;
                            new StaticAchievementToken("ubiquitous.common2");
                        }
                    }
                    
                    hackButton.StartCoolDown();
                };
                hackButton.SetAsUsurpableButton(this);
                var coolDownTimer = new TimerImpl(doorHackCoolDownOption).SetAsAbilityCoolDown().Start().Register(this);
                var pred = coolDownTimer.Predicate;
                coolDownTimer.SetPredicate(()=>pred!.Invoke() || (myDrone && AmongUsUtil.CurrentCamTarget == myDrone));
                hackButton.CoolDownTimer = coolDownTimer;
            }
        }


        void IGameOperator.OnReleased()
        {
            if (AmOwner)
            {
                AmongUsUtil.SetCamTarget();
                if (myDrone) myDrone!.DestroyDroneObject();
            }
        }

        [Local]
        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            //死亡時、元の視点に戻す
            AmongUsUtil.SetCamTarget();
        }

        bool IsOperatingDrone => myDrone && AmongUsUtil.CurrentCamTarget == myDrone;
        //ドローン視点では壁を無視
        bool IPlayerAbility.EyesightIgnoreWalls => IsOperatingDrone;

        [Local]
        public void EditLightRange(LightRangeUpdateEvent ev)
        {
            if (IsOperatingDrone) ev.LightRange *= 1.8f;
        }

    }

    static RemoteProcess<Vector2> RpcSpawnDetachedDrone = new("SpawnDetachedDrone",
        (message,_) => {
            var drone = UnityHelper.CreateObject<UbiquitousDetachedDrone>("DetachedDrone", null, message);
            NebulaGameManager.Instance?.VoiceChatManager?.AddMicrophone(drone);
            NebulaGameManager.Instance?.VoiceChatManager?.AddSpeaker(drone);
        });
    
}
