using Nebula.Modules;
using Virial.Assignable;

namespace Nebula.Roles;

public abstract class RoleInstance : AssignableInstance, IRuntimePropertyHolder
{
    public override IAssignableBase AssignableBase => Role;
    public abstract AbstractRole Role { get; }

    public RoleInstance(PlayerModInfo player):base(player)
    {
    }

    public virtual int[]? GetRoleArgument() => null;
    public virtual bool CanInvokeSabotage => Role.RoleCategory == RoleCategory.ImpostorRole;
    public virtual bool HasVanillaKillButton => Role.RoleCategory == RoleCategory.ImpostorRole;
    public virtual bool CanReport => true;
    public virtual bool CanUseVent => Role.RoleCategory != RoleCategory.CrewmateRole;
    public virtual bool CanMoveInVent => true;
    public virtual Timer? VentCoolDown => null;
    public virtual Timer? VentDuration => null;
    public virtual string DisplayRoleName => Role.DisplayName.Color(Role.RoleColor);
    public virtual bool HasCrewmateTasks => Role.RoleCategory == RoleCategory.CrewmateRole;
    public virtual bool HasAnyTasks => HasCrewmateTasks;

    public virtual bool HasImpostorVision => Role.RoleCategory == RoleCategory.ImpostorRole;
    public virtual bool IgnoreBlackout => Role.RoleCategory == RoleCategory.ImpostorRole;

    public virtual void OnEnterVent(Vent vent) { }
    public virtual void OnExitVent(Vent vent) { }

    public virtual void OnEnterVent(PlayerControl player,Vent vent) { }
    public virtual void OnExitVent(PlayerControl player, Vent vent) { }

    public override void OnGameReenabled() => VentCoolDown?.Start();
    public override void OnActivated() => VentCoolDown?.Start();
    public override void OnGameStart() => VentCoolDown?.Start();

    public virtual IEnumerator? CoMeetingEnd() => null;

    public bool TryGetProperty(string id, out INebulaProperty? property)
    {
        property = null;
        return false;
    }

    public virtual bool CanSeeOthersFakeSabotage { get => Role.RoleCategory == RoleCategory.ImpostorRole; }

}
