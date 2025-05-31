using Epic.OnlineServices;
using Nebula.Game.Statistics;
using Nebula.Modules.GUIWidget;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;

namespace Nebula.Roles.Crewmate;

public class Madmate : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Madmate() : base("madmate", new(Palette.ImpostorRed), RoleCategory.CrewmateRole, Crewmate.MyTeam, 
        [CanFixLightOption, CanFixCommsOption, CanSuicideOption, SuicideCoolDownOption, HasImpostorVisionOption, CanUseVentsOption, CanMoveInVentsOption, 
        new GroupConfiguration("options.role.madmate.group.embroil", [EmbroilVotersOnExileOption, LimitEmbroiledPlayersToVotersOption, EmbroilDelayOption], GroupConfigurationColor.ImpostorRed), 
        new GroupConfiguration("options.role.madmate.group.identification", [CanIdentifyImpostorsOptionEditor], GroupConfigurationColor.ImpostorRed)
        ]) 
    {
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Madmate.png");
        ConfigurationHolder?.ScheduleAddRelated(() => [Modifier.Madmate.MyRole.ConfigurationHolder!]);
    }
    Citation? HasCitation.Citation => Citations.TheOtherRolesGM;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private readonly BoolConfiguration EmbroilVotersOnExileOption = NebulaAPI.Configurations.Configuration("options.role.madmate.embroilPlayersOnExile", false);
    static private readonly BoolConfiguration LimitEmbroiledPlayersToVotersOption = NebulaAPI.Configurations.Configuration("options.role.madmate.limitEmbroiledPlayersToVoters", true, ()=>EmbroilVotersOnExileOption);
    static private readonly FloatConfiguration EmbroilDelayOption = NebulaAPI.Configurations.Configuration("options.role.madmate.embroilDelay", (0f, 5f, 1f), 0f,FloatConfigurationDecorator.TaskPhase, () => EmbroilVotersOnExileOption);

    static private readonly BoolConfiguration CanSuicideOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canSuicide", false);
    static private readonly IRelativeCoolDownConfiguration SuicideCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.madmate.suicideCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 30f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f, () => CanSuicideOption);
    static private readonly BoolConfiguration CanFixLightOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canFixLight", false);
    static private readonly BoolConfiguration CanFixCommsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canFixComms", false);
    static private readonly BoolConfiguration HasImpostorVisionOption = NebulaAPI.Configurations.Configuration("options.role.madmate.hasImpostorVision", false);
    static private readonly BoolConfiguration CanUseVentsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canUseVents", false);
    static private readonly BoolConfiguration CanMoveInVentsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canMoveInVents", false);
    static private readonly IntegerConfiguration CanIdentifyImpostorsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canIdentifyImpostors", (0, 3), 0);
    static private readonly IOrderedSharableVariable<int>[] NumOfTasksToIdentifyImpostorsOptions = [
        NebulaAPI.Configurations.SharableVariable("numOfTasksToIdentifyImpostors0",(0,10),2),
        NebulaAPI.Configurations.SharableVariable("numOfTasksToIdentifyImpostors1",(0,10),4),
        NebulaAPI.Configurations.SharableVariable("numOfTasksToIdentifyImpostors2",(0,10),6)
        ];
    static private IConfiguration CanIdentifyImpostorsOptionEditor = NebulaAPI.Configurations.Configuration(
        () => CanIdentifyImpostorsOption.GetDisplayText() + StringExtensions.Color(
            " (" +
            NumOfTasksToIdentifyImpostorsOptions
                .Take(CanIdentifyImpostorsOption)
                .Join(option => option.Value.ToString(), ", ")
            + ")", Color.gray),
        () =>
        {
            List<GUIWidget> widgets = new([CanIdentifyImpostorsOption.GetEditor().Invoke()]);

            if (CanIdentifyImpostorsOption.GetValue() > 0)
            {
                int length = CanIdentifyImpostorsOption.GetValue();
                
                for (int i = 0; i < length; i++)
                {
                    var option = NumOfTasksToIdentifyImpostorsOptions[i];

                    widgets.Add(new HorizontalWidgetsHolder(GUIAlignment.Left,
                        GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), "options.role.madmate.requiredTasksForIdentifying" + i),
                        GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OptionsFlexible), ":"),
                        GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsValueShorter), option.CurrentValue.ToString()),
                        GUI.API.SpinButton(GUIAlignment.Center, v => { option.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
                        ));
                }
            }
            return new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, widgets);
        }
        );

    bool DefinedRole.IsMadmate => true;
    static public readonly Madmate MyRole = new();
    static private readonly GameStatsEntry StatsFound = NebulaAPI.CreateStatsEntry("stats.madmate.foundImpostors", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsEmbroil = NebulaAPI.CreateStatsEntry("stats.madmate.embroil", GameStatsCategory.Roles, MyRole);
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        List<byte> impostors = new();

        public Instance(GamePlayer player) : base(player) {}

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.IsWin |= ev.GameEnd == NebulaGameEnd.ImpostorWin;

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= ev.GameEnd == NebulaGameEnd.CrewmateWin;

        void SetMadmateTask()
        {
            if (AmOwner)
            {
                if (CanIdentifyImpostorsOption > 0)
                {
                    var numOfTasksOptions = NumOfTasksToIdentifyImpostorsOptions.Take(CanIdentifyImpostorsOption);
                    int max = numOfTasksOptions.Max(option => option.Value);

                    using (RPCRouter.CreateSection("MadmateTask"))
                    {
                        MyPlayer.Tasks.Unbox().ReplaceTasksAndRecompute(max, 0, 0);
                        MyPlayer.Tasks.Unbox().BecomeToOutsider();
                    }
                }
            }
        }

        static private Image suicideButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MadmateSuicideButton.png", 115f);
        void RuntimeAssignable.OnActivated()
        {
            SetMadmateTask();
            if(AmOwner) IdentifyImpostors();

            if (AmOwner && CanSuicideOption)
            {
                var suicideButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, SuicideCoolDownOption.CoolDown,
                    "madmate.suicide", suicideButtonSprite);
                suicideButton.OnClick = (button) =>
                {
                    MyPlayer.Suicide(PlayerState.Suicide, null, KillParameter.RemoteKill);
                };
                suicideButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
            }
        }

        public void OnGameStart(GameStartEvent ev)
        {
            SetMadmateTask();
            if (AmOwner) IdentifyImpostors();
        }

        private void IdentifyImpostors()
        {
            //インポスター判別のチャンスだけ繰り返す
            while (CanIdentifyImpostorsOption > impostors.Count && MyPlayer.Tasks.CurrentCompleted >= NumOfTasksToIdentifyImpostorsOptions[impostors.Count].Value)
            {
                var pool = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => p.Role.Role.Category == RoleCategory.ImpostorRole && !impostors.Contains(p.PlayerId)).ToArray();
                //候補が残っていなければ何もしない
                if (pool.Length == 0) return;
                //生存しているインポスターだけに絞っても候補がいるなら、そちらを優先する。
                if (pool.Any(p => !p.IsDead)) pool = pool.Where(p => !p.IsDead).ToArray();

                impostors.Add(pool[System.Random.Shared.Next(pool.Length)].PlayerId);
                StatsFound.Progress();

                if (MyPlayer.Tasks.CurrentCompleted > 0) new StaticAchievementToken("madmate.common2");
            }
        }

        [OnlyMyPlayer]
        public void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev) => IdentifyImpostors();


        [Local]
        void DecorateOtherPlayerName(PlayerDecorateNameEvent ev)
        {
            if (impostors.Contains(ev.Player.PlayerId) && ev.Player.IsImpostor) ev.Color = new(Palette.ImpostorRed);
            
        }

        [Local, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {
            if (NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.Role.Role.Category == RoleCategory.ImpostorRole))
                new StaticAchievementToken("madmate.common1");

            if (!EmbroilVotersOnExileOption) return;

            GamePlayer[] voters = MeetingHudExtension.LastVotedForMap
                .Where(entry => entry.Value == MyPlayer.PlayerId && entry.Key != MyPlayer.PlayerId)
                .Select(entry => NebulaGameManager.Instance!.GetPlayer(entry.Key)).ToArray()!;

            void Embroil()
            {
                StatsEmbroil.Progress();

                ExtraExileRoleSystem.MarkExtraVictim(MyPlayer.Unbox(), false, true, LimitEmbroiledPlayersToVotersOption ? voters : []);

            }

            if (EmbroilDelayOption == 0)
                Embroil();
            else
            {
                int left = (int)(float)EmbroilDelayOption;
                GameOperatorManager.Instance?.Subscribe<MeetingPreSyncEvent>(ev => {
                    left--;
                    if (left == 0) Embroil();
                }, new FunctionalLifespan(() => left > 0));
            }
        }




        [Local, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer?.Role.Role.Category == RoleCategory.ImpostorRole)
            {
                new StaticAchievementToken("madmate.another1");
                if (ev.Murderer != null && impostors.Contains(ev.Murderer.PlayerId)) new StaticAchievementToken("madmate.another2");
                if (ev.Murderer != null && !impostors.Contains(ev.Murderer.PlayerId) && impostors.Count > 0) new StaticAchievementToken("madmate.another3");
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin && NebulaGameManager.Instance!.AllPlayerInfo.All(p=>p.Role.Role.Category != RoleCategory.ImpostorRole || !p.IsDead))
                new StaticAchievementToken("madmate.challenge");
        }

        RoleTaskType RuntimeRole.TaskType => CanIdentifyImpostorsOption > 0 ? RoleTaskType.RoleTask : RoleTaskType.NoTask;

        bool RuntimeAssignable.CanFixComm => CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => CanFixLightOption;
        bool RuntimeRole.CanMoveInVent => CanMoveInVentsOption;
        bool RuntimeRole.CanUseVent => CanUseVentsOption;
        bool RuntimeRole.HasImpostorVision => HasImpostorVisionOption;
        bool RuntimeRole.IgnoreBlackout => HasImpostorVisionOption;
    }
}

