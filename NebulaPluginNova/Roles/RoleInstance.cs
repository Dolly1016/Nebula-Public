using NAudio.CoreAudioApi;
using Nebula.Modules;
using Virial.Assignable;

namespace Nebula.Roles;

public abstract class RoleInstance : AssignableInstance, IRuntimePropertyHolder, RuntimeRole
{
    public override IAssignableBase AssignableBase => Role;
    public abstract AbstractRole Role { get; }
    DefinedRole RuntimeRole.Role => Role;
    Virial.Game.Player RuntimeAssignable.MyPlayer => MyPlayer;
    public RoleInstance(GamePlayer player):base(player)
    {
    }

    public virtual int[]? GetRoleArgument() => null;
    public virtual bool CanInvokeSabotage => Role.Category == RoleCategory.ImpostorRole;
    public virtual bool HasVanillaKillButton => Role.Category == RoleCategory.ImpostorRole;
    public virtual bool CanReport => true;
    public virtual bool CanUseVent => Role.Category != RoleCategory.CrewmateRole;
    public virtual bool CanMoveInVent => true;
    public virtual Timer? VentCoolDown => null;
    public virtual Timer? VentDuration => null;
    public virtual string DisplayRoleName => Role.DisplayName.Color(Role.RoleColor);
    public virtual bool HasCrewmateTasks => Role.Category == RoleCategory.CrewmateRole;
    public virtual bool HasAnyTasks => HasCrewmateTasks;

    public virtual bool HasImpostorVision => Role.Category == RoleCategory.ImpostorRole;
    public virtual bool IgnoreBlackout => Role.Category == RoleCategory.ImpostorRole;

    public virtual bool EyesightIgnoreWalls => false;

    public virtual void OnEnterVent(Vent vent) { }
    public virtual void OnExitVent(Vent vent) { }

    public virtual void OnEnterVent(PlayerControl player,Vent vent) { }
    public virtual void OnExitVent(PlayerControl player, Vent vent) { }

    public virtual void OnGameReenabled() => VentCoolDown?.Start();

    //役職履歴に追加される直前に呼び出されます。
    public override void OnActivated() => VentCoolDown?.Start();
    public virtual void OnGameStart() => VentCoolDown?.Start();

    public virtual IEnumerator? CoMeetingEnd() => null;

    public bool TryGetProperty(string id, out INebulaProperty? property)
    {
        property = null;
        return false;
    }

    public virtual bool CanSeeOthersFakeSabotage { get => (MyPlayer as GamePlayer).IsImpostor; }

}

public abstract class GhostRoleInstance : AssignableInstance
{
    public override IAssignableBase AssignableBase => Role;
    public abstract AbstractGhostRole Role { get; }
    
    public GhostRoleInstance(GamePlayer player) : base(player)
    {
    }

    public virtual int[]? GetRoleArgument() => null;
    public virtual string DisplayRoleName => Role.DisplayName.Color(Role.RoleColor);
    

    //役職履歴に追加される直前に呼び出されます。
    public override void OnActivated() { }

}
