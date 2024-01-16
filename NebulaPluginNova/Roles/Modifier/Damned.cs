using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Text;

namespace Nebula.Roles.Modifier;

public class Damned : ConfigurableStandardModifier
{
    static public Damned MyRole = new Damned();
    public override string LocalizedName => "damned";
    public override string CodeName => "DMD";
    public override Color RoleColor => Palette.ImpostorRed;

    private NebulaConfiguration TakeOverRoleOfKillerOption = null!;
    private NebulaConfiguration DamnedMurderMyKillerOption = null!;
    private NebulaConfiguration KillDelayOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        TakeOverRoleOfKillerOption = new NebulaConfiguration(RoleConfig, "takeOverRoleOfKiller", null, true, true);
        DamnedMurderMyKillerOption = new NebulaConfiguration(RoleConfig, "damnedMurderMyKiller", null, true, true);
        KillDelayOption = new NebulaConfiguration(RoleConfig, "killDelay", null, 0f, 20f, 2.5f, 0f, 0f) { Decorator = NebulaConfiguration.SecDecorator, Predicate = () => DamnedMurderMyKillerOption };
    }

    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance
    {
        public override AbstractModifier Role => MyRole;

        private bool hasGuard = true;
        AbstractRole? nextRole = null;
        public override bool CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        public override void DecorateRoleName(ref string text)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text = Language.Translate("role.damned.prefix").Color(MyRole.RoleColor) + "<space=0.5em>" + text; 
        }

        public override KillResult CheckKill(PlayerModInfo killer, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, bool isMeetingKill)
        {
            //Damnedが反射するように発動することは無い
            if (isMeetingKill || eventDetail == EventDetail.Curse) return KillResult.Kill;
            return hasGuard ? KillResult.ObviousGuard : KillResult.Kill;
        }

        public override void OnGuard(PlayerModInfo killer)
        {
            hasGuard = false;
            nextRole = killer.Role.Role;

            if (killer.AmOwner && MyRole.DamnedMurderMyKillerOption)
            {
                IEnumerator CoDelayKill()
                {
                    yield return Effects.Wait(MyRole.KillDelayOption.GetFloat());

                    using (RPCRouter.CreateSection("DamedAction"))
                    {
                        killer.MyControl.ModFlexibleKill(killer.MyControl, false, PlayerState.Cursed, EventDetail.Curse, true);
                        MyPlayer.RpcInvokerUnsetModifier(Role).InvokeSingle();
                        MyPlayer.RpcInvokerSetRole(MyRole.TakeOverRoleOfKillerOption ? nextRole! : Impostor.DamnedImpostor.MyRole, null).InvokeSingle();
                    }
                }
                NebulaManager.Instance.StartCoroutine(CoDelayKill().WrapToIl2Cpp());
            }

            if(AmOwner) AmongUsUtil.PlayQuickFlash(Palette.ImpostorRed);
        }

        public override void OnPreMeetingStart(PlayerModInfo reporter, PlayerModInfo? reported)
        {
            if(!hasGuard && !MyPlayer.IsDead && AmOwner && !MyRole.DamnedMurderMyKillerOption)
            {
                NebulaManager.Instance.ScheduleDelayAction(() =>
                {
                    using (RPCRouter.CreateSection("DamedAction"))
                    {
                        MyPlayer.RpcInvokerUnsetModifier(Role).InvokeSingle();
                        MyPlayer.RpcInvokerSetRole(MyRole.TakeOverRoleOfKillerOption ? nextRole! : Impostor.DamnedImpostor.MyRole, null).InvokeSingle();
                    }
                });
            }
        }
    }
}


