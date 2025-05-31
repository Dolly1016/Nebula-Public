using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils;
using Epic.OnlineServices.Mods;
using Hazel;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Player;
using TMPro;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Extensions;

[NebulaRPCHolder]
public static class PlayerExtension
{

    public static IEnumerator CoDive(this PlayerControl player, bool playAnim)
    {
        if (!playAnim)
        {
            player.MyPhysics.myPlayer.Visible = false;
            player.GetModInfo()!.Unbox().CurrentDiving = new();
            yield break;
        }

        player.MyPhysics.body.velocity = Vector2.zero;
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = true;
        player.cosmetics.skin.SetEnterVent(player.cosmetics.FlipX);
        player.moveable = false;

        player.NetTransform.SetPaused(true);

        yield return player.MyPhysics.Animations.CoPlayEnterVentAnimation(0);

        player.NetTransform.SetPaused(false);
        player.MyPhysics.myPlayer.Visible = false;
        player.cosmetics.skin.SetIdle(player.cosmetics.FlipX);
        player.MyPhysics.Animations.PlayIdleAnimation();
        player.moveable = true;

        player.currentRoleAnimations.ForEach((Action<RoleEffectAnimation>)((an) => an.ToggleRenderer(false)));
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = false;
        player.GetModInfo()!.Unbox().CurrentDiving = new();
    }

    public static IEnumerator CoGush(this PlayerControl player, bool playAnim)
    {
        if (!playAnim)
        {
            player.MyPhysics.myPlayer.Visible = true;
            player.GetModInfo()!.Unbox().CurrentDiving = null;
            yield break;
        }

        player.MyPhysics.body.velocity = Vector2.zero;
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = true;
        player.moveable = false;
        player.MyPhysics.myPlayer.Visible = true;
        player.cosmetics.AnimateSkinExitVent();

        player.GetModInfo()!.Unbox().CurrentDiving = null;

        player.NetTransform.SetPaused(true);

        yield return player.MyPhysics.Animations.CoPlayExitVentAnimation();

        player.NetTransform.SetPaused(false);

        player.cosmetics.AnimateSkinIdle();
        player.MyPhysics.Animations.PlayIdleAnimation();
        player.moveable = true;
        player.currentRoleAnimations.ForEach((Action<RoleEffectAnimation>)((an) => an.ToggleRenderer(true)));
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = false;
    }

    public static void HaltSmoothly(this CustomNetworkTransform netTransform)
    {
        ushort minSid = (ushort)(netTransform.lastSequenceId + 1);
        netTransform.SnapToSmoothly(netTransform.transform.position);
    }

    public static void SnapToSmoothly(this CustomNetworkTransform netTransform, Vector2 position)
    {
        //netTransform.ClearPositionQueues();

        Transform transform = netTransform.transform;
        netTransform.body.position = position;
        transform.position = position;
        netTransform.body.velocity = Vector2.zero;

        netTransform.sendQueue.Enqueue(position);
        netTransform.SetDirtyBit(2U);
    }

    public static void StopAllAnimations(this CosmeticsLayer layer)
    {
        try
        {
            if (layer.skin.animator) layer.skin.animator.Stop();
            if (layer.currentPet.animator) layer.currentPet.animator.Stop();
        }
        catch { }
    }

    static RemoteProcess<(byte exiledId, byte sourceId, CommunicableTextTag stateId, CommunicableTextTag recordId)> RpcMarkAsExtraVictim = new(
        "MarkAsExtraVictim",
        (message, _) => MeetingHudExtension.ExtraVictims.Add(message)
        );

    static public void ModMarkAsExtraVictim(this PlayerControl exiled,PlayerControl? source, CommunicableTextTag playerState, CommunicableTextTag recordState)
    {
        RpcMarkAsExtraVictim.Invoke((exiled.PlayerId, source?.PlayerId ?? byte.MaxValue, playerState, recordState));

    }

    static public void ModDive(this PlayerControl player, bool isDive = true, bool playAnim = true)
    {
        RpcDive.Invoke((player.PlayerId,isDive, playAnim));
    }

    static RemoteProcess<(byte sourceId, byte targetId, Vector2 revivePos, bool cleanDeadBody,bool recordEvent)> RpcRivive = new(
        "Revive",
        (message, calledByMe) =>
        {
            var player = Helpers.GetPlayer(message.targetId);
            if (!player) return;

            player!.Revive();
            player.NetTransform.SnapTo(message.revivePos);
            var modinfo = player.GetModInfo()!.Unbox();
            modinfo.MyState = PlayerState.Revived;
            modinfo.WillDie = false;
            modinfo.CurrentDiving = null;

            if (message.cleanDeadBody) foreach (var d in Helpers.AllDeadBodies()) if (d.ParentId == player.PlayerId) GameObject.Destroy(d.gameObject);

            if(message.recordEvent)NebulaGameManager.Instance?.GameStatistics.RecordEvent(new(GameStatistics.EventVariation.Revive, message.sourceId != byte.MaxValue ? message.sourceId : null, 1 << message.targetId) { RelatedTag = EventDetail.Revive });

            GameOperatorManager.Instance?.Run(new PlayerReviveEvent(modinfo, NebulaGameManager.Instance?.GetPlayer(message.sourceId)));
        }
        );

    static RemoteProcess<(byte playerId, bool isDive, bool playAnim)> RpcDive = new(
        "Dive",
        (message, _) =>
        {
            var player = Helpers.GetPlayer(message.playerId);
            if (!player) return;
            player?.StartCoroutine(message.isDive ? player.CoDive(message.playAnim) : player.CoGush(message.playAnim));
        }
        );

    static public void ModRevive(this PlayerControl player, Vector2 pos, bool cleanDeadBody,bool recordEvent)
    {
        RpcRivive.Invoke((byte.MaxValue, player.PlayerId, pos, cleanDeadBody,recordEvent));
    }

    static public void ModRevive(this PlayerControl player, PlayerControl? healer, Vector2 pos, bool cleanDeadBody, bool recordEvent = true)
    {
        RpcRivive.Invoke((healer?.PlayerId ?? byte.MaxValue, player.PlayerId, pos, cleanDeadBody, recordEvent));
    }

    static public ModTitleShower GetTitleShower(this PlayerControl player)
    {
        if (player.TryGetComponent<ModTitleShower>(out var result))
            return result;
        else
        {
            return player.gameObject.AddComponent<ModTitleShower>();
        }
    }

    static public void ResetOnDying(PlayerControl player)
    {
        var modInfo = player.GetModInfo()!;
        modInfo.Unbox().CurrentDiving = null;

        if (player.AmOwner)
        {
            if(Vent.currentVent != null)
            {
                Vent.currentVent.SetButtons(false);
                Vent.currentVent = null;
            }
        }

        player.inVent = false;
        player.moveable = true;
    }
}
