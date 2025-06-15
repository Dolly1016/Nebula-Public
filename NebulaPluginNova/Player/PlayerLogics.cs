using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Player;

internal interface IPlayerLogics
{
    UnityEngine.Vector2 Position { get; set; }
    UnityEngine.Vector2 TruePosition { get; }
    Collider2D GroundCollider { get; }
    PlayerAnimations Animations { get; }
    Rigidbody2D Body { get; }
    IPlayerlike Player { get; }
    float TrueSpeed { get; }
    void Halt();
    void SnapTo(Vector2 position);
    void ClearPositionQueues();
    void UpdateNetworkTransformState(bool enabled);
    void SetKinematic(bool kinematic) => Body.isKinematic = kinematic;
    void SetNormalizedVelocity(Vector2 direction) => Body.velocity = direction * this.TrueSpeed;
    void ResetMoveState();
    IEnumerator UseZipline(ZiplineConsole zipline);
    IEnumerator UseLadder(Ladder ladder);
}

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
}
