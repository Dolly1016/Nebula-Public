﻿using Nebula.Game.Statistics;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Modifier;

[NebulaRPCHolder]
public class Damned : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Damned() : base("damned", "DMD", new(Palette.ImpostorRed), [TakeOverRoleOfKillerOption, DamnedMurderMyKillerOption, KillDelayOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }
    
    static private BoolConfiguration TakeOverRoleOfKillerOption = NebulaAPI.Configurations.Configuration("options.role.damned.takeOverRoleOfKiller", true);
    static private BoolConfiguration DamnedMurderMyKillerOption = NebulaAPI.Configurations.Configuration("options.role.damned.damnedMurderMyKiller", true);
    static private FloatConfiguration KillDelayOption = NebulaAPI.Configurations.Configuration("options.role.damned.killDelay", (0f, 20f, 2.5f), 0f, FloatConfigurationDecorator.Second);

    static public Damned MyRole = new Damned();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private bool hasGuard = true;
        DefinedRole? nextRole = null;
        int[]? nextArgs = null;
        bool RuntimeAssignable.CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (canSeeAllInfo) name = Language.Translate("role.damned.prefix").Color(MyRole.UnityColor) + "<space=0.5em>" + name;
        }

        [OnlyMyPlayer]
        void CheckKill(PlayerCheckKillEvent ev)
        {
            //Damnedが反射するように発動することは無い
            if (ev.IsMeetingKill || ev.EventDetail == EventDetail.Curse) return;
            //自殺は考慮に入れない
            if (ev.Killer.PlayerId == MyPlayer.PlayerId) return;

            ev.Result = hasGuard ? KillResult.ObviousGuard : KillResult.Kill;
        }

        private void CheckAchievement(DefinedRole myNextRole)
        {
            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && myNextRole.Category == Virial.Assignable.RoleCategory.CrewmateRole)
                //インポスター⇒クルーメイト
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 2));

            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && myNextRole.Category != Virial.Assignable.RoleCategory.ImpostorRole)
                //インポスター⇒非インポスター
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 3));

            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.CrewmateRole && myNextRole.Category == Virial.Assignable.RoleCategory.ImpostorRole)
                //クルーメイト⇒インポスター
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 0));

            if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.NeutralRole && myNextRole.Category == Virial.Assignable.RoleCategory.ImpostorRole)
                //第三陣営⇒インポスター
                RpcNoticeCurse.Invoke((MyPlayer.PlayerId, 1));
        }

        [OnlyMyPlayer]
        void OnGuard(PlayerGuardEvent ev)
        {
            hasGuard = false;
            nextRole = ev.Murderer.Role.Role;
            nextArgs = ev.Murderer.Role.RoleArguments;
            
            if (ev.Murderer.AmOwner && DamnedMurderMyKillerOption)
            {
                IEnumerator CoDelayKill()
                {
                    yield return Effects.Wait(KillDelayOption);

                    DefinedRole myNextRole = TakeOverRoleOfKillerOption ? nextRole! : Impostor.DamnedImpostor.MyRole;
                    var myNextArgs = TakeOverRoleOfKillerOption ? nextArgs! : null;
                    CheckAchievement(myNextRole);


                    using (RPCRouter.CreateSection("DamedAction"))
                    {
                        if (MyPlayer.MurderPlayer(ev.Murderer, PlayerState.Cursed, EventDetail.Curse, KillParameter.RemoteKill) == KillResult.Kill)
                        {
                            MyPlayer.Unbox().RpcInvokerUnsetModifier(MyRole).InvokeSingle();
                            MyPlayer.Unbox().RpcInvokerSetRole(myNextRole, myNextArgs).InvokeSingle();
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
            if(!hasGuard && !MyPlayer.IsDead && !DamnedMurderMyKillerOption)
            {
                NebulaManager.Instance.ScheduleDelayAction(() =>
                {
                    DefinedRole myNextRole = TakeOverRoleOfKillerOption ? nextRole! : Impostor.DamnedImpostor.MyRole;
                    var myNextArgs = TakeOverRoleOfKillerOption ? nextArgs! : null;
                    CheckAchievement(myNextRole);

                    using (RPCRouter.CreateSection("DamnedAction"))
                    {
                        MyPlayer.Unbox().RpcInvokerUnsetModifier(MyRole).InvokeSingle();
                        MyPlayer.Unbox().RpcInvokerSetRole(myNextRole, myNextArgs).InvokeSingle();
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
                    NebulaGameManager.Instance!.EndState!.Winners.Test(NebulaGameManager.Instance.GetPlayer(param.playerId)) && NebulaGameManager.Instance!.EndState.EndCondition == NebulaGameEnd.CrewmateWin
                );
                if (param.type == 3) new AchievementToken<int>("damned.another1", 0, (_, _) =>
                    NebulaGameManager.Instance?.LocalPlayerInfo.MyKiller?.IsImpostor ?? false || NebulaGameManager.Instance?.LocalPlayerInfo.PlayerState == PlayerStates.Guessed || NebulaGameManager.Instance?.LocalPlayerInfo.PlayerState == PlayerStates.Exiled
                );
            }
        });
}


