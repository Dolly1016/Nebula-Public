using NAudio.CoreAudioApi;
using Nebula.Compat;
using Nebula.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles;

public class AddonRole : ConfigurableStandardRole
{
    public AbstractRoleDef MyDef;
    public override RoleCategory Category => MyDef.RoleCategory;

    public override string LocalizedName => MyDef.LocalizedName;
    public override Color RoleColor => MyDef.RoleColor.ToUnityColor();
    public override RoleTeam Team => MyDef.Team;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new AddonRoleInstance(player, this, arguments);

    protected override void LoadOptions()
    {
        base.LoadOptions();
    }

    public AddonRole(AbstractRoleDef roleDef)
    {
        MyDef = roleDef;
    }

    public override IEnumerable<IAssignableBase> RelatedOnConfig()
    {
        foreach (var assignable in MyDef.RelatedAssignable()) if (assignable is IAssignableBase iab) yield return iab;
    }
}

public class AddonRoleInstance : RoleInstance, IBinderLifespan, IGamePlayerEntity
{
    public AddonRole MyAddonRole { get; private set; }
    private AbstractRoleInstanceCommon? MyRoleInstance { get; set; }

    public override AbstractRole Role => MyAddonRole;
    public AddonRoleInstance(PlayerModInfo player, AddonRole myAddonRole, int[] arguments) : base(player)
    {
        MyAddonRole = myAddonRole;
        MyRoleInstance = (MyAddonRole.MyDef.RoleInstanceType?.GetConstructor(new Type[0])?.Invoke(new object?[0]) as AbstractRoleInstanceCommon);
        if (MyRoleInstance != null)
        {
            MyRoleInstance.RuntimeRole = this;
        }
        MyRoleInstance?.SetRole(MyAddonRole.MyDef);
    }


    private bool isActiveRole = true;
    bool ILifespan.IsDeadObject => !isActiveRole;
    void IGameEntity.OnReleased()
    {
        isActiveRole = false;
    }

    public override void OnActivated()
    {
        base.OnActivated();
        EventManager.RegisterEvent(this, MyRoleInstance);
        MyRoleInstance?.OnActivated();
        if(AmOwner) MyRoleInstance?.OnLocalActivated();
    }

    void IGameEntity.Update()
    {
        MyRoleInstance?.OnUpdate();
        if (AmOwner) MyRoleInstance?.OnLocalUpdate();
    }

    public override bool CheckWins(CustomEndCondition endCondition, ref ulong _)
    {
        if (MyRoleInstance != null) return MyRoleInstance!.CheckWin(endCondition);

        if (Role.Category == RoleCategory.ImpostorRole)
            return endCondition == NebulaGameEnd.ImpostorWin;
        else if (Role.Category == RoleCategory.CrewmateRole)
            return endCondition == NebulaGameEnd.CrewmateWin;
        
        return false;
    }

    public override void DecoratePlayerName(ref string text, ref Color color)
    {
        if (Role.Category == RoleCategory.ImpostorRole)
        {
            if (PlayerControl.LocalPlayer.GetModInfo()?.Role.Role.Category == RoleCategory.ImpostorRole) color = Palette.ImpostorRed;
        }
    }
}
