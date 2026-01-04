using Nebula.Game.Statistics;
using Nebula.Roles.Neutral;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Nebula.Roles.Modifier;

[NebulaRPCHolder]
public class Damned : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Damned() : base("damned", "DMD", new(Palette.ImpostorRed), [DamnedActionOption, DamnedMurderMyKillerOption, KillDelayOption,
        new GroupConfiguration("options.role.damned.group.task", [CanBecomeAwareOfOption, TaskProgressOption], GroupConfigurationColor.ImpostorRed)
        ]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Damned.png");
    }
    
    //static private BoolConfiguration TakeOverRoleOfKillerOption = NebulaAPI.Configurations.Configuration("options.role.damned.takeOverRoleOfKiller", true);
    //static private BoolConfiguration PromoteToMadmateOption = NebulaAPI.Configurations.Configuration("options.role.damned.promoteToMadmate", false);
    static private ValueConfiguration<int> DamnedActionOption = NebulaAPI.Configurations.Configuration("options.role.damned.damnedAction", ["options.role.damned.damnedAction.impostor", "options.role.damned.damnedAction.takeOver", "options.role.damned.damnedAction.madmate"], 0);
    static private BoolConfiguration DamnedMurderMyKillerOption = NebulaAPI.Configurations.Configuration("options.role.damned.damnedMurderMyKiller", true);
    static private BoolConfiguration CanBecomeAwareOfOption = NebulaAPI.Configurations.Configuration("options.role.damned.canBecomeAwareOfDamned", false);
    static private FloatConfiguration TaskProgressOption = NebulaAPI.Configurations.Configuration("options.role.damned.taskProgressRequiredForSelfAdmission", (10, 100, 10), 80, FloatConfigurationDecorator.Percentage, () => CanBecomeAwareOfOption);
    static private FloatConfiguration KillDelayOption = NebulaAPI.Configurations.Configuration("options.role.damned.killDelay", (0f, 20f, 2.5f), 0f, FloatConfigurationDecorator.Second);

    static public Damned MyRole = new Damned();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        static private MultiImage DamnedAnimImage = DividedSpriteLoader.FromResource("Nebula.Resources.Damned.png", 100f, 6, 1);

        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private bool hasGuard = true;
        DefinedRole? nextRole = null;
        int[]? nextArgs = null;
        bool RuntimeAssignable.CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (canSeeAllInfo) name += MyRole.GetRoleIconTagSmall();
        }

        [OnlyMyPlayer]
        void CheckKill(PlayerCheckKilledEvent ev)
        {
            //Damnedが反射するように発動することは無い
            if (ev.IsMeetingKill || ev.EventDetail == EventDetail.Curse) return;
            //自殺は考慮に入れない
            if (ev.Killer.PlayerId == MyPlayer.PlayerId) return;

            //Avengerのキルは呪いを貫通する(このあと、Avengerに呪いを起こす)
            if (ev.Killer.Role.Role == Avenger.MyRole && (ev.Killer.Role as Avenger.Instance)?.AvengerTarget == ev.Player) return;

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

                    DefinedRole myNextRole = DamnedActionOption.GetValue() switch
                    {
                        0 => Impostor.DamnedImpostor.MyRole,
                        1 => nextRole,
                        _ => Crewmate.Madmate.MyRole,
                    };
                    var myNextArgs = DamnedActionOption.GetValue() == 1 ? nextArgs! : null;
                    CheckAchievement(myNextRole);

                    using (RPCRouter.CreateSection("DamedAction", true))
                    {
                        if (ev.Murderer.IsDead)
                        {
                            MyPlayer.RemoveModifier(MyRole);
                            MyPlayer.SetRole(myNextRole, myNextArgs);

                        }
                        else
                        {
                            ShareNextRole.Invoke((MyPlayer, myNextRole.Id, myNextArgs ?? []));
                            MyPlayer.MurderPlayer(ev.Murderer, PlayerState.Cursed, EventDetail.Curse, KillParameter.RemoteKill, KillCondition.BothAlive );
                            if(DamnedActionOption.GetValue() == 1) PlayerExtension.SendRoleSwapping(ev.Murderer, MyPlayer, myNextRole, PlayerRoleSwapEvent.SwapType.Duplicate);
                        }
                    }

                }
                NebulaManager.Instance.StartCoroutine(CoDelayKill().WrapToIl2Cpp());
            }

            if(AmOwner) AmongUsUtil.PlayQuickFlash(Palette.ImpostorRed);
        }

        [OnlyHost, OnlyMyPlayer]
        void OnAnyoneCursed(PlayerKillPlayerEvent ev)
        {
            if(ev.Dead.PlayerState ==  PlayerState.Cursed)
            {
                using (RPCRouter.CreateSection("DamedAction", true))
                {
                    MyPlayer.RemoveModifier(MyRole);
                    MyPlayer.SetRole(nextRole ?? Impostor.DamnedImpostor.MyRole, nextArgs);
                }
            }
        }

        [OnlyHost, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev) { 
            if(ev.Murderer.Role.Role == Avenger.MyRole && (ev.Murderer.Role as Avenger.Instance)?.AvengerTarget == ev.Dead && ev.Dead.PlayerState == PlayerState.Dead)
            {
                nextRole = DamnedActionOption.GetValue() switch
                {
                    0 => Impostor.DamnedImpostor.MyRole,
                    1 => ev.Dead.Role.Role,
                    _ => Crewmate.Madmate.MyRole
                };
                nextArgs = DamnedActionOption.GetValue() == 1 ? ev.Dead.Role.RoleArguments : [];
                GamePlayer[] lovers = ev.Murderer.GetModifiers<Lover.Instance>().Select(lover => lover.MyLover.Get()).ToArray();
                

                using (RPCRouter.CreateSection("DamedAction", true))
                {
                    ev.Murderer.RemoveModifier(Lover.MyRole);
                    foreach(var l in lovers) l?.RemoveModifier(Lover.MyRole);
                    ev.Murderer.SetRole(nextRole, nextArgs);
                    ev.Dead.SetRole(Ember.MyRole);
                }
            }
        }

        [Local]
        void OnPreMeetingStart(MeetingPreStartEvent ev)
        {
            if(!hasGuard && !MyPlayer.IsDead && !DamnedMurderMyKillerOption)
            {
                NebulaManager.Instance.ScheduleDelayAction(() =>
                {
                    DefinedRole myNextRole = DamnedActionOption.GetValue() switch
                    {
                        0 => Impostor.DamnedImpostor.MyRole,
                        1 => nextRole!,
                        _ => Crewmate.Madmate.MyRole
                    };
                    var myNextArgs = DamnedActionOption.GetValue() == 1 ? nextArgs : [];
                    CheckAchievement(myNextRole);

                    using (RPCRouter.CreateSection("DamnedAction", true))
                    {
                        MyPlayer.RemoveModifier(MyRole);
                        MyPlayer.SetRole(myNextRole, myNextArgs);
                    }
                });
            }
        }

        bool amAware = false;
        [OnlyMyPlayer, Local]
        void OnTaskUpdate(PlayerTaskUpdateEvent ev)
        {
            if (!CanBecomeAwareOfOption) return;
            if (MyPlayer.Tasks.CurrentTasks == 0) return;
            if (!(((float)MyPlayer.Tasks.CurrentCompleted / (float)MyPlayer.Tasks.CurrentTasks) < TaskProgressOption / 100f))
            {
                if (!amAware)
                {
                    amAware = true;
                    var animator = UnityHelper.SimpleAnimator(MyPlayer.VanillaPlayer.transform, new(0f, 0.7f, 0.1f), 0.12f, i => DamnedAnimImage.GetSprite(i % 6));
                    animator.transform.localEulerAngles = new(0f, 0f, -10f);
                    animator.material = new(NebulaAsset.MultiplyShader);
                    animator.transform.localScale = new(1.6f, 1.6f, 1f);
                    animator.color = new(1f, 1f, 1f, 0.75f);
                    GameOperatorManager.Instance?.RegisterReleasedAction(() => {
                        if (animator) GameObject.Destroy(animator.gameObject);
                    }, this);
                }
            }
        }

        public static RemoteProcess<(GamePlayer damnedPlayer, int roleId, int[] roleArg)> ShareNextRole = new(
        "ShareDamnedNextRole",
        (message, _) =>
        {
            if (message.damnedPlayer.TryGetModifier<Instance>(out var damned))
            {
                damned.nextRole = Roles.GetRole(message.roleId);
                damned.nextArgs = message.roleArg;
            }
        }
        );
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
                    GamePlayer.LocalPlayer?.MyKiller?.IsImpostor ?? false || GamePlayer.LocalPlayer?.PlayerState == PlayerStates.Guessed || GamePlayer.LocalPlayer?.PlayerState == PlayerStates.Exiled
                );
            }
        });
}


