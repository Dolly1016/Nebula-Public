using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;
using static Rewired.Demos.CustomPlatform.MyPlatformControllerExtension;

namespace Nebula.Player;

internal class VanillaPlayerLogics : IPlayerLogics { 
    PlayerControl player;
    Transform playerTransform;
    Rigidbody2D rigidBody;
    Collider2D myCollider;
    PlayerAnimations animations;
    PlayerPhysics physics;
    CustomNetworkTransform netTransform;
    GamePlayer modPlayer;
    public VanillaPlayerLogics(PlayerControl player, GamePlayer modPlayer)
    {
        this.player = player;
        this.playerTransform = player.transform;
        this.modPlayer = modPlayer;

        this.physics = player.MyPhysics;
        this.rigidBody = physics.body;
        this.animations = physics.Animations;
        this.myCollider = player.Collider;
        this.netTransform = player.NetTransform;
    }

    public UnityEngine.Vector2 Position { get => playerTransform.position; set => playerTransform.position = value.AsVector3(value.y / 1000f); }
    public UnityEngine.Vector2 TruePosition => player.GetTruePosition();

    public Collider2D GroundCollider => this.myCollider;

    public PlayerAnimations Animations => this.animations;

    public Rigidbody2D Body => this.rigidBody;

    public IPlayerlike Player => modPlayer;

    public float TrueSpeed => this.physics.TrueSpeed;

    public void ClearPositionQueues() => this.netTransform.ClearPositionQueues();
    public bool InVent => player.inVent;
    public bool InMovingPlat => player.inMovingPlat;
    public bool OnLadder => player.onLadder;

    public void Halt()
    {
        Body.velocity = Vector2.zero;
        ClearPositionQueues();
    }

    public void SnapTo(Vector2 position) => this.netTransform.SnapTo(position);
    

    public void UpdateNetworkTransformState(bool enabled) => this.netTransform.enabled = enabled;
    public void ResetMoveState() => this.physics.ResetMoveState();

    public IEnumerator UseLadder(Ladder ladder)
    {
        this.physics.ResetMoveState(true);
        yield return this.physics.CoClimbLadder(ladder, 0);
    }

    public IEnumerator UseZipline(ZiplineConsole zipline)
    {
        ZiplineBehaviour zipBehaviour = zipline.zipline;
        Transform transform;
        Transform transform2;
        Transform transform3;
        if (zipline.atTop)
        {
            transform = zipBehaviour.handleTop;
            transform2 = zipBehaviour.handleBottom;
            transform3 = zipBehaviour.landingPositionBottom;
        }
        else
        {
            transform = zipBehaviour.handleBottom;
            transform2 = zipBehaviour.handleTop;
            transform3 = zipBehaviour.landingPositionTop;
        }
        yield return zipBehaviour.CoUseZipline(player,
            zipline.atTop ? zipBehaviour.handleTop : zipBehaviour.handleBottom,
            zipline.atTop ? zipBehaviour.handleBottom : zipBehaviour.handleTop,
            zipline.atTop ? zipBehaviour.landingPositionBottom : zipBehaviour.landingPositionTop,
            zipline.atTop);
    }

    public IEnumerator UseMovingPlatform(MovingPlatformBehaviour movingPlatform, Variable<bool> done)
    {
        movingPlatform.Use();
        yield return Effects.Wait(0.5f);
        if (movingPlatform.Target.AsBoolFast(out var target) && target.PlayerId == player.PlayerId)
        {
            done.Value = true;
            while (movingPlatform.Target.AsBoolFast(out var currentTtarget) && currentTtarget.PlayerId == player.PlayerId) yield return null;
            yield break;
        }
        else
        {
            done.Value = false;
            yield break;
        }
    }

    public void SetMovement(bool canMove)
    {
        player.moveable = canMove;
        this.physics.ResetMoveState(false);
        this.netTransform.enabled = canMove;
        this.physics.enabled = canMove;
        this.netTransform.Halt();
    }
}
