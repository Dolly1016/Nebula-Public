using Virial.Events.Game;

namespace Nebula.Extensions;

public static class KillAnimationExtension
{
    static public IEnumerator CoPerformModKill(this KillAnimation killAnim, PlayerControl source, PlayerControl target,bool blink)
    {
        FollowerCamera cam = Camera.main.GetComponent<FollowerCamera>();
        bool isParticipant = PlayerControl.LocalPlayer == source || PlayerControl.LocalPlayer == target;
        PlayerPhysics sourcePhys = source.MyPhysics;
        if (blink) KillAnimation.SetMovement(source, false);
        KillAnimation.SetMovement(target, false);
        DeadBody deadBody = GameObject.Instantiate<DeadBody>(GameManager.Instance.DeadBodyPrefab);
        deadBody.enabled = false;
        deadBody.ParentId = target.PlayerId;
        foreach(var r in deadBody.bodyRenderers) target.SetPlayerMaterialColors(r);
        
        target.SetPlayerMaterialColors(deadBody.bloodSplatter);

        //死体の発生場所を決定 (移動中は移動後の位置に発生)
        Vector2 deadBodyPlayerPos = target.transform.position;
        if(target.inMovingPlat || target.onLadder) deadBodyPlayerPos = target.GetModInfo()?.Unbox().GoalPos ?? deadBodyPlayerPos;

        Vector3 vector = (Vector3)deadBodyPlayerPos + killAnim.BodyOffset;
        vector.z = vector.y / 1000f;
        deadBody.transform.position = vector;
        if (isParticipant)
        {
            cam.Locked = true;
            ConsoleJoystick.SetMode_Task();
            if (PlayerControl.LocalPlayer.AmOwner)
            {
                PlayerControl.LocalPlayer.MyPhysics.inputHandler.enabled = true;
            }
        }

        GameOperatorManager.Instance?.Run(new DeadBodyInstantiateEvent(target.GetModInfo(), deadBody));
        target.GetModInfo()!.Unbox().relatedDeadBodyCache = deadBody;

        target.Die(DeathReason.Kill, false);
        yield return source.MyPhysics.Animations.CoPlayCustomAnimation(killAnim.BlurAnim);

        if (blink)
        {
            source.NetTransform.SnapTo(target.transform.position);
            sourcePhys.Animations.PlayIdleAnimation();
        }

        KillAnimation.SetMovement(source, true);
        KillAnimation.SetMovement(target, true);
        deadBody.enabled = true;
        if (isParticipant)
        {
            cam.Locked = false;
        }
        yield break;
    }
}
