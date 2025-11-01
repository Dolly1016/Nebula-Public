using BepInEx.Unity.IL2CPP.Utils;
using Innersloth.Assets;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Player;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
[NebulaRPCHolder]
internal class FakePlayerManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess) => DIManager.Instance.RegisterModule(() => new FakePlayerManager());
    
    private int AvailableId = 1;

    private FakePlayerManager()
    {
        ModSingleton<FakePlayerManager>.Instance = this;
    }

    protected override void OnInjected(Virial.Game.Game container) => GameOperatorManager.Instance?.Subscribe(this, container);
    
    public int GenerateAvailableId() => (AvailableId++) << 8 + GamePlayer.LocalPlayer!.PlayerId;

    [EventPriority(0)]
    void OnInteract(PlayerInteractPlayerLocalEvent ev)
    {
        if (ev.IsCanceled && ev.Target is IFakePlayer p) RpcNoticeFailedInteract.Invoke((ev.User, ev.Target));
    }

    static readonly private RemoteProcess<(GamePlayer user, IPlayerlike target)> RpcNoticeFailedInteract = new("NoticeFailedInteraction", (message, _) =>
    {
        if (message.target is IFakePlayer fp && fp.AmOwner) GameOperatorManager.Instance?.Run(new PlayerInteractionFailedForMyFakePlayerEvent(message.user, fp));
    });

}

[NebulaRPCHolder]
internal class FakePlayerNetTransform : IGameOperator
{
    Rigidbody2D body;
    bool amOwner;
    internal bool enabled = true;

    
    public FakePlayerNetTransform(Rigidbody2D body, FakePlayer player, bool amOwner)
    {
        this.body = body;
        this.player = player;
        this.amOwner = amOwner;

        this.lastPosSent = body.transform.position;
        this.incomingPosQueue.Enqueue(body.transform.position);

        enabled = true;
        isPaused = false;
    }

    public void SetPaused(bool isPaused)
    {
        this.isPaused = isPaused;
    }

    public void Halt()
    {
        ushort num = (ushort)(lastSequenceId + 1);
        SnapTo(body.transform.position, num);
    }

    public void RpcSnapTo(Vector2 position) => RpcCallSnapTo.Invoke(((this.player as IPlayerlike).PlayerlikeId, position, lastSequenceId));
    

    static readonly RemoteProcess<(int playerId, Vector2 position, ushort id)> RpcCallSnapTo = new("FakeSnapTo", (message, calledByMe) => (NebulaGameManager.Instance?.GetPlayerlike(message.playerId) as FakePlayer)?.NetTransform.SnapTo(message.position, (ushort)(message.id + (calledByMe ? 1 : 2))));

    public void SnapTo(Vector2 position)
    {
        ushort num = (ushort)(lastSequenceId + 2);
        this.SnapTo(position, num);
    }

    public void ClearPositionQueues()
    {
        if (amOwner)
            this.sendQueue.Clear();
        else
            this.incomingPosQueue.Clear();
    }

    private bool IsInMiddleOfAnimationThatMakesPlayerInvisible()
    {
        return false;

        //いまのところベントに入るモーションはしない
        //return this.player.Animations.IsPlayingEnterVentAnimation() || this.player.walkingToVent;
    }

    private void SnapTo(Vector2 position, ushort minSid)
    {
        if (!NetHelpers.SidGreaterThan(minSid, lastSequenceId))
        {
            return;
        }
        if (IsInMiddleOfAnimationThatMakesPlayerInvisible())
        {
            tempSnapPosition = new Vector2?(position);
            return;
        }
        ClearPositionQueues();
        lastSequenceId = minSid;
        tempSnapPosition = null;
        Transform transform = body.transform;
        body.position = position;
        transform.position = position;
        body.velocity = Vector2.zero;
    }

    int interval = 2;
    void FixedUpdate(GameUpdateEvent ev)
    {
        if (isPaused || !enabled) return;

        if (amOwner)
        {
            interval--;
            if (this.HasMoved())
            {
                this.sendQueue.Enqueue(this.body.position);
            }
            if (interval <= 0 && sendQueue.Count > 0)
            {
                Sync();
                interval = 2;
            }
            return;
        }
        else
        {
            if (this.tempSnapPosition != null && !this.IsInMiddleOfAnimationThatMakesPlayerInvisible())
            {
                this.SnapTo(this.tempSnapPosition.Value);
                this.tempSnapPosition = null;
            }
            if (this.incomingPosQueue.Count < 1)
            {
                return;
            }
            this.SkipExcessiveFrames();
            this.SetMovementSmoothingModifier();
            this.MoveTowardNextPoint();
        }
    }

    private bool HasMoved()
    {
        float num = Vector2.Distance(body.position, this.lastPosSent);
        return num > 0.0001f;
    }

    private static readonly RemoteProcess<(int playerId, ushort lastSequenceId, Vector2[] positions)> RpcSync = new("FakeNetTransformSync",
        (message, calledByMe) => {
            if (calledByMe) return;

            var fakePlayer = (NebulaGameManager.Instance?.GetPlayerlike(message.playerId) as FakePlayer);
            if (fakePlayer == null) return;

            var netTransform = fakePlayer.NetTransform;
            
            Vector2 vector;
            if (netTransform.incomingPosQueue.Count > 0)
            {
                vector = netTransform.incomingPosQueue.Last();
            }
            else
            {
                vector = netTransform.body.position;
            }
            var length = message.positions.Length;
            for (int i = 0; i < length; i++)
            {
                ushort num3 = (ushort)((int)message.lastSequenceId + i);
                Vector2 vector2 = message.positions[i];
                if (NetHelpers.SidGreaterThan(num3, netTransform.lastSequenceId))
                {
                    netTransform.lastSequenceId = num3;
                    netTransform.incomingPosQueue.Enqueue(vector2);
                    vector = vector2;
                }
            }
            if (netTransform.IsInMiddleOfAnimationThatMakesPlayerInvisible()) netTransform.tempSnapPosition = vector;
            else netTransform.tempSnapPosition = null;

        });

    public void Sync()
    {
        if (this.isPaused) return;
        if (!this.enabled) return;
        
        if (sendQueue.Count == 0) return;

        lastSequenceId += 1;
        var count = sendQueue.Count();
        RpcSync.Invoke(((this.player as IPlayerlike).PlayerlikeId, lastSequenceId, sendQueue.ToArray()));
        lastPosSent = sendQueue.Last();
        sendQueue.Clear();
        lastSequenceId += (ushort)(count - 1);
    }

    private void MoveTowardNextPoint()
    {
        Vector2 vector = incomingPosQueue.Peek();
        Vector2 vector2 = body.position;
        Vector2 vector3 = vector - vector2;
        if (ShouldExtendCurrentFrame(vector, vector2))
        {
            Vector2 vector4 = vector3.normalized * idealSpeed * rubberbandModifier;
            vector4 = Vector2.ClampMagnitude(vector4, 10f);
            body.velocity = vector4;
            lastPosition = body.position;
            return;
        }
        if (incomingPosQueue.Count <= 1)
        {
            if (Vector2.Distance(body.position, vector) > 0.01f)
            {
                body.position = vector;
                body.velocity = Vector2.zero;
                lastPosition = body.position;
            }
            return;
        }
        Vector2 vector5 = incomingPosQueue.Dequeue();
        if (Vector2.Distance(body.position, vector5) > 0.05f)
        {
            body.position = vector5;
        }
        vector = incomingPosQueue.Peek();
        vector2 = body.position;
        vector3 = vector - vector2;
        idealSpeed = vector3.magnitude / Time.fixedDeltaTime;
        Vector2 vector6 = vector3.normalized * idealSpeed * rubberbandModifier;
        vector6 = Vector2.ClampMagnitude(vector6, 10f);
        body.velocity = vector6;
        lastPosition = body.position;
    }

    private void SkipExcessiveFrames()
    {
        if (incomingPosQueue.Count < QUEUE_LENGTH_FOR_SNAPPING) return;
        if (body)
        {
            body.position = incomingPosQueue.Peek();
            MoveTowardNextPoint();
            if (incomingPosQueue.Count >= QUEUE_LENGTH_FOR_SECOND_SNAP)
            {
                body.position = this.incomingPosQueue.Peek();
                MoveTowardNextPoint();
                return;
            }
        }
        else
        {
            body.position = this.incomingPosQueue.Dequeue();
        }
    }

    private bool ShouldExtendCurrentFrame(Vector2 nextPos, Vector2 currentPos)
    {
        return !DidPassPosition(nextPos, lastPosition, currentPos) && incomingPosQueue.Count <= QUEUE_THRESHOLD_FOR_SMOOTHING;
    }

    private bool DidPassPosition(Vector2 nextPos, Vector2 lastPos, Vector2 currentPos)
    {
        float num = Vector2.Distance(lastPos, currentPos);
        float num2 = Vector2.Distance(currentPos, nextPos);
        float num3 = Vector2.Distance(lastPos, nextPos);
        return Mathn.Abs(num - (num3 + num2)) < REPLAY_POSITION_THRESHOLD;
    }

    private void SetMovementSmoothingModifier()
    {
        if (GeneralConfigurations.LowLatencyPlayerSyncOption && (AmongUsUtil.IsCustomServer() || AmongUsUtil.IsLocalServer()))
        {
            float num = incomingPosQueue.Count switch
            {
                < 2 => 0.7f, 
                3 => 0.85f, 
                4 or 5 => 0.9f, 
                _ => 1.2f
            };
            rubberbandModifier = Mathn.Lerp(rubberbandModifier, num, Time.fixedDeltaTime * 3f);
        }
        else
        {
            float num = ((incomingPosQueue.Count <= QUEUE_THRESHOLD_FOR_SMOOTHING) ? SMOOTHING_BAND_MODIFIER : NEUTRAL_BAND_MODIFIER);
            rubberbandModifier = Mathn.Lerp(rubberbandModifier, num, Time.fixedDeltaTime * SMOOTHING_LERP_RATE);
        }
    }

    private const float SEND_MOVEMENT_THRESHOLD = 0.0001f;
    private const float REPLAY_POSITION_THRESHOLD = 0.003f;
    private const float NEUTRAL_BAND_MODIFIER = 0.995f;
    private const float SMOOTHING_BAND_MODIFIER = 0.5f;
    private const float SMOOTHING_LERP_RATE = 3f;
    private const int QUEUE_LENGTH_FOR_SNAPPING = 12;
    private const int QUEUE_LENGTH_FOR_SECOND_SNAP = 14;
    private const int QUEUE_THRESHOLD_FOR_SMOOTHING = 5;
    private const int SNAP_TO_SEQUENCE_ID_BUFFER = 2;

    private FakePlayer player;
    private Queue<Vector2> sendQueue = [];
    private Queue<Vector2> incomingPosQueue = [];
    private float rubberbandModifier = 1f;
    private float idealSpeed;
    private bool isPaused;
    private ushort lastSequenceId = 0;
    private Vector2 lastPosition;
    private Vector2 lastPosSent;
    private Vector2? tempSnapPosition;
}

internal class FakePet : IGameOperator
{
    CosmeticsLayer cosmeticsLayer;
    FakePlayer player;
    PetBehaviour vanillaPet => cosmeticsLayer.CurrentPet;

    public FakePet(CosmeticsLayer layer, FakePlayer player)
    {
        this.player = player;
        this.cosmeticsLayer = layer;
    }

    void IGameOperator.OnReleased()
    {
        if(vanillaPet) GameObject.Destroy(vanillaPet.gameObject);
    }
    public void UpdatePet(PetData petData, Vector2? position = null)
    {
        if (vanillaPet) GameObject.Destroy(vanillaPet.gameObject);
        
        cosmeticsLayer.UnloadAddressableAsset(cosmeticsLayer.petAsset);
        NebulaManager.Instance.StartCoroutine(cosmeticsLayer.CoLoadAssetAsync<PetBehaviour>(petData.Cast<IAddressableAssetProvider<PetBehaviour>>(),(Il2CppSystem.Action<PetBehaviour>)(Action<PetBehaviour>)((PetBehaviour pet)=>
        {
            cosmeticsLayer.currentPet = GameObject.Instantiate<PetBehaviour>(pet, null);
            cosmeticsLayer.currentPet.enabled = false;

            cosmeticsLayer.currentPet.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetPlayersLayer());

            vanillaPet.SetCrewmateColor(cosmeticsLayer.ColorId);
            vanillaPet.transform.localPosition = Vector3.zero;
            vanillaPet.SetDefaultMaterial();
            cosmeticsLayer.SetPetFlipX(cosmeticsLayer.FlipX);
            vanillaPet.SetIdle();

            if (position != null) vanillaPet.transform.position = position.Value.AsVector3(position.Value.y * 1000f);
            
            if (Application.targetFrameRate > 30) vanillaPet.rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (player.InMovingPlat) cosmeticsLayer.SetPetVisible(false);
        })));       
    }

    public PetData Data => vanillaPet.data;

    public FakePlayer TargetPlayer => player;
    public int RendererCount => vanillaPet.renderers.Length;

    public bool Visible => vanillaPet.visible;

    public bool FlipX => vanillaPet.flipX;

    void OnUpdate(GameUpdateEvent ev)
    {
        if (!vanillaPet) return;

        Vector2 truePosition = player.TruePosition.ToUnityVector();
        Vector2 truePosition2 = vanillaPet.GetTruePosition();
        Vector2 vector = vanillaPet.rigidbody.velocity;
        Vector2 vector2 = truePosition - truePosition2;
        float num = 0.2f; //canmoveでなければ0にするらしい
        if (vector2.sqrMagnitude > num)
        {
            if (vector2.sqrMagnitude > 2f)
            {
                vanillaPet.transform.position = truePosition;
                return;
            }
            vector2 *= 5f * AmongUsUtil.GetCurrentNormalOption().PlayerSpeedMod;
            vector = vector * 0.8f + vector2 * 0.2f;
        }
        else
        {
            vector *= 0.7f;
        }
        AnimationClip currentAnimation = vanillaPet.animator.GetCurrentAnimation();
        if (vector.sqrMagnitude > 0.01f)
        {
            if (currentAnimation != vanillaPet.walkClip)
            {
                vanillaPet.StartWalkAnim();
            }
            if (vector.x < -0.01f) vanillaPet.FlipX = true;
            else if (vector.x > 0.01f) vanillaPet.FlipX = false;
        }
        else if (currentAnimation == vanillaPet.walkClip)
        {
            vanillaPet.animator.Play(vanillaPet.idleClip, 1f);
        }
        vanillaPet.rigidbody.velocity = vector;
    }

    void LateUpdate(GameLateUpdateEvent ev)
    {
        if (!vanillaPet) return;

        Vector3 localPosition = vanillaPet.transform.localPosition;
        localPosition.z = (localPosition.y + vanillaPet.yOffset) / 1000f + 0.0002f;
        vanillaPet.transform.localPosition = localPosition;
    }
}


[NebulaPreprocess(PreprocessPhase.BuildNoSModuleContainer), NebulaRPCHolder]
internal record FakePlayerParameters(Vector2 position, KillCharacteristics KillCharacteristics, bool CanBeTarget, bool InitialFlipX, Vector2? petInitialPos, OutfitCandidate? specialOutfit = null)
{
    static FakePlayerParameters()
    {
        new RemoteProcessArgument<FakePlayerParameters>((writer, parameters) =>
        {
            writer.Write(parameters!.position.x);
            writer.Write(parameters.position.y);
            writer.Write((int)parameters.KillCharacteristics);
            writer.Write(parameters.CanBeTarget);
            writer.Write(parameters.InitialFlipX);
            writer.Write(parameters.petInitialPos?.x ?? parameters.position.x);
            writer.Write(parameters.petInitialPos?.y ?? (parameters.position.y + 0.2f));
            writer.WriteIfNotNullCustom(parameters.specialOutfit);
        }, (reader) =>
        {
            return new(new(reader.ReadSingle(), reader.ReadSingle()), (KillCharacteristics)reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean(),new(reader.ReadSingle(), reader.ReadSingle()), reader.ReadIfNotNullCustom<OutfitCandidate>());
        });
    }
}


[NebulaPreprocess(PreprocessPhase.BuildNoSModule), NebulaRPCHolder]
internal class FakePlayer : AbstractModuleContainer, IFakePlayer, ILifespan, IGameOperator
{
    private enum MovingPlatformState
    {
        None,
        NotAllowed,
        Allowed,
        Failed,
        Done,
    }

    protected readonly PlayerDisplay displayPlayer;
    protected readonly Collider2D collider;
    protected readonly Rigidbody2D body;
    private readonly FakePet pet;
    private readonly int id;
    private readonly GamePlayer visualPlayer;
    private bool isDead;
    private KillCharacteristics killCharacteristics;
    private bool canBeTarget;
    private bool amOwner;
    internal readonly FakePlayerNetTransform NetTransform;
    internal readonly FakePlayerLogics Logics;
    private OutfitCandidate? specialOutfit = null;
    IPlayerLogics IPlayerlike.Logic => this.Logics;

    protected bool isDeadObject { get; private set; } = false;
    public void Release() => isDeadObject = true;


    protected FakePlayer(bool amOwner, int id, GamePlayer visualPlayer, FakePlayerParameters parameters, IFakePlayerSpawnStrategy spawnStrategy)
    {
        this.id = id;
        this.displayPlayer = VanillaAsset.GetPlayerDisplay(true, true);
        this.displayPlayer.transform.position = parameters.position.AsVector3(parameters.position.y / 1000f);
        this.displayPlayer.Cosmetics.GetComponent<NebulaCosmeticsLayer>().fakePlayerCache = this;
        this.displayPlayer.Cosmetics.SetFlipX(parameters.InitialFlipX);
        this.collider = displayPlayer.GetComponent<Collider2D>();
        this.body = displayPlayer.GetComponent<Rigidbody2D>();
        this.visualPlayer = visualPlayer;
        this.amOwner = amOwner;
        this.NetTransform = new FakePlayerNetTransform(body, this, amOwner).Register(this);
        this.pet = new FakePet(displayPlayer.Cosmetics, this).Register(this);

        this.killCharacteristics = parameters.KillCharacteristics;
        this.canBeTarget = parameters.CanBeTarget;

        this.specialOutfit = parameters.specialOutfit;

        NebulaGameManager.Instance.RegisterFakePlayer(this);

        Logics = new(this);

        //スポーン & デスポーン
        GameOperatorManager.Instance?.RegisterOnReleased(() =>
        {
            GameObject.Destroy(displayPlayer.gameObject);
            NebulaGameManager.Instance?.RemovePlayerlike(this);
            spawnStrategy.OnDespawn(this, parameters);
        }, this);
        spawnStrategy.OnSpawn(this, parameters);

        this.Register(this);

        CheckAndUpdateOutfit(parameters.petInitialPos);
    }

    private void CheckAndUpdateOutfit(Vector2? petPos) { 
        if(specialOutfit != null)
        {
            UpdateOutfit((visualPlayer.GetOutfit(null, specialOutfit.Priority + 1) ?? specialOutfit.Outfit), petPos);
        }
        else
        {
            UpdateOutfit(visualPlayer.CurrentOutfit, petPos);
        }
    }

    static public FakePlayer SpawnLocalFakePlayer(GamePlayer visualPlayer, FakePlayerParameters parameters) => new FakePlayer(true, ModSingleton<FakePlayerManager>.Instance.GenerateAvailableId(), visualPlayer, parameters, new LocalOnlyFakePlayerSpawnStrategy());

    int IPlayerlike.PlayerlikeId => id;

    GamePlayer? IPlayerlike.RealPlayer => visualPlayer;

    string IPlayerlike.Name => visualPlayer.Name;
    public bool AmOwner => amOwner;
    public bool IsDead => isDead;
    public bool IsActive => !IsDeadObject;

    public KillCharacteristics KillCharacteristics => killCharacteristics;
    public bool CanBeTarget => canBeTarget;

    public Virial.Compat.Vector2 TruePosition => new((Vector2)displayPlayer.transform.position + collider.offset);

    public Virial.Compat.Vector2 Position => new(displayPlayer.transform.position);

    public virtual bool IsDeadObject => isDeadObject;

    //Vanillaの挙動を模倣するためのメンバ
    protected bool vanilla_inMovingPlat = false;
    protected bool vanilla_onLadder = false;
    internal bool InMovingPlat => vanilla_inMovingPlat;
    internal bool OnLadder => vanilla_onLadder;
    private MovingPlatformState MovingPlatState = MovingPlatformState.None; 

    public bool UsingSomeUtility => vanilla_inMovingPlat || vanilla_onLadder;

    private List<SpriteRenderer> additionalRenderers = [];
    CosmeticsLayer IPlayerlike.VanillaCosmetics => displayPlayer.Cosmetics;

    internal static readonly RemoteProcess<(int id, GamePlayer? visualPlayer, FakePlayerParameters parameters)> RpcSpawnFakePlayer = new("SendSpawnFakePlayer",
    (message, calledByMe) =>
    {
        if (!calledByMe)
        {
            new FakePlayer(false, message.id, message.visualPlayer, message.parameters, new OnlineNonOwnerFakePlayerSpawnStrategy());
        }
    });

    internal static readonly RemoteProcess<int> RpcDespawnFakePlayer = new("DespawnFakePlayer", (id, _) => (NebulaGameManager.Instance?.GetPlayerlike(id) as FakePlayer)?.Release());

    internal static readonly RemoteProcess<(int player, bool allowed)> ReplyMovingPlatform = new("ReplyMovingPlatform", (message, _) => {
        if (NebulaGameManager.Instance?.GetPlayerlike(message.player) is FakePlayer fp) fp.MovingPlatState = message.allowed ? MovingPlatformState.Allowed : MovingPlatformState.NotAllowed;
    });
    internal static readonly RemoteProcess<int> RequestMovingPlatform = new("RequestMovingPlatform", (id, _) => {
        var player = NebulaGameManager.Instance?.GetPlayerlike(id);
        if (player == null) return;
        if (player is FakePlayer fp)
            fp.MovingPlatState = MovingPlatformState.None;
        if (AmongUsClient.Instance.AmHost)
        {
            AirshipStatus airship = ShipStatus.Instance.TryCast<AirshipStatus>();
            if (airship)
            {
                Vector2 vector = (Vector2)airship!.GapPlatform!.transform.position - player.Position.ToUnityVector();
                bool canUse = !airship.GapPlatform.Target && vector.magnitude < 3f;
                if (canUse) airship.GapPlatform.Target = player.RealPlayer.VanillaPlayer;
                ReplyMovingPlatform.Invoke((id, canUse));
            }
        }
    });

    OutfitDefinition? currentOutfit;
    OutfitDefinition IPlayerlike.CurrentOutfit => currentOutfit ??= visualPlayer.CurrentOutfit;

    void UpdateOutfit(OutfitDefinition outfitDef, Vector2? petPosition = null)
    {
        currentOutfit = outfitDef;
        NetworkedPlayerInfo.PlayerOutfit outfit = outfitDef.outfit;

        var animations = displayPlayer.Animations;
        var cosmetics = displayPlayer.Cosmetics;

        //PlayerControl.SetOutfit
        //cosmetics.SetName(outfit.PlayerName);
        cosmetics.nameText.text = outfit.PlayerName; //SetNameメソッドは空白の場合に別のテキストが入ってしまうため使えない
        cosmetics.SetNameMask(true);
        cosmetics.SetColor(outfit.ColorId);
        cosmetics.SetHat(outfit.HatId, outfit.ColorId);
        cosmetics.SetNamePosition(new Vector3(0f, string.IsNullOrEmpty(outfit.HatId) ? 0.8f : 1f, -0.5f));
        cosmetics.SetSkin(outfit.SkinId, outfit.ColorId, (Il2CppSystem.Action)(() =>
        {
            if (animations.IsPlayingSpawnAnimation()) cosmetics.AnimateSkinSpawn(animations.Time);
            ShipStatus instance = ShipStatus.Instance;
            if (instance != null && instance.Type == ShipStatus.MapType.Fungle)
            {
                if (vanilla_inMovingPlat) cosmetics.AnimateSkinJump();
                if (animations.IsPlayingClimbAnimation())
                {
                    bool flag = body.velocity.y <= 0f;
                    animations.PlayClimbAnimation(flag);
                    cosmetics.AnimateClimb(flag);
                }
            }
        }));
        cosmetics.SetVisor(outfit.VisorId, outfit.ColorId);

        foreach (var r in additionalRenderers) if (r) r.material = cosmetics.currentBodySprite.BodySprite.sharedMaterial;

        pet.UpdatePet(HatManager.Instance.GetPetById(outfit.PetId), petPosition);
        
        cosmetics.Visible = !IsDead || GamePlayer.LocalPlayer!.IsDead;
    }

    [NebulaRPCHolder]
    internal class FakePlayerLogics : IPlayerLogics
    {
        FakePlayer player;
        public FakePlayerLogics(FakePlayer player)
        {
            this.player = player;
        }

        public UnityEngine.Vector2 Position { get => player.Position; set => player.displayPlayer.transform.position = value.AsVector3(value.y / 1000f); }
        public UnityEngine.Vector2 TruePosition => player.TruePosition;
        public Collider2D GroundCollider => player.collider;

        public PlayerAnimations Animations => player.displayPlayer.Animations;

        public Rigidbody2D Body => player.body;

        public void Halt() => player.NetTransform.Halt();

        bool IPlayerLogics.IsActive => player.body;
        public IPlayerlike Player => player;
        bool IPlayerLogics.InMovingPlat => player.vanilla_inMovingPlat;
        bool IPlayerLogics.OnLadder => player.vanilla_onLadder;
        float IPlayerLogics.TrueSpeed { get
            {
                var speed = PlayerModInfo.OriginalSpeed;
                var mod = 1f;
                if (GameManager.Instance != null)
                {
                    mod = AmongUsUtil.GetCurrentNormalOption().PlayerSpeedMod;
                    //死んでいたらちょっと早くなるが、幽霊状態を考慮していないのでスキップ
                }
                return speed * mod;
            }
        }
        public void ResetAnimState() {
            /*
            this.myPlayer.FootSteps.Stop();
            this.myPlayer.FootSteps.loop = false;
            */
            player.displayPlayer.Cosmetics.SetHatAndVisorIdle(player.visualPlayer.CurrentOutfit.outfit.ColorId);

            if (!player.isDead)
            {
                player.displayPlayer.Cosmetics.AnimateSkinIdle();
                player.displayPlayer.Animations.PlayIdleAnimation();
                //this.myPlayer.Visible = true;
                //player.SetHatAndVisorAlpha(1f);
                return;
            }
            player.displayPlayer.Cosmetics.SetGhost();
            player.displayPlayer.Animations.PlayGhostIdleAnimation();
            //player.SetHatAndVisorAlpha(0.5f);
        }
        public void ResetMoveState()
        {
            //petting = false
            //inVent = false
            //walkingToVent = false
            player.vanilla_onLadder = false;
            UpdateNetworkTransformState(true);
            player.NetTransform.SetPaused(false);
            Body.isKinematic = false;
            player.collider.enabled = true;
            ResetAnimState();
        }
        public IEnumerator UseLadder(Ladder ladder)
        {
            RpcSendUseLadder.Invoke((ladder.Id, this.player.id));
            yield return UseLadderImpl(ladder);
        }

        static readonly private RemoteProcess<(int ladderId, int playerId)> RpcSendUseLadder = new("FakeSendUseLadder", (message, calledByMe) =>
        {
            if(!calledByMe) NebulaManager.Instance.StartCoroutine((NebulaGameManager.Instance?.GetPlayerlike(message.playerId) as FakePlayer)?.Logics.UseLadderImpl(ShipStatus.Instance.Ladders.FirstOrDefault(l => l.Id == message.ladderId)!));
        });

        private IEnumerator UseLadderImpl(Ladder ladder)
        { 
            //this.myPlayer.moveable = false;
            //this.myPlayer.ForceKillTimerContinue = true;
            player.vanilla_onLadder = true;
            UpdateNetworkTransformState(false);
            Body.isKinematic = true;
            ClearPositionQueues();

            yield return WalkPlayerTo(ladder.transform.position, 0.0005f, 1f, false);
            yield return Effects.Wait(0.1f);

            player.FlipX = false;
            bool down = ladder.IsTop;
            Animations.PlayClimbAnimation(down);
            player.displayPlayer.Cosmetics.AnimateClimb(down);
            player.displayPlayer.Cosmetics.TogglePetVisible(false);
            if (player.displayPlayer.Cosmetics.bodyType.GetVisorOptions().HideDuringClimb) player.displayPlayer.Cosmetics.ToggleVisor(false);

            yield return WalkPlayerTo(ladder.Destination.transform.position, 0.001f, (float)(ladder.IsTop ? 2 : 1), false);
            player.visualPlayer.VanillaCosmetics.SetPetPosition(this.player.Position.ToUnityVector());
            this.ResetAnimState();
            yield return Effects.Wait(0.1f);

            //this.myPlayer.moveable = true;
            UpdateNetworkTransformState(true);
            Body.isKinematic = false;
            player.vanilla_onLadder = false;
        }

        public IEnumerator UseZipline(ZiplineConsole zipline)
        {
            RpcSendUseZipline.Invoke((this.player.id, zipline.atTop));
            yield return UseZiplineImpl(zipline);
        }

        static readonly private RemoteProcess<(int playerId, bool atTop)> RpcSendUseZipline = new("FakeSendUseZipline", (message, calledByMe) =>
        {
            if (!calledByMe)
            {
                var fungleShipStatus = ShipStatus.Instance.TryCast<FungleShipStatus>();
                if (fungleShipStatus)
                {
                    if(fungleShipStatus!.Zipline.gameObject.GetComponentsInChildren<ZiplineConsole>().Find(c => c.atTop == message.atTop, out var zipline))
                    {
                        NebulaManager.Instance.StartCoroutine((NebulaGameManager.Instance?.GetPlayerlike(message.playerId) as FakePlayer)?.Logics.UseZiplineImpl(zipline!));
                    }
                    
                }
            }
        });

        private IEnumerator UseZiplineImpl(ZiplineConsole zipline)
        {
            Transform start;
            Transform end;
            Transform landing;
            ZiplineBehaviour zBehaviour = zipline.zipline;

            if (zipline.atTop)
            {
                start = zBehaviour.handleTop;
                end = zBehaviour.handleBottom;
                landing = zBehaviour.landingPositionBottom;
            }
            else
            {
                start = zBehaviour.handleBottom;
                end = zBehaviour.handleTop;
                landing = zBehaviour.landingPositionTop;
            }
            bool fromTop = zipline.atTop;

            float ziplineTime = Time.time;
            float totalTime = Time.time;

            void ZiplinePlaySound(AudioClip sound, Vector2 soundPosition)
            {
                float soundVolume = SoundManager.GetSoundVolume(soundPosition, PlayerControl.LocalPlayer.GetTruePosition(), 2f, 6f, 0f);
                if (soundVolume <= 0f) return;
                SoundManager.Instance.PlaySoundImmediate(sound, false, soundVolume, 1f, null);
            }

            var id = (player as IPlayerlike).PlayerlikeId;
            string ziplineSoundId = "ZiplineTravel_" + id;
            IEnumerator ZiplineStartSound()
            {
                if (fromTop)
                {
                    SoundManager.Instance.PlayDynamicSound(ziplineSoundId, zBehaviour.downSound, false, (DynamicSound.GetDynamicsFunction)(zBehaviour.SoundDynamics), SoundManager.Instance.SfxChannel);
                    yield return new WaitForSeconds(zBehaviour.downSound.length - 0.05f);
                    SoundManager.Instance.PlayDynamicSound(ziplineSoundId, zBehaviour.downLoopSound, true, (DynamicSound.GetDynamicsFunction)(zBehaviour.SoundDynamics), SoundManager.Instance.SfxChannel);
                }
                if (zBehaviour.ShouldPlaySound())
                {
                    SoundManager.Instance.PlayDynamicSound(ziplineSoundId, zBehaviour.upSound, true, (DynamicSound.GetDynamicsFunction)(zBehaviour.SoundDynamics), SoundManager.Instance.SfxChannel);
                }
            }
            void ZiplineStopsound()
            {
                if (SoundManager.Instance.HasNamedSound(ziplineSoundId)) SoundManager.Instance.StopNamedSound(ziplineSoundId);
            }

            //REMOVED: キル中ならキルアニメーションが終わるまで待つ (FakePlayerにキルモーションはないためカット)

            //PreparePlayerForZipline ここから
            ResetMoveState();
            //player.moveable = false;
            //player.ForceKillTimerContinue = true;
            UpdateNetworkTransformState(false);
            Body.isKinematic = true;
            ClearPositionQueues();
            player.vanilla_inMovingPlat = true;
            //PreparePlayerForZipline ここまで

            yield return WalkPlayerTo(start.position, 0.001f, 1f, true);
            player.displayPlayer.Cosmetics.TogglePetVisible(false);
            HandZiplinePoolable currentHand = zBehaviour.GetHand();
            Transform handTransform = currentHand.transform;
            currentHand.SetPlayerColor((player as IPlayerlike).RealPlayer!.CurrentOutfit.outfit, PlayerMaterial.MaskType.None, 1f);
            ZiplinePlaySound(zBehaviour.attachSound, start.position);

            //CoAnimatePlayerJumpingOnToZipline ここから
            player.additionalRenderers.Add(currentHand.handRenderer);
            currentHand.gameObject.SetActive(true);
            AnimationCurve animationCurve;
            if (fromTop)
            {
                currentHand.StartDownAnimation();
                handTransform.position = zBehaviour.upHandPosition.position;
                animationCurve = zBehaviour.jumpZiplineCurve;
            }
            else
            {
                currentHand.StartUpAnimation();
                handTransform.position = zBehaviour.downHandPosition.position;
                animationCurve = zBehaviour.jumpZiplineCurveBottom;
            }
            player.displayPlayer.Cosmetics.UpdateBounceHatZipline();
            player.displayPlayer.Cosmetics.AnimateSkinJump();
            yield return Effects.All(
                Effects.CurvePositionY(player.displayPlayer.transform, animationCurve, zBehaviour.timeJump, 0f),
                Effects.CurvePositionY(handTransform, zBehaviour.jumpZiplineHandCurve, zBehaviour.timeJump, 0f),
                player.displayPlayer.Animations.CoPlayJumpAnimation()
            );
            yield return Effects.Wait(0.1f);
            //player.MyPhysics.enabled = false;
            //CoAnimatePlayerJumpingOnToZipline ここまで
            ZiplineStartSound();

            //CoAnimateZiplineAndPlayer ここから
            float travelSeconds;
            Vector3 handleEndPosition;
            if (fromTop)
            {
                travelSeconds = zBehaviour.downTravelTime;
                handleEndPosition = zBehaviour.dropPositionBottom.position;
            }
            else
            {
                travelSeconds = zBehaviour.upTravelTime;
                handleEndPosition = zBehaviour.dropPositionTop.position;
            }
            Vector3 vector = Vector3.zero;
            Vector3 startPos = Position;
            Vector3 handOffset = handTransform.position - startPos;
            for (float time = 0f; time < travelSeconds; time += Time.deltaTime)
            {
                float num = time / travelSeconds;
                vector.x = Mathn.SmoothStep(startPos.x, handleEndPosition.x, num);
                vector.y = Mathn.SmoothStep(startPos.y, handleEndPosition.y, num);
                vector.z = vector.y / 1000f;
                player.displayPlayer.transform.position = vector;
                vector += handOffset;
                vector.z = handTransform.position.z;
                handTransform.position = vector;
                yield return null;
            }
            //CoAnimateZiplineAndPlayer ここまで

            ZiplineStopsound();
            ZiplinePlaySound(zBehaviour.detachSound, end.position);

            //CoAlightPlayerFromZipline ここから
            //player.MyPhysics.enabled = true;
            player.additionalRenderers.RemoveAll(r => r && r.GetInstanceID() == currentHand.handRenderer.GetInstanceID());

            if (fromTop) currentHand.StartDownOutroAnimation();
            else currentHand.StartUpOutroAnimation();
            
            yield return WalkPlayerTo(zBehaviour.transform.TransformPoint(landing.position), 0.01f, 1f, false);
            player.displayPlayer.Cosmetics.SetPetPosition(player.Position.ToUnityVector());
            player.displayPlayer.Cosmetics.TogglePetVisible(true);
            //REMOVE: Petの再表示
            yield return Effects.Wait(0.1f);
            //CoAlightPlayerFromZipline ここまで

            //player.NetTransform.Halt();
            //ResetTarget ここから
            player.vanilla_inMovingPlat = false;
            //player.moveable = true;
            //player.NetTransform.enabled = true;
            //player.SetKinematic(false);
            //ResetTarget ここまで

            VibrationManager.ClearAllVibration();

            yield break;
        }

        public IEnumerator UseMovingPlatform(MovingPlatformBehaviour movingPlat, Variable<bool> done)
        {
            done.Value = false;
            RequestMovingPlatform.Invoke(player.id);
            while (player.MovingPlatState == MovingPlatformState.None) yield return null;
            if (player.MovingPlatState == MovingPlatformState.NotAllowed)
            {
                player.MovingPlatState = MovingPlatformState.Failed;
                yield break;
            }

            RpcSendUseMovingPlat.Invoke(this.player.id);
            yield return UseMovePlatformImpl(movingPlat);
            done.Value = player.MovingPlatState == MovingPlatformState.Done;
        }

        static readonly private RemoteProcess<int> RpcSendUseMovingPlat = new("FakeSendUseMovingPlat", (message, calledByMe) =>
        {
            if (!calledByMe)
            {
                var airship = ShipStatus.Instance.TryCast<AirshipStatus>();
                if (airship)
                {
                    NebulaManager.Instance.StartCoroutine((NebulaGameManager.Instance?.GetPlayerlike(message) as FakePlayer)?.Logics.UseMovePlatformImpl(airship.GapPlatform));   
                }
            }
        });

        private IEnumerator UseMovePlatformImpl(MovingPlatformBehaviour movingPlatform)
        {
            movingPlatform.Target = (player as IFakePlayer).RealPlayer.VanillaPlayer;
            ResetMoveState();
            UpdateNetworkTransformState(false);
            ClearPositionQueues();
            Body.isKinematic = true;
            player.vanilla_inMovingPlat = true;
            
            Vector3 vector = (movingPlatform.IsLeft ? movingPlatform.LeftUsePosition : movingPlatform.RightUsePosition);
            Vector3 vector2 = ((!movingPlatform.IsLeft) ? movingPlatform.LeftUsePosition : movingPlatform.RightUsePosition);
            Vector3 sourcePos = (movingPlatform.IsLeft ? movingPlatform.LeftPosition : movingPlatform.RightPosition);
            Vector3 targetPos = ((!movingPlatform.IsLeft) ? movingPlatform.LeftPosition : movingPlatform.RightPosition);
            Vector3 vector3 = movingPlatform.transform.parent.TransformPoint(vector);
            Vector3 worldUseTargetPos = movingPlatform.transform.parent.TransformPoint(vector2);
            Vector3 worldSourcePos = movingPlatform.transform.parent.TransformPoint(sourcePos);
            Vector3 worldTargetPos = movingPlatform.transform.parent.TransformPoint(targetPos);
            yield return WalkPlayerTo(vector3, 0.01f, 1f, false);
            yield return WalkPlayerTo(worldSourcePos, 0.01f, 1f, false);
            yield return Effects.Wait(0.1f);
            worldSourcePos -= (Vector3)player.collider.offset;
            worldTargetPos -= (Vector3)player.collider.offset;
            if (Constants.ShouldPlaySfx())
            {
                SoundManager.Instance.PlayDynamicSound("PlatformMoving", movingPlatform.MovingSound, true, (DynamicSound.GetDynamicsFunction)movingPlatform.SoundDynamics, SoundManager.Instance.SfxChannel);
            }
            movingPlatform.IsLeft = !movingPlatform.IsLeft;
            yield return Effects.All(
                Effects.Slide2D(movingPlatform.transform, sourcePos, targetPos, PlayerModInfo.OriginalSpeed),
                Effects.Slide2DWorld(player.displayPlayer.transform, worldSourcePos, worldTargetPos, PlayerModInfo.OriginalSpeed)
            );
            if (Constants.ShouldPlaySfx()) SoundManager.Instance.StopNamedSound("PlatformMoving");
            
            yield return WalkPlayerTo(worldUseTargetPos, 0.01f, 1f, false);
            player.displayPlayer.Cosmetics.SetPetPosition(player.Position.ToUnityVector());
            player.vanilla_inMovingPlat = false;
            UpdateNetworkTransformState(true);
            Body.isKinematic = false;
            Halt();
            yield return Effects.Wait(0.1f);
            movingPlatform.Target = null;

            player.MovingPlatState = MovingPlatformState.Done;

            yield break;
        }

        protected IEnumerator WalkPlayerTo(Vector2 worldPos, float tolerance = 0.01f, float speedMul = 1f, bool ignoreColliderOffset = false)
        {
            if (!ignoreColliderOffset)
            {
                worldPos -= player.collider.offset;
            }

            Vector2 del = worldPos - Position;
            while (del.sqrMagnitude > tolerance)
            {
                float num = Mathn.Clamp(del.magnitude * 2f, 0.05f, 1f);
                player.body.velocity = del.normalized * PlayerModInfo.OriginalSpeed * num * speedMul;
                yield return null;
                if (player.body.velocity.magnitude < 0.005f && (double)del.sqrMagnitude < 0.1)
                {
                    break;
                }
                del = worldPos - Position;
            }
            del = default(Vector2);
            player.body.velocity = Vector2.zero;
            yield break;
        }

        public void SetMovement(bool canMove)
        {
            ResetMoveState();
            UpdateNetworkTransformState(canMove);
            Halt();
        }

        public void SnapTo(Vector2 position)
        {
            player.displayPlayer.transform.position = position.AsVector3(position.y / 1000f);
        }

        public void ClearPositionQueues()
        {
            player.NetTransform.ClearPositionQueues();
        }

        public void UpdateNetworkTransformState(bool enabled)
        {
            player.NetTransform.enabled = enabled;
        }

        public bool InVent => false;
    }

    public bool FlipX { get => displayPlayer.Cosmetics.FlipX; private set => displayPlayer.Cosmetics.SetFlipXWithoutPet(value); }

    void OnUpdateRealOutfit(PlayerOutfitChangeEvent ev)
    {
        if (ev.Player == visualPlayer) CheckAndUpdateOutfit(displayPlayer.Cosmetics.GetPetPosition());
    }

    void UpdateAnimation(GameUpdateEvent ev){
        var animations = displayPlayer.Animations;
        var cosmetics = displayPlayer.Cosmetics;
        if (animations.IsPlayingSpawnAnimation()) return;
        //if (this.DoingCustomAnimation) return;
        
        if (!GameData.Instance) return;

        body.transform.SetWorldZ(body.transform.position.y / 1000f);

        Vector2 velocity = this.body.velocity;
        if (animations.IsPlayingClimbAnimation()) return;
        
        if (!isDead)
        {
            if (velocity.sqrMagnitude >= 0.05f)
            {
                bool flipX = this.FlipX;
                if (velocity.x < -0.01f) FlipX = true;
                else if (velocity.x > 0.01f) FlipX = false;
                
                if (!animations.IsPlayingRunAnimation() || flipX != this.FlipX || !cosmetics.IsSkinPlayingRunAnim())
                {
                    animations.PlayRunAnimation();
                    cosmetics.AnimateSkinRun();
                    return;
                }
            }
            else if (animations.IsPlayingRunAnimation() || animations.IsPlayingSpawnAnimation() || !animations.IsPlayingSomeAnimation())
            {
                cosmetics.AnimateSkinIdle();
                animations.PlayIdleAnimation();
                //SetHatAndVisorAlpha(1f);
                return;
            }
        }
        else
        {
            cosmetics.SetGhost();
            
            //REMOVE: GuardianAngelの場合の処理
            if (!animations.IsPlayingGhostIdleAnimation())
            {
                animations.PlayGhostIdleAnimation();
                //SetHatAndVisorAlpha(0.5f);
            }
            if (velocity.x < -0.01f) FlipX = true;
            else if (velocity.x > 0.01f) FlipX = false;
        }

        PlayerModInfo.UpdateNameTextTransform(cosmetics.nameText.transform.parent);

        UpdateVisibility(true, false, true);
    }

    bool IsInShadowCache = false;
    private void SetPlayerAlpha(float alpha, float bodyAlpha)
    {
        var cosmetics = displayPlayer.Cosmetics;
        var color = Color.white.AlphaMultiplied(alpha);
        if (cosmetics.currentBodySprite.BodySprite != null) cosmetics.currentBodySprite.BodySprite.color = Color.white.AlphaMultiplied(bodyAlpha);

        if (cosmetics.skin.layer != null) cosmetics.skin.layer.color = color;

        if (cosmetics.hat)
        {
            if (cosmetics.hat.FrontLayer != null) cosmetics.hat.FrontLayer.color = color;
            if (cosmetics.hat.BackLayer != null) cosmetics.hat.BackLayer.color = color;
        }

        if (cosmetics.visor != null) cosmetics.visor.Image.color = color;

        cosmetics.GetComponent<NebulaCosmeticsLayer>().AdditionalRenderers().Do(r => r.color = color);
        foreach (var r in additionalRenderers) if (r) r.color = color;
    }
    public void UpdateVisibility(bool update, bool ignoreShadow = false, bool showNameText = false)
    {
        var cosmetics = displayPlayer.Cosmetics;
        try
        {
            if (IsDead)
            {
                SetPlayerAlpha(0.5f, 1f);
                return;
            }

            //生存している場合
            if (update)
            {
                bool isInShadow = false;
                if (!GamePlayer.LocalPlayer!.IsDead)
                {
                    //自身も生存している場合、影の中にいるプレイヤーは見えないようにする

                    int shadowMask = Constants.ShadowMask;
                    int objectMask = Constants.ShipAndAllObjectsMask;

                    var light = PlayerControl.LocalPlayer.lightSource;
                    Vector2 pos = light.transform.position;
                    Vector2 myPos = Position;

                    var isAcrossWalls = PlayerModInfo.VisibilityCheckVectors.All(v => Helpers.AnyNonTriggersBetween(pos, myPos + v * 0.22f, out _, objectMask));

                    var mag = isAcrossWalls ? 0.22f : 0.4f;

                    //いずれかの追加ライトの範囲内にいない場合
                    if (!LightInfo.AllLightInfo.Any(info => PlayerModInfo.VisibilityCheckVectors.Any(vec => info.CheckPoint(vec))))
                    {
                        isInShadow = PlayerModInfo.VisibilityCheckVectors.All(v => Helpers.AnyCustomNonTriggersBetween(pos, myPos + v * mag,
                            collider => LightSource.OneWayShadows.TryGetValue(collider.gameObject, out var oneWayShadows) ? !oneWayShadows.IsIgnored(light) : true,
                            shadowMask));
                    }
                }

                IsInShadowCache = isInShadow;
            }

            var shadowHidesPlayer = !ignoreShadow && IsInShadowCache;
            displayPlayer.Cosmetics.nameText.transform.parent.gameObject.SetActive(!shadowHidesPlayer && showNameText);

            float immadiateAlpha = 0 switch { 2 => 0f, 1 => 0.25f, _ => 1f };
            float alpha = Mathn.Min(immadiateAlpha, shadowHidesPlayer ? 0f : 1f);

            SetPlayerAlpha(alpha, alpha);
        }
        catch (Exception e) { }
    }

}

internal class FakePlayerController : FakePlayer, ILifespan
{
    //自身が管理者なら関係する寿命を紐づけられる。
    private ILifespan? relatedLifespan = null;

    private FakePlayerController(GamePlayer visualPlayer, FakePlayerParameters parameters, IFakePlayerSpawnStrategy spawnStrategy) 
        : base(true, ModSingleton<FakePlayerManager>.Instance.GenerateAvailableId(), visualPlayer, parameters, spawnStrategy)
    {
    }

    static public FakePlayerController SpawnSyncFakePlayer(GamePlayer visualPlayer, FakePlayerParameters parameters) => new FakePlayerController(visualPlayer, parameters, new OnlineOwnerFakePlayerSpawnStrategy());

    /// <summary>
    /// 自身が管理者であれば、寿命を関連付けられます。
    /// 明示的に開放するか、紐づけた寿命が尽きれば寿命が尽きます。
    /// </summary>
    /// <param name="lifespan"></param>
    public FakePlayerController BindLifespan(ILifespan lifespan)
    { 
        if(!IsDeadObject) relatedLifespan = lifespan;
        return this;
    }

    public override bool IsDeadObject => isDeadObject || (relatedLifespan?.IsDeadObject ?? false);
}


internal interface IFakePlayerSpawnStrategy
{
    void OnSpawn(FakePlayer player, FakePlayerParameters parameters);
    void OnDespawn(FakePlayer player, FakePlayerParameters parameters);
}

internal class LocalOnlyFakePlayerSpawnStrategy : IFakePlayerSpawnStrategy
{
    void IFakePlayerSpawnStrategy.OnDespawn(FakePlayer player, FakePlayerParameters parameters)
    {
    }

    void IFakePlayerSpawnStrategy.OnSpawn(FakePlayer player, FakePlayerParameters parameters)
    {
    }
}

internal class OnlineOwnerFakePlayerSpawnStrategy : IFakePlayerSpawnStrategy
{
    void IFakePlayerSpawnStrategy.OnDespawn(FakePlayer player, FakePlayerParameters parameters)
    {
        IFakePlayer fp = player;
        FakePlayer.RpcDespawnFakePlayer.Invoke(fp.PlayerlikeId);
    }

    void IFakePlayerSpawnStrategy.OnSpawn(FakePlayer player, FakePlayerParameters parameters)
    {
        IFakePlayer fp = player;
        FakePlayer.RpcSpawnFakePlayer.Invoke((fp.PlayerlikeId, fp.RealPlayer, parameters));
    }
}

internal class OnlineNonOwnerFakePlayerSpawnStrategy : IFakePlayerSpawnStrategy
{
    void IFakePlayerSpawnStrategy.OnDespawn(FakePlayer player, FakePlayerParameters parameters)
    {
    }

    void IFakePlayerSpawnStrategy.OnSpawn(FakePlayer player, FakePlayerParameters parameters)
    {
    }
}
