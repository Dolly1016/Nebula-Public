using Nebula.Game.Statistics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Text;

namespace Nebula.Extensions;

[NebulaRPCHolder]
public static class MeetingHudExtension
{
    //タイマーなど、MeetingHudの拡張
    static public float DiscussionTimer = 0f;
    static public float VotingTimer = 0f;
    static public float LeftTime => DiscussionTimer + VotingTimer;
    static public float ResultTimer = 0f;
    static private int VoteForMask = 0;
    static private int CanVoteMask = 0;
    static private int SealedMask = 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static private bool CanVoteForByMask(byte playerId) => (VoteForMask & (1 << playerId)) != 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static private bool CanUseAbilityForByMask(byte playerId) => (SealedMask & (1 << playerId)) == 0;

    static public bool CanVoteFor(byte playerId) => !(GamePlayer.GetPlayer(playerId)?.IsDead ?? true) && CanVoteForByMask(playerId) && CanUseAbilityForByMask(playerId);
    static public bool CanVoteFor(GamePlayer player) => !player.IsDead && CanVoteForByMask(player.PlayerId) && CanUseAbilityForByMask(player.PlayerId);
    static public bool CanUseAbilityFor(GamePlayer player, bool shouldBeAlive) => CanUseAbility && (!shouldBeAlive || !player.IsDead) && CanUseAbilityForByMask(player.PlayerId);
    static public bool HasVote(byte playerId) => (CanVoteMask & (1 << playerId)) != 0;
    static public bool CanSkip = true;
    static public bool ExileEvenIfTie = false;
    static public bool IsObvious = false;
    static public bool CanShowPhotos = true;
    static public float ActionCoolDown = 0f;
    static public bool CanInvokeSomeAction => !(ActionCoolDown > 0f);
    static public int LastSharedCount = 110;

    //自分自身が投票権を持つ場合、True。
    static public bool CanVote { set; get => field && (CanVoteMask & (1 << GamePlayer.LocalPlayer!.PlayerId)) != 0; }
    //自分自身が会議内で能力を使える場合、True。
    static public bool CanUseAbility = true;

    static public GamePlayer? LastReporter = null;
    //直近の投票の結果吊られるプレイヤー
    static public PlayerControl[]? ExiledAll = null;
    static public GamePlayer[]? ExiledAllModCache = null;
    
    //直近の投票がタイであったかどうか
    static public bool WasTie = false;

    public static void UpdateVotingMask(int mask) => VoteForMask = mask;
    public static void UpdateSealedMask(int mask) => SealedMask = mask;
    public static void AddSealedMask(int mask) => SealedMask |= mask;
    public static void UpdateCanVoteMask(int mask) => CanVoteMask = mask;
    public static void RemoveCanVoteMask(int mask) => CanVoteMask &= ~mask;


    public static void InitMeetingTimer()
    {
        LogicOptionsNormal logicOptionsNormal = GameManager.Instance.LogicOptions.Cast<LogicOptionsNormal>();
        DiscussionTimer = logicOptionsNormal.GetDiscussionTime();
        VotingTimer = logicOptionsNormal.GetVotingTime();
        ResultTimer = 5f;
        VoteForMask = 0xFFFFFFF;
        CanVoteMask = 0xFFFFFFF;
        CanUseAbility = true;
        SealedMask = 0;
        CanSkip = true;
        ExileEvenIfTie = false;
        IsObvious = false;
        ExiledAll = null;
        CanShowPhotos = true;
        ActionCoolDown = 0f;
        LastSharedCount = 110;

        int penalty = (int)(GeneralConfigurations.DeathPenaltyOption * (float)(NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.IsDead) ?? 0f));
        if(GeneralConfigurations.IsInEarlyPhase && GamePlayer.AllPlayers.All(p => !p.IsDead))
        {
            penalty += (int)(float)GeneralConfigurations.EarlyDiscussionReductionOption;
        }

        //DiscussionTimerを引けるだけ引いておいて、引き過ぎた分をVotingTimerに繰り越し
        DiscussionTimer -= penalty;
        if (DiscussionTimer < 0f) VotingTimer += DiscussionTimer;

        //0秒を切らないように調整
        DiscussionTimer = Mathf.Max(0f, DiscussionTimer);
        VotingTimer = Mathf.Max(15f, VotingTimer);
    }

    public static void ReflectVotingMask()
    {
        foreach (var p in MeetingHud.Instance.playerStates)
        {
            if (((1 << p.TargetPlayerId) & VoteForMask) != 0)
                p.SetEnabled();
            else
                p.SetDisabled();
        }

        if (CanSkip && MeetingHud.Instance.CurrentState == MeetingHud.VoteStates.NotVoted && CanVote)
            MeetingHud.Instance.SkipVoteButton.SetEnabled();
        else
            MeetingHud.Instance.SkipVoteButton.SetDisabled();
    }

    public static void UpdatePlayerState(this MeetingHud meetingHud)
    {
        foreach (PlayerVoteArea pva in meetingHud.playerStates)
        {
            bool isDead = NebulaGameManager.Instance?.GetPlayer(pva.TargetPlayerId)?.IsDead ?? true;

            if (pva.AmDead == isDead) continue;

            pva.SetDead(pva.DidReport, isDead);
            pva.Overlay.gameObject.SetActive(isDead);
        }

        ReflectVotingMask();
    }

    public static void ResetPlayerState(this MeetingHud meetingHud)
    {
        if (MeetingHud.Instance.state <= MeetingHud.VoteStates.Discussion) return;

        Reset();

        meetingHud.ClearVote();

        meetingHud.UpdatePlayerState();
        foreach (PlayerVoteArea voter in meetingHud.playerStates)
        {
            voter.ThumbsDown.enabled = false;
            voter.UnsetVote();
        }

        GameOperatorManager.Instance?.Run(new MeetingResetEvent());

        if (AmongUsClient.Instance.AmHost) meetingHud.CheckForEndVoting();
    }

    public static Dictionary<byte, int> WeightMap= new();
    public static Dictionary<byte, int> LastVotedForMap = new();
    public static List<GameObject> LeftContents = new();
    public static List<(byte exiledId, byte sourceId, CommunicableTextTag playerState, CommunicableTextTag eventTag)> ExtraVictims = new();

    public static void Reset()
    {
        LastVotedForMap.Clear();
        WeightMap.Clear();
        ExtraVictims.Clear();
    }

    public static void AddLeftContent(GameObject obj)
    {
        obj.transform.SetParent(MeetingHud.Instance.transform);
        obj.layer = LayerExpansion.GetUILayer();
        obj.transform.localPosition = new Vector3(-4.8f, 1.8f - 1.2f * (float)LeftContents.Count, -40f);
        LeftContents.Add(obj);
        ActionCoolDown = 0.8f;
    }

    public static void ExpandDiscussionTime()
    {
        IEnumerator CoGainDiscussionTime()
        {
            for (int i = 0; i < 10; i++)
            {
                MeetingHudExtension.VotingTimer += 1f;
                MeetingHud.Instance!.lastSecond = 11;
                yield return new WaitForSeconds(0.1f);
            }
        }
        CoGainDiscussionTime().StartOnScene();
    }

    public static void ExileExtraVictims()
    {
        foreach(var victims in ExtraVictims)
        {
            var player = NebulaGameManager.Instance?.GetPlayer(victims.exiledId);
            if (player == null) continue;

            var killer = NebulaGameManager.Instance?.GetPlayer(victims.sourceId);

            player.VanillaPlayer.Exiled();
            player.VanillaPlayer.Data.IsDead = true;
            PlayerExtension.ResetOnDying(player.VanillaPlayer);

            player.Unbox().MyState = victims.playerState;

            //Entityイベント発火
            GameOperatorManager.Instance?.Run(new PlayerExtraExiledEvent(player, killer), true);

            if (player.AmOwner && NebulaAchievementManager.GetRecord("death." + player.PlayerState.TranslationKey, out var recDeath)) new StaticAchievementToken(recDeath);
            if(player.AmOwner) new StaticAchievementToken("stats.death." + player.PlayerState.TranslationKey);

            if ((killer?.AmOwner ?? false) && NebulaAchievementManager.GetRecord("kill." + player.PlayerState.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);
            if (player.AmOwner) new StaticAchievementToken("extraVictim");

            NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, victims.sourceId == byte.MaxValue ? null : victims.sourceId, 1 << victims.exiledId) { RelatedTag = victims.eventTag });
        }

        ExtraVictims.Clear();
    }

    public static bool MarkedAsExtraVictims(byte playerId) => ExtraVictims.Any(c => c.exiledId == playerId);

    public static void ModCastVote(this MeetingHud meeting, byte playerId, byte suspectIdx,int votes)
    {
        RpcModCastVote.Invoke((playerId, suspectIdx, votes));
    }

    public static PlayerVoteArea GetPlayer(this MeetingHud instance, int playerId) => instance.playerStates.FirstOrDefault(p => p.TargetPlayerId == playerId)!;
    public static bool TryGetPlayer(this MeetingHud instance, int playerId, [MaybeNullWhen(false)]out PlayerVoteArea pva)
    {
        pva = instance.playerStates.FirstOrDefault(p => p.TargetPlayerId == playerId);
        return pva != null;
    }

    private static RemoteProcess<(byte source, byte target, int weight)> RpcModCastVote = new(
        "CaseVote",
        (message, _) =>
        {
            WeightMap[message.source] = message.weight;
            if (PlayerControl.LocalPlayer.PlayerId == message.source) MeetingHud.Instance.state = MeetingHud.VoteStates.Voted;

            GameOperatorManager.Instance?.Run(new PlayerVoteCastEvent(GamePlayer.GetPlayer(message.source)!, GamePlayer.GetPlayer(message.target), message.weight));

            if (AmongUsClient.Instance.AmHost)
            {
                //MeetingHud.CastVote
                NetworkedPlayerInfo playerById = GameData.Instance.GetPlayerById(message.source);
                var playerVoteArea = MeetingHud.Instance.GetPlayer(message.source);
                
                if (playerVoteArea != null && !playerVoteArea.AmDead && !playerVoteArea.DidVote)
                {
                    if (playerById.AmOwner || AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame) SoundManager.Instance.PlaySound(MeetingHud.Instance.VoteLockinSound, false, 1f, null);
                    
                    playerVoteArea.SetVote(message.target);
                    MeetingHud.Instance.SetDirtyBit(1U);
                    MeetingHud.Instance.CheckForEndVoting();
                    if(GeneralConfigurations.ShowVoteStateOption) PlayerControl.LocalPlayer.RpcSendChatNote(message.source, ChatNoteTypes.DidVote);
                }
            }

            if (!GeneralConfigurations.ShowVoteStateOption)
            {
                SoundManager.Instance.PlaySound(HudManager.Instance.Chat.messageSound, false, 1f, null).pitch = 0.5f + System.Random.Shared.NextSingle() * 0.6f;
            }
        }
        );

    static internal Vector3 VoteAreaPlayerIconPos = new Vector3(-0.95f, 0f, -2.5f);


}
