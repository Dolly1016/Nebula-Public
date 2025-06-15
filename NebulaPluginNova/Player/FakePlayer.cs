using AmongUs.GameOptions;
using Hazel;
using InnerNet.GizmoHelpers;
using NAudio.Codecs;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Rewired.UI.ControlMapper;
using Rewired.Utils.Classes.Data;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;
using static Rewired.Demos.CustomPlatform.MyPlatformControllerExtension;

namespace Nebula.Player;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class FakePlayerIdGenerator : AbstractModule<Virial.Game.Game>
{
    static public void Preprocess(NebulaPreprocessor preprocess) => DIManager.Instance.RegisterModule(() => new FakePlayerIdGenerator());
    
    private int AvailableId = 1;

    private FakePlayerIdGenerator()
    {
        ModSingleton<FakePlayerIdGenerator>.Instance = this;
    }

    public int GenerateAvailableId() => (AvailableId++) << 8 + GamePlayer.LocalPlayer!.PlayerId;
}

internal class FakePlayerNetTransform : IGameOperator
{
    Rigidbody2D body;
    bool amOwner;
    bool isActive;

    public FakePlayerNetTransform(Rigidbody2D body, FakePlayer player, bool amOwner)
    {
        this.body = body;
        this.player = player;
        this.amOwner = amOwner;

        this.lastPosSent = body.transform.position;
        this.incomingPosQueue.Enqueue(body.transform.position);
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

    public void RpcSnapTo(Vector2 position)
    {
        if (AmongUsClient.Instance.AmClient)
        {
            this.SnapTo(position, (ushort)(lastSequenceId + 1));
        }
        ushort num = (ushort)(lastSequenceId + 2);
        /*
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(this.NetId, 21, 1, -1);
        NetHelpers.WriteVector2(position, messageWriter);
        messageWriter.Write(num);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        */
    }

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

    private void FixedUpdate()
    {
        if (this.isPaused) return;

        if (amOwner)
        {
            if (this.HasMoved())
            {
                this.sendQueue.Enqueue(this.body.position);
                return;
            }
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

    //TODO RpcSnapToに相当するメソッド

    public bool Serialize(MessageWriter writer, bool initialState)
    {
        if (this.isPaused)
        {
            return false;
        }
        if (initialState)
        {
            writer.Write(this.lastSequenceId);
            NetHelpers.WriteVector2(this.body.position, writer);
            return true;
        }
        
        if (sendQueue.Count == 0) sendQueue.Enqueue(lastPosSent);

        lastSequenceId += 1;
        writer.Write(lastSequenceId);
        ushort num = (ushort)sendQueue.Count;
        writer.WritePacked((int)num);
        foreach (Vector2 vector in sendQueue)
        {
            NetHelpers.WriteVector2(vector, writer);
            lastPosSent = vector;
        }
        sendQueue.Clear();
        lastSequenceId += (ushort)(num - 1);
        return true;
    }
    public void Deserialize(MessageReader reader, bool initialState)
    {
        if (isPaused) return;
        
        if (initialState)
        {
            lastSequenceId = reader.ReadUInt16();
            body.transform.position = NetHelpers.ReadVector2(reader);
            incomingPosQueue.Clear();
            incomingPosQueue.Enqueue(body.transform.position);
            return;
        }
        if (amOwner) return;
        
        ushort num = reader.ReadUInt16();
        int num2 = reader.ReadPackedInt32();
        Vector2 vector;
        if (this.incomingPosQueue.Count > 0)
        {
            vector = incomingPosQueue.Last<Vector2>();
        }
        else
        {
            vector = body.position;
        }
        for (int i = 0; i < num2; i++)
        {
            ushort num3 = (ushort)((int)num + i);
            Vector2 vector2 = NetHelpers.ReadVector2(reader);
            if (NetHelpers.SidGreaterThan(num3, lastSequenceId))
            {
                lastSequenceId = num3;
                incomingPosQueue.Enqueue(vector2);
                vector = vector2;
            }
        }
        if (IsInMiddleOfAnimationThatMakesPlayerInvisible()) tempSnapPosition = new Vector2?(vector);
        else tempSnapPosition = null;
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
        return Mathf.Abs(num - (num3 + num2)) < REPLAY_POSITION_THRESHOLD;
    }

    private void SetMovementSmoothingModifier()
    {
        float num = ((incomingPosQueue.Count <= QUEUE_THRESHOLD_FOR_SMOOTHING) ? SMOOTHING_BAND_MODIFIER : NEUTRAL_BAND_MODIFIER);
        this.rubberbandModifier = Mathf.Lerp(this.rubberbandModifier, num, Time.fixedDeltaTime * SMOOTHING_LERP_RATE);
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
    private ushort lastSequenceId;
    private Vector2 lastPosition;
    private Vector2 lastPosSent;
    private Vector2? tempSnapPosition;
}

[NebulaRPCHolder]
internal record FakePlayerParameters(bool AllowKill, bool CanBeTarget)
{
    static FakePlayerParameters()
    {
        RemoteProcessAsset.RegisterType<FakePlayerParameters>((writer, parameters) =>
        {
            writer.Write(parameters.AllowKill);
            writer.Write(parameters.CanBeTarget);
        }, (reader) =>
        {
            return new(reader.ReadBoolean(), reader.ReadBoolean());
        });
    }
}

[NebulaRPCHolder]
internal class FakePlayer : AbstractModuleContainer, IFakePlayer, ILifespan, IGameOperator
{
    protected readonly PlayerDisplay displayPlayer;
    protected readonly Collider2D collider;
    protected readonly Rigidbody2D body;
    private readonly int id;
    private readonly GamePlayer visualPlayer;
    private bool isDead;
    private bool allowKill;
    private bool canBeTarget;
    private bool amOwner;
    internal readonly FakePlayerNetTransform NetTransform;

    protected bool isDeadObject { get; private set; } = false;
    private void Release() => isDeadObject = true;

    static FakePlayer()
    {
        RemoteProcessAsset.RegisterType<IPlayerlike>((writer, player) => writer.Write(player?.PlayerlikeId ?? -1), (reader) => NebulaGameManager.Instance?.GetPlayerlike(reader.ReadInt32())!);
    }

    protected FakePlayer(Vector2 position, bool amOwner, int id, GamePlayer visualPlayer, FakePlayerParameters parameters, IFakePlayerSpawnStrategy spawnStrategy)
    {
        this.id = id;
        this.displayPlayer = VanillaAsset.GetPlayerDisplay(true, true);
        this.displayPlayer.transform.position = position.AsVector3(position.y / 1000f);
        this.displayPlayer.Cosmetics.GetComponent<NebulaCosmeticsLayer>().fakePlayerCache = this;
        this.collider = displayPlayer.GetComponent<Collider2D>();
        this.body = displayPlayer.GetComponent<Rigidbody2D>();
        this.visualPlayer = visualPlayer;
        this.amOwner = amOwner;
        this.NetTransform = new FakePlayerNetTransform(body, this, amOwner).Register(this);

        this.allowKill = parameters.AllowKill;
        this.canBeTarget = parameters.CanBeTarget;

        //スポーン & デスポーン
        GameOperatorManager.Instance?.RegisterOnReleased(() =>
        {
            GameObject.Destroy(displayPlayer.gameObject);
            NebulaGameManager.Instance?.RemovePlayerlike(this);
            spawnStrategy.OnDespawn(this, parameters);
        }, this);
        spawnStrategy.OnSpawn(this, parameters);

        this.Register(this);

        UpdateOutfit(visualPlayer.CurrentOutfit.outfit);
    }

    static public FakePlayer SpawnLocalFakePlayer(Vector2 position, GamePlayer visualPlayer, FakePlayerParameters parameters) => new FakePlayer(position, true, ModSingleton<FakePlayerIdGenerator>.Instance.GenerateAvailableId(), visualPlayer, parameters, new LocalOnlyFakePlayerSpawnStrategy());

    int IPlayerlike.PlayerlikeId => id;

    GamePlayer? IPlayerlike.VisualPlayer => visualPlayer;

    string IPlayerlike.Name => visualPlayer.Name;
    public bool AmOwner => amOwner;
    public bool IsDead => isDead;

    public bool AllowKill => allowKill;

    public bool CanBeTarget => canBeTarget;

    public Virial.Compat.Vector2 TruePosition => new((Vector2)displayPlayer.transform.position + collider.offset);

    public Virial.Compat.Vector2 Position => new(displayPlayer.transform.position);

    public virtual bool IsDeadObject => isDeadObject;

    //Vanillaの挙動を模倣するためのメンバ
    protected bool vanilla_inMovingPlat = false;
    protected bool vanilla_onLadder = false;

    private List<SpriteRenderer> additionalRenderers = [];
    CosmeticsLayer IPlayerlike.VanillaCosmetics => displayPlayer.Cosmetics;

    internal static readonly RemoteProcess<(int id, GamePlayer? visualPlayer, FakePlayerParameters parameters, Vector2 position)> RpcSpawnFakePlayer = new("SendSpawnFakePlayer",
    (message, calledByMe) =>
    {
        if (!calledByMe)
        {
            new FakePlayer(message.position, false, message.id, message.visualPlayer, message.parameters, new OnlineNonOwnerFakePlayerSpawnStrategy());
        }
    });

    internal static readonly RemoteProcess<int> RpcDespawnFakePlayer = new("DespawnFakePlayer", (id, _) => (NebulaGameManager.Instance?.GetPlayerlike(id) as FakePlayer)?.Release());

    void UpdateOutfit(NetworkedPlayerInfo.PlayerOutfit outfit)
    {
        var animations = displayPlayer.Animations;
        var cosmetics = displayPlayer.Cosmetics;

        //PlayerControl.SetOutfit
        cosmetics.SetName(outfit.PlayerName);
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

        /*
        cosmetics.SetPetIdle(outfit.outfit.PetId, outfit.outfit.ColorId, delegate
        {
            //cosmetics.SetPetSource(this);
            if (vanilla_inMovingPlat) cosmetics.SetPetVisible(false);
            
        });
        */
        cosmetics.Visible = !IsDead || GamePlayer.LocalPlayer!.IsDead;
    }

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

        public void Halt()
        {
            //TODO
        }

        public IPlayerlike Player => player;

        float IPlayerLogics.TrueSpeed { get
            {
                var speed = PlayerModInfo.OriginalSpeed;
                var mod = 1f;
                if (GameManager.Instance != null)
                {
                    mod = GameOptionsManager.Instance.currentNormalGameOptions.PlayerSpeedMod;
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
            //NetTranform.SetPaused(false);
            Body.isKinematic = false;
            player.collider.enabled = true;
            ResetAnimState();
        }
        public IEnumerator UseLadder(Ladder ladder)
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
            //this.myPlayer.SetPetPosition(this.myPlayer.transform.position);
            this.ResetAnimState();
            yield return Effects.Wait(0.1f);

            //this.myPlayer.moveable = true;
            UpdateNetworkTransformState(true);
            Body.isKinematic = false;
            player.vanilla_onLadder = false;
        }

        public IEnumerator UseZipline(ZiplineConsole zipline)
        {
            Transform start;
            Transform end;
            Transform landing;
            if (zipline.atTop)
            {
                start = zipline.zipline.handleTop;
                end = zipline.zipline.handleBottom;
                landing = zipline.zipline.landingPositionBottom;
            }
            else
            {
                start = zipline.zipline.handleBottom;
                end = zipline.zipline.handleTop;
                landing = zipline.zipline.landingPositionTop;
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
                    SoundManager.Instance.PlayDynamicSound(ziplineSoundId, zipline.zipline.downSound, false, (DynamicSound.GetDynamicsFunction)(zipline.zipline.SoundDynamics), SoundManager.Instance.SfxChannel);
                    yield return new WaitForSeconds(zipline.zipline.downSound.length - 0.05f);
                    SoundManager.Instance.PlayDynamicSound(ziplineSoundId, zipline.zipline.downLoopSound, true, (DynamicSound.GetDynamicsFunction)(zipline.zipline.SoundDynamics), SoundManager.Instance.SfxChannel);
                }
                if (zipline.zipline.ShouldPlaySound())
                {
                    SoundManager.Instance.PlayDynamicSound(ziplineSoundId, zipline.zipline.upSound, true, (DynamicSound.GetDynamicsFunction)(zipline.zipline.SoundDynamics), SoundManager.Instance.SfxChannel);
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

            //REMOVED: Petがいるなら隠す
            yield return WalkPlayerTo(start.position, 0.001f, 1f, true);
            HandZiplinePoolable currentHand = zipline.zipline.GetHand();
            currentHand.SetPlayerColor((player as IPlayerlike).VisualPlayer!.CurrentOutfit.outfit, PlayerMaterial.MaskType.None, 1f);
            ZiplinePlaySound(zipline.zipline.attachSound, start.position);

            //CoAnimatePlayerJumpingOnToZipline ここから
            player.additionalRenderers.Add(currentHand.handRenderer);
            currentHand.gameObject.SetActive(true);
            AnimationCurve animationCurve;
            if (fromTop)
            {
                currentHand.StartDownAnimation();
                currentHand.transform.position = zipline.zipline.upHandPosition.position;
                animationCurve = zipline.zipline.jumpZiplineCurve;
            }
            else
            {
                currentHand.StartUpAnimation();
                currentHand.transform.position = zipline.zipline.downHandPosition.position;
                animationCurve = zipline.zipline.jumpZiplineCurveBottom;
            }
            player.displayPlayer.Cosmetics.UpdateBounceHatZipline();
            player.displayPlayer.Cosmetics.AnimateSkinJump();
            yield return Effects.All(
                Effects.CurvePositionY(player.displayPlayer.transform, animationCurve, zipline.zipline.timeJump, 0f),
                Effects.CurvePositionY(currentHand.transform, zipline.zipline.jumpZiplineHandCurve, zipline.zipline.timeJump, 0f),
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
                travelSeconds = zipline.zipline.downTravelTime;
                handleEndPosition = zipline.zipline.dropPositionBottom.position;
            }
            else
            {
                travelSeconds = zipline.zipline.upTravelTime;
                handleEndPosition = zipline.zipline.dropPositionTop.position;
            }
            Vector3 vector = Vector3.zero;
            Vector3 startPos = Position;
            Vector3 handOffset = currentHand.transform.position - startPos;
            for (float time = 0f; time < travelSeconds; time += Time.deltaTime)
            {
                float num = time / travelSeconds;
                vector.x = Mathf.SmoothStep(startPos.x, handleEndPosition.x, num);
                vector.y = Mathf.SmoothStep(startPos.y, handleEndPosition.y, num);
                vector.z = vector.y / 1000f;
                player.displayPlayer.transform.position = vector;
                vector += handOffset;
                vector.z = currentHand.transform.position.z;
                currentHand.transform.position = vector;
                yield return null;
            }
            //CoAnimateZiplineAndPlayer ここまで

            ZiplineStopsound();
            ZiplinePlaySound(zipline.zipline.detachSound, end.position);

            //CoAlightPlayerFromZipline ここから
            //player.MyPhysics.enabled = true;
            player.additionalRenderers.RemoveAll(r => r && r.GetInstanceID() == currentHand.handRenderer.GetInstanceID());

            if (fromTop) currentHand.StartDownOutroAnimation();
            else currentHand.StartUpOutroAnimation();
            
            yield return WalkPlayerTo(zipline.zipline.transform.TransformPoint(landing.position), 0.01f, 1f, false);
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

        protected IEnumerator WalkPlayerTo(Vector2 worldPos, float tolerance = 0.01f, float speedMul = 1f, bool ignoreColliderOffset = false)
        {
            if (!ignoreColliderOffset)
            {
                worldPos -= player.collider.offset;
            }

            Vector2 del = worldPos - Position;
            while (del.sqrMagnitude > tolerance)
            {
                float num = Mathf.Clamp(del.magnitude * 2f, 0.05f, 1f);
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

        public void SnapTo(Vector2 position)
        {
            player.displayPlayer.transform.position = position.AsVector3(position.y / 1000f);
        }

        public void ClearPositionQueues()
        {
            //TODO
        }

        public void UpdateNetworkTransformState(bool enabled)
        {
            //TODO
        }
    }

    public bool FlipX { get => displayPlayer.Cosmetics.FlipX; private set => displayPlayer.Cosmetics.SetFlipXWithoutPet(value); } 
        
    void UpdateAnimation(GameUpdateEvent ev){
        var animations = displayPlayer.Animations;
        var cosmetics = displayPlayer.Cosmetics;
        if (animations.IsPlayingSpawnAnimation()) return;
        //if (this.DoingCustomAnimation) return;
        
        if (!GameData.Instance) return;
        
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

        UpdateVisibility(true);
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
            displayPlayer.Cosmetics.nameText.transform.parent.gameObject.SetActive(!shadowHidesPlayer);

            float immadiateAlpha = 0 switch { 2 => 0f, 1 => 0.25f, _ => 1f };
            float alpha = Mathf.Min(immadiateAlpha, shadowHidesPlayer ? 0f : 1f);

            SetPlayerAlpha(alpha, alpha);
        }
        catch (Exception e) { }
    }
}

[NebulaRPCHolder]
internal class FakePlayerController : FakePlayer, ILifespan
{
    //自身が管理者なら関係する寿命を紐づけられる。
    private ILifespan? relatedLifespan = null;

    private FakePlayerController(Vector2 position, GamePlayer visualPlayer, FakePlayerParameters parameters, IFakePlayerSpawnStrategy spawnStrategy) 
        : base(position, true, ModSingleton<FakePlayerIdGenerator>.Instance.GenerateAvailableId(), visualPlayer, parameters, spawnStrategy)
    {
    }

    static public FakePlayerController SpawnSyncFakePlayer(Vector2 position, GamePlayer visualPlayer, FakePlayerParameters parameters) => new FakePlayerController(position, visualPlayer, parameters, new OnlineOwnerFakePlayerSpawnStrategy());

    /// <summary>
    /// 自身が管理者であれば、寿命を関連付けられます。
    /// 明示的に開放するか、紐づけた寿命が尽きれば寿命が尽きます。
    /// </summary>
    /// <param name="lifespan"></param>
    void BindLifespan(ILifespan lifespan)
    { 
        if(!IsDeadObject) relatedLifespan = lifespan;
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
        FakePlayer.RpcSpawnFakePlayer.Invoke((fp.PlayerlikeId, fp.VisualPlayer, parameters, player.Position));
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
