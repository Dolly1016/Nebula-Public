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
    GamePlayer modPlayer;
    public VanillaPlayerLogics(PlayerControl player, GamePlayer modPlayer)
    {
        this.player = player;
        this.modPlayer = modPlayer;
    }

    public UnityEngine.Vector2 Position { get => player.transform.position; set => player.transform.position = value.AsVector3(value.y / 1000f); }
    public UnityEngine.Vector2 TruePosition => player.GetTruePosition();

    public Collider2D GroundCollider => player.Collider;

    public PlayerAnimations Animations => player.MyPhysics.Animations;

    public Rigidbody2D Body => player.MyPhysics.body;

    public IPlayerlike Player => modPlayer;

    public float TrueSpeed => player.MyPhysics.TrueSpeed;

    public void ClearPositionQueues() => player.NetTransform.ClearPositionQueues();
    public bool InVent => player.inVent;
    public bool InMovingPlat => player.inMovingPlat;
    public bool OnLadder => player.onLadder;

    public void Halt()
    {
        Body.velocity = Vector2.zero;
        ClearPositionQueues();
    }

    public void SnapTo(Vector2 position) =>  player.NetTransform.SnapTo(position);
    

    public void UpdateNetworkTransformState(bool enabled) =>  player.NetTransform.enabled = enabled;
    public void ResetMoveState() => player.MyPhysics.ResetMoveState();

    public IEnumerator UseLadder(Ladder ladder)
    {
        player.MyPhysics.ResetMoveState(true);
        yield return player.MyPhysics.CoClimbLadder(ladder, 0);
    }

    public IEnumerator UseZipline(ZiplineConsole zipline)
    {
        Transform transform;
        Transform transform2;
        Transform transform3;
        if (zipline.atTop)
        {
            transform = zipline.zipline.handleTop;
            transform2 = zipline.zipline.handleBottom;
            transform3 = zipline.zipline.landingPositionBottom;
        }
        else
        {
            transform = zipline.zipline.handleBottom;
            transform2 = zipline.zipline.handleTop;
            transform3 = zipline.zipline.landingPositionTop;
        }
        yield return zipline.zipline.CoUseZipline(player,
            zipline.atTop ? zipline.zipline.handleTop : zipline.zipline.handleBottom,
            zipline.atTop ? zipline.zipline.handleBottom : zipline.zipline.handleTop,
            zipline.atTop ? zipline.zipline.landingPositionBottom : zipline.zipline.landingPositionTop,
            zipline.atTop);
    }

    public IEnumerator UseMovingPlatform(MovingPlatformBehaviour movingPlatform, Variable<bool> done)
    {
        movingPlatform.Use();
        yield return Effects.Wait(0.5f);
        if (movingPlatform.Target && movingPlatform.Target.PlayerId == player.PlayerId)
        {
            done.Value = true;
            while (movingPlatform.Target && movingPlatform.Target.PlayerId == player.PlayerId) yield return null;
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
        player.MyPhysics.ResetMoveState(false);
        player.NetTransform.enabled = canMove;
        player.MyPhysics.enabled = canMove;
        player.NetTransform.Halt();
    }
}
