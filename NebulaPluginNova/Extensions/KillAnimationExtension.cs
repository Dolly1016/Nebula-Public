using TMPro;
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

        var existedBodies = Helpers.AllDeadBodies().ToArray();

        DeadBody deadBody = GameObject.Instantiate<DeadBody>(GameManager.Instance.DeadBodyPrefab);
        deadBody.enabled = false;
        deadBody.ParentId = target.PlayerId;
        foreach(var r in deadBody.bodyRenderers) target.SetPlayerMaterialColors(r);
        
        target.SetPlayerMaterialColors(deadBody.bloodSplatter);

        //死体の発生場所を決定 (移動中は移動後の位置に発生)
        Vector2 deadBodyPlayerPos = target.transform.position;
        if(target.inMovingPlat || target.onLadder) deadBodyPlayerPos = target.GetModInfo()?.Unbox().GoalPos ?? deadBodyPlayerPos;

        Vector3 vector = (Vector3)deadBodyPlayerPos + killAnim.BodyOffset;

        //至近距離に死体がある場合
        if(existedBodies.Any(b => b.transform.position.Distance(vector) < 0.05f))
        {
            void TryShift()
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var dir = Vector2.right.Rotate(15 + 45 * i + 90 * j);
                        var cand = (Vector2)vector + dir * 0.12f;

                        if (
                            !Helpers.AnyNonTriggersBetween(vector, cand, out _, Constants.ShipAndAllObjectsMask) &&
                            !existedBodies.Any(b => b.transform.position.Distance(cand) < 0.05f)
                            )
                        {
                            vector = cand;
                            break;
                        }
                    }
                }
            }
            TryShift();
        }

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
        PlayerExtension.ResetOnDying(target);

        if (blink)
        {
            if (!source.Data.IsDead) yield return source.MyPhysics.Animations.CoPlayCustomAnimation(killAnim.BlurAnim);
            source.NetTransform.SnapTo(target.transform.position);
            sourcePhys.Animations.PlayIdleAnimation();
            KillAnimation.SetMovement(source, true);
        }

        KillAnimation.SetMovement(target, true);

        deadBody.enabled = true;
        if (isParticipant)
        {
            cam.Locked = false;
        }
        yield break;
    }
}
