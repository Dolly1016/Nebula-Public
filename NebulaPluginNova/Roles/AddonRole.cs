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

namespace Nebula.Roles;

public class AddonRole : ConfigurableStandardRole
{
    public AbstractRoleDef MyDef;
    public override RoleCategory RoleCategory => MyDef.RoleCategory;

    public override string LocalizedName => MyDef.LocalizedName;
    public override Color RoleColor => MyDef.RoleColor.ToUnityColor();
    public override RoleTeam Team => MyDef.Team;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new AddonRoleInstance(player, this, arguments);

    protected override void LoadOptions()
    {
        base.LoadOptions();

        MyDef.LoadOptions();
    }

    public AddonRole(AbstractRoleDef roleDef)
    {
        MyDef = roleDef;
    }
}

public class AddonRoleInstance : RoleInstance, IBinderLifespan
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
            MyRoleInstance.MyPlayer = player;
            MyRoleInstance.MyBinderLifespan = this;
        }
        MyRoleInstance?.SetRole(MyAddonRole.MyDef);
    }


    private bool isActiveRole = true;
    bool ILifespan.IsDeadObject => !isActiveRole;
    public override void Release()
    {
        base.Release();
        isActiveRole = false;
    }

    public override void OnActivated()
    {
        base.OnActivated();
        EventManager.RegisterEvent(this, MyRoleInstance);
        MyRoleInstance?.OnActivated();
        if(AmOwner) MyRoleInstance?.OnLocalActivated();
    }

    public override void Update()
    {
        MyRoleInstance?.OnUpdate();
        if (AmOwner) MyRoleInstance?.OnLocalUpdate();
    }
}
