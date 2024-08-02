using Epic.OnlineServices;
using Nebula.Game.Statistics;
using Nebula.Modules.GUIWidget;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;

namespace Nebula.Roles.Crewmate;

public class Madmate : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Madmate() : base("madmate", new(Palette.ImpostorRed), RoleCategory.CrewmateRole, Crewmate.MyTeam, [CanFixLightOption, CanFixCommsOption, HasImpostorVisionOption, CanUseVentsOption, CanMoveInVentsOption, EmbroilVotersOnExileOption, LimitEmbroiledPlayersToVotersOption, CanIdentifyImpostorsOptionEditor]) { }
    Citation? HasCitation.Citaion => Citations.TheOtherRolesGM;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private BoolConfiguration EmbroilVotersOnExileOption = NebulaAPI.Configurations.Configuration("options.role.madmate.embroilPlayersOnExile", false);
    static private BoolConfiguration LimitEmbroiledPlayersToVotersOption = NebulaAPI.Configurations.Configuration("options.role.madmate.limitEmbroiledPlayersToVoters", true);

    static private BoolConfiguration CanFixLightOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canFixLight", false);
    static private BoolConfiguration CanFixCommsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canFixComms", false);
    static private BoolConfiguration HasImpostorVisionOption = NebulaAPI.Configurations.Configuration("options.role.madmate.hasImpostorVision", false);
    static private BoolConfiguration CanUseVentsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canUseVents", false);
    static private BoolConfiguration CanMoveInVentsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canMoveInVents", false);
    static private IntegerConfiguration CanIdentifyImpostorsOption = NebulaAPI.Configurations.Configuration("options.role.madmate.canIdentifyImpostors", (0, 3), 0);
    static private IOrderedSharableVariable<int>[] NumOfTasksToIdentifyImpostorsOptions = [
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
                List<GUIWidget> inner = new([
                    GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OptionsTitleHalf), "options.role.madmate.requiredTasksForIdentifying"),
                    GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OptionsFlexible), ":"),
                    GUI.API.HorizontalMargin(0.05f)
                    ]);
                int length = CanIdentifyImpostorsOption.GetValue();
                for (int i = 0; i < length; i++)
                {
                    if (i != 0) inner.AddRange([GUI.API.HorizontalMargin(0.05f), GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), ",")]);

                    var option = NumOfTasksToIdentifyImpostorsOptions[i];

                    inner.AddRange([
                        GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsValueShorter), option.CurrentValue.ToString()),
                        GUI.API.SpinButton(GUIAlignment.Center, v => { option.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
                        ]);
                }
                widgets.Add(new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left, inner));
            }
            return new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, widgets);
        }
        );

    static public Madmate MyRole = new Madmate();

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

        void RuntimeAssignable.OnActivated()
        {
            SetMadmateTask();
            if(AmOwner) IdentifyImpostors();
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
                var pool = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => p.Role.Role.Category == RoleCategory.ImpostorRole && !impostors.Contains(p.PlayerId)).ToArray();
                //候補が残っていなければ何もしない
                if (pool.Length == 0) return;
                //生存しているインポスターだけに絞っても候補がいるなら、そちらを優先する。
                if (pool.Any(p => !p.IsDead)) pool = pool.Where(p => !p.IsDead).ToArray();

                impostors.Add(pool[System.Random.Shared.Next(pool.Length)].PlayerId);

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
            if (NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && p.Role.Role.Category == RoleCategory.ImpostorRole))
                new StaticAchievementToken("madmate.common1");

            if (!EmbroilVotersOnExileOption) return;

            if (LimitEmbroiledPlayersToVotersOption)
                ExtraExileRoleSystem.MarkExtraVictim(MyPlayer.Unbox(), false, true);
            else
            {
                var voters = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && !p.AmOwner && p.Role.Role.Category != RoleCategory.ImpostorRole).ToArray();
                if (voters.Length > 0) voters.Random().VanillaPlayer.ModMarkAsExtraVictim(MyPlayer.VanillaPlayer, PlayerState.Embroiled, EventDetail.Embroil);
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
            if(ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin && NebulaGameManager.Instance!.AllPlayerInfo().All(p=>p.Role.Role.Category != RoleCategory.ImpostorRole || !p.IsDead))
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

