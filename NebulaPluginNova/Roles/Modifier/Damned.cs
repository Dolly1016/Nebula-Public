using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Modifier;

[NebulaRPCHolder]
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

        RoleConfig.AddTags(ConfigurationHolder.TagFunny);

        TakeOverRoleOfKillerOption = new NebulaConfiguration(RoleConfig, "takeOverRoleOfKiller", null, true, true);
        DamnedMurderMyKillerOption = new NebulaConfiguration(RoleConfig, "damnedMurderMyKiller", null, true, true);
        KillDelayOption = new NebulaConfiguration(RoleConfig, "killDelay", null, 0f, 20f, 2.5f, 0f, 0f) { Decorator = NebulaConfiguration.SecDecorator, Predicate = () => DamnedMurderMyKillerOption };
    }

    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IBindPlayer
    {
        public override AbstractModifier Role => MyRole;

        private bool hasGuard = true;
        DefinedRole? nextRole = null;
        public override bool CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;
        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void DecorateRoleName(ref string text)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text = Language.Translate("role.damned.prefix").Color(MyRole.RoleColor) + "<space=0.5em>" + text; 
        }

        public override KillResult CheckKill(GamePlayer killer, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, bool isMeetingKill)
        {
            //Damnedが反射するように発動することは無い
            if (isMeetingKill || eventDetail == EventDetail.Curse) return KillResult.Kill;
            //自殺は考慮に入れない
            if (killer.PlayerId == MyPlayer.PlayerId) return KillResult.Kill;

            return hasGuard ? KillResult.ObviousGuard : KillResult.Kill;
        }

        private void CheckAchievement(DefinedRole myNextRole)
        {
            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && myNextRole.Category == Virial.Assignable.RoleCategory.CrewmateRole)
                //インポスター⇒クルーメイト
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 2));


            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.CrewmateRole && myNextRole.Category == Virial.Assignable.RoleCategory.ImpostorRole)
                //クルーメイト⇒インポスター
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 0));


            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.NeutralRole && myNextRole.Category == Virial.Assignable.RoleCategory.ImpostorRole)
                //第三陣営⇒インポスター
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 1));
        }

        void OnGuard(PlayerGuardEvent ev)
        {
            hasGuard = false;
            nextRole = ev.Murderer.Role.Role;

            if (ev.Murderer.AmOwner && MyRole.DamnedMurderMyKillerOption)
            {
                IEnumerator CoDelayKill()
                {
                    yield return Effects.Wait(MyRole.KillDelayOption.GetFloat());

                    DefinedRole myNextRole = MyRole.TakeOverRoleOfKillerOption ? nextRole! : Impostor.DamnedImpostor.MyRole;
                    CheckAchievement(myNextRole);


                    using (RPCRouter.CreateSection("DamedAction"))
                    {
                        if (MyPlayer.MurderPlayer(ev.Murderer, PlayerState.Cursed, EventDetail.Curse,false, true) == KillResult.Kill)
                        {
                            MyPlayer.Unbox().RpcInvokerUnsetModifier(Role).InvokeSingle();
                            MyPlayer.Unbox().RpcInvokerSetRole(myNextRole, null).InvokeSingle();
                        }
                    }
                }
                NebulaManager.Instance.StartCoroutine(CoDelayKill().WrapToIl2Cpp());
            }

            if(AmOwner) AmongUsUtil.PlayQuickFlash(Palette.ImpostorRed);
        }

        [Local]
        void OnPreMeetingStart(MeetingPreStartEvent ev)
        {
            if(!hasGuard && !MyPlayer.IsDead && !MyRole.DamnedMurderMyKillerOption)
            {
                NebulaManager.Instance.ScheduleDelayAction(() =>
                {
                    DefinedRole myNextRole = MyRole.TakeOverRoleOfKillerOption ? nextRole! : Impostor.DamnedImpostor.MyRole;
                    CheckAchievement(myNextRole);

                    using (RPCRouter.CreateSection("DamnedAction"))
                    {
                        MyPlayer.Unbox().RpcInvokerUnsetModifier(Role).InvokeSingle();
                        MyPlayer.Unbox().RpcInvokerSetRole(myNextRole, null).InvokeSingle();
                    }
                });
            }
        }
    }


    //Damnedであったプレイヤー本人が通知を受け取って実績を達成する
    public static RemoteProcess<(byte playerId, int type)> RpcNoticeCurse = new(
        "NoticeCurse",
        (param, _) =>
        {
            if(PlayerControl.LocalPlayer.PlayerId == param.playerId)
            {
                if (param.type == 0) new StaticAchievementToken("damned.common1");
                if (param.type == 1) new StaticAchievementToken("damned.common3");
                if (param.type == 2) new AchievementToken<int>("damned.challenge", 0, (_, _) => 
                    NebulaGameManager.Instance!.EndState!.CheckWin(param.playerId) && NebulaGameManager.Instance!.EndState.EndCondition == NebulaGameEnd.CrewmateWin
                );
            }
        });
}


