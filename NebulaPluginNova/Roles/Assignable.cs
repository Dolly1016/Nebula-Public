using Il2CppSystem.Runtime.CompilerServices;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Text;

namespace Nebula.Roles;

public interface IAssignableBase : DefinedAssignable
{
    public ConfigurationHolder? RelatedConfig { get; }
    public string InternalName { get; }
    public string LocalizedName { get; }
    public string DisplayName { get; }
    public Color RoleColor { get; }
    public int Id { get; set; }

    public void Load();

    //For Config
    public IEnumerable<IAssignableBase> RelatedOnConfig();


    ValueConfiguration? DefinedAssignable.GetConfiguration(string id) => RelatedConfig?.MyConfigurations.FirstOrDefault(c => c.Id == id);
}

public abstract class AssignableInstance : ScriptHolder, Virial.IBinderLifespan
{
    public virtual IAssignableBase AssignableBase { get; } = null!;
    public PlayerModInfo MyPlayer { get; private init; }
    public bool IsDeadObject { get; private set; } = false;
    public bool AmOwner => MyPlayer.AmOwner;

    public AssignableInstance(PlayerModInfo player)
    {
        this.MyPlayer = player;
    }

    public void Inactivate()
    {
        Release();
        OnInactivated();
        IsDeadObject = true;
    }

    public virtual bool CheckWins(CustomEndCondition endCondition,ref ulong extraWinMask) => false;
    public virtual bool CheckExtraWins(CustomEndCondition endCondition, int winnersMask,ref ulong extraWinMask) => false;
    public virtual void OnGameStart() { }
    public virtual void OnGameEnd(NebulaEndState endState) { }
    public virtual void Update() { }
    public virtual void LocalUpdate() { }
    public virtual void LocalHudUpdate() { }
    public virtual void OnMeetingStart() { }
    public virtual void OnStartExileCutScene() { }
    public virtual void OnMeetingEnd() { }
    public virtual void OnEndVoting() { }
    public virtual void OnGameReenabled() { }
    public virtual void OnDead() { }
    public virtual void OnExiled() { }
    public virtual void OnMurdered(PlayerControl murder) { }
    public virtual void OnKillPlayer(PlayerControl target) { }
    public virtual void OnGuard(PlayerModInfo killer) { }

    //OnReported, OnEmergencyMeetingの直前に呼び出されます。
    //OnPreMeetingStart -> OnReported / OnEmergencyMeeting -> しばらくの間 -> OnMeetingStart
    public virtual void OnPreMeetingStart(PlayerModInfo reporter, PlayerModInfo? reported) { }
    public virtual void OnReported(PlayerModInfo reporter,PlayerModInfo reported) { }
    public virtual void OnEmergencyMeeting(PlayerModInfo reporter) { }


    //何らかの理由でプレイヤーが死亡すると呼び出されます。
    public virtual void OnAnyoneDeadLocal(PlayerControl dead) { }
    //何らかの理由でプレイヤーが誰かに殺害されると呼び出されます。(OnPlayerDeadLocalはこの直前に呼び出されます。)
    public virtual void OnAnyoneMurderedLocal(PlayerControl dead, PlayerControl murderer) { }
    //何らかの理由でプレイヤーが追放されると呼び出されます。(追加追放は対象外)
    public virtual void OnAnyoneExiledLocal(PlayerControl exiled) { }

    public virtual void OnActivated() { }
    public virtual void OnSetTaskLocal(ref List<GameData.TaskInfo> tasks, out int extraQuota) { extraQuota = 0; }
    public virtual void OnTaskCompleteLocal() { }
    protected virtual void OnInactivated() { }
    public virtual void OnCastVoteLocal(byte target,ref int vote) { }
    //自身の票が投じられたときに呼び出されます。(投票結果開示の瞬間)
    public virtual void OnVotedLocal(PlayerControl? votedFor) { }
    //自身に票が投じられたときに呼び出されます。(投票結果開示の瞬間)
    public virtual void OnVotedForMeLocal(PlayerControl[] voters) { }
    //投票結果開示の瞬間に呼び出されます。 投票結果をここで書き換えることはできません。
    public virtual void OnDiscloseVotingLocal(MeetingHud.VoterState[] result) { }

    public virtual void OnDeadBodyGenerated(DeadBody deadBody) { }
    public virtual string? OverrideRoleName(string lastRoleName,bool isShort) => null;
    public virtual void DecoratePlayerName(ref string text, ref Color color) { }
    public virtual void DecorateOtherPlayerName(PlayerModInfo player,ref string text, ref Color color) { }
    public virtual void DecorateRoleName(ref string text) { }

    public virtual void EditLightRange(ref float range) { }

    public virtual void OnTieVotes(ref List<byte> extraVotes,PlayerVoteArea myVoteArea) { }

    public virtual void OnOpenSabotageMap() { }
    public virtual void OnOpenNormalMap() { }
    public virtual void OnOpenAdminMap() { }
    public virtual void OnMapInstantiated() { }

    public virtual string? GetExtraTaskText() => null;

    public virtual bool CanFixLight { get => true; }
    public virtual bool CanFixComm { get => true; }
    public virtual bool CanBeAwareAssignment { get => true; }

    public virtual KillResult CheckKill(PlayerModInfo killer, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, bool isMeetingKill) { return KillResult.Kill; }
}