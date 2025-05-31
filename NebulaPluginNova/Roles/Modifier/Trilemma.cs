using Nebula.Game.Statistics;
using Nebula.VoiceChat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Utilities;
using Virial;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Modifier;

internal class Trilemma : DefinedModifierTemplate, DefinedAllocatableModifier, RoleFilter
{
    private Trilemma() : base("trilemma", new(113, 129, 155), [NumOfTrilemmaOption, RoleChanceOption, ImpostorAssignmentOption, NeutralAssignmentOption, WinConditionOption])
    {
        ConfigurationHolder?.SetDisplayState(() => NumOfTrilemmaOption == 0 ? ConfigurationHolderState.Inactivated : RoleChanceOption == 100 ? ConfigurationHolderState.Emphasized : ConfigurationHolderState.Activated);
        Language.Register("role.trilemma.winCond", () => WinConditionOption.GetValue() switch
        {
            0 => Language.Translate("role.trilemma.winCond.block").Color(Color.cyan).Bold(),
            1 => Language.Translate("role.trilemma.winCond.additional").Color(Color.cyan).Bold(),
            2 => Language.Translate("role.trilemma.winCond.overwrite").Color(Color.cyan).Bold(),
            _ => "Error case!"
        });
    }
    string ICodeName.CodeName => "TRL";

    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.ModifierFilter?.Test(this) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.ModifierFilter?.ToggleAndShare(this);
    void AssignableFilter<DefinedRole>.SetAndShare(Virial.Assignable.DefinedRole role, bool val) => role.ModifierFilter?.SetAndShare(this, val);
    RoleFilter HasRoleFilter.RoleFilter => this;
    bool ISpawnable.IsSpawnable => NumOfTrilemmaOption > 0;

    int HasAssignmentRoutine.AssignPriority => 1;

    static internal IntegerConfiguration NumOfTrilemmaOption = NebulaAPI.Configurations.Configuration("options.role.trilemma.numOfTrilemma", (0, 8), 0);
    static internal IntegerConfiguration RoleChanceOption = NebulaAPI.Configurations.Configuration("options.role.trilemma.roleChance", (10, 100, 10), 100, decorator: num => num + "%", title: new TranslateTextComponent("options.role.chance"));
    static private ValueConfiguration<int> ImpostorAssignmentOption = NebulaAPI.Configurations.Configuration("options.role.trilemma.impostorAssignment", [
        "options.role.trilemma.assignment.zero",
        "options.role.trilemma.assignment.maxOne",
        "options.role.trilemma.assignment.one",
        "options.role.trilemma.assignment.oneOrMore",
        "options.role.trilemma.assignment.zeroOrMore",
        ], 1);
    static private ValueConfiguration<int> NeutralAssignmentOption = NebulaAPI.Configurations.Configuration("options.role.trilemma.neutralAssignment", [
        "options.role.trilemma.assignment.zero",
        "options.role.trilemma.assignment.maxOne",
        "options.role.trilemma.assignment.one",
        "options.role.trilemma.assignment.oneOrMore",
        "options.role.trilemma.assignment.zeroOrMore",
        ], 4);
    static public ValueConfiguration<int> WinConditionOption = NebulaAPI.Configurations.Configuration("options.role.trilemma.winCondition", [
        "options.role.trilemma.winCondition.block",
        "options.role.trilemma.winCondition.additional",
        "options.role.trilemma.winCondition.overwrite",
        ], 0);

    static public Trilemma MyRole = new Trilemma();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));


    void HasAssignmentRoutine.TryAssign(Virial.Assignable.IRoleTable roleTable)
    {
        Queue<(byte playerId, DefinedRole role)>[] queues = [
            new(roleTable.GetPlayers(RoleCategory.ImpostorRole).Where(p => p.role.CanLoad(this)).Shuffle()),
            new(roleTable.GetPlayers(RoleCategory.NeutralRole).Where(p => p.role.CanLoad(this)).Shuffle()),
            new(roleTable.GetPlayers(RoleCategory.CrewmateRole).Where(p => p.role.CanLoad(this)).Shuffle())
        ];

        bool mustNotAssignToImp = ImpostorAssignmentOption.GetValue() == 0;
        bool mustNotAssignToNeu = NeutralAssignmentOption.GetValue() == 0;
        bool mustAssignToImp = ImpostorAssignmentOption.GetValue() is 2 or 3;
        bool mustAssignToNeu = NeutralAssignmentOption.GetValue() is 2 or 3;
        bool canMultiAssignToImp = ImpostorAssignmentOption.GetValue() >= 3;
        bool canMultiAssignToNeu = NeutralAssignmentOption.GetValue() >= 3;


        //imp, neu, crew
        bool[][] pattern = [
            [!mustNotAssignToImp, !mustAssignToImp && canMultiAssignToNeu, !mustAssignToImp],
            [!mustAssignToNeu && canMultiAssignToImp, !mustNotAssignToNeu, !mustAssignToNeu],
            [canMultiAssignToImp, canMultiAssignToNeu, true]
            ];

        int max = NumOfTrilemmaOption;

        byte[] players = [0, 0, 0];

        int assigned = 0;
        for (int i = 0; i < max; i++)
        {
            //確率による割り当てスキップ
            float chance = RoleChanceOption / 100f;
            if ((float)System.Random.Shared.NextDouble() >= chance) continue;

            for (int p = 0; p < players.Length; p++)
            {
                var currentPattern = pattern[p];
                int cand = 0;
                for(int c = 0;c < 3; c++) if (currentPattern[c]) cand += queues[c].Count;

                if (cand == 0) return;//これ以上割り当てられないなら終了。

                int selected = System.Random.Shared.Next(cand);
                for(int c = 0; c < 3; c++)
                {
                    if (!currentPattern[c]) continue;
                    if(queues[c].Count > selected)
                    {
                        players[p] = queues[c].Dequeue().playerId;
                        break;
                    }
                    else
                    {
                        selected -= queues[c].Count;
                    }
                }
            }

            //3人が確定したら割り当てる
            for (int p = 0; p < players.Length; p++)
            {
                roleTable.SetModifier(players[p], this, [assigned]);
            }
            assigned++;
        }
    }

    void IAssignToCategorizedRole.GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        assign100 = 0;
        assignRandom = 0;
        assignChance = 0;
    }

    static public Color[] Colors => colors;
    static private Color[] colors = new Color[] { MyRole.UnityColor,
        (Color)new Color32(178, 147, 69, 255),
        (Color)new Color32(184, 87, 159, 255),
        (Color)new Color32(164, 96, 203, 255),
        (Color)new Color32(93, 164, 96, 255),
        (Color)new Color32(207, 98, 98, 255) ,
        (Color)new Color32(163, 181, 203, 255),
        (Color)new Color32(223,164,116, 255),};

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private int trilemmaId;
        public Instance(GamePlayer player, int trilemmaId) : base(player)
        {
            this.trilemmaId = trilemmaId;
        }

        private bool AmLastTrilemma => MyTrilemmas.All(p => p.IsDead || p == MyPlayer) && !MyPlayer.IsDead;

        [OnlyMyPlayer]
        void CheckBlock(PlayerBlockWinEvent ev) => ev.SetBlockedIf(!AmLastTrilemma);

        [OnlyMyPlayer]
        void CheckExtraWin(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.TrilemmaPhase) return;

            if (WinConditionOption.GetValue() == 1 && AmLastTrilemma)
            {
                ev.SetWin(true);
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraTrilemmaWin);
            }
        }

        [OnlyMyPlayer]
        void CheckWin(PlayerCheckWinEvent ev)
        {
            ev.SetWinIf(ev.GameEnd == NebulaGameEnd.TrilemmaWin && WinConditionOption.GetValue() == 2 && AmLastTrilemma);
        }

        void OnCheckGameEnd(EndCriteriaMetEvent ev)
        {
            if (WinConditionOption.GetValue() == 2 && AmLastTrilemma && ev.Winners.Test(MyPlayer)) ev.TryOverwriteEnd(NebulaGameEnd.TrilemmaWin, GameEndReason.Special);
        }


        public bool IsMyTrilemma(GamePlayer p) => p.TryGetModifier<Instance>(out var trilemma) ? trilemma.trilemmaId == trilemmaId : false;
        public bool IsMyTrilemmaFast(GamePlayer p) => MyTrilemmas.Contains(p);
        private GamePlayer[] MyTrilemmas = null!;

        void RuntimeAssignable.OnActivated()
        {
            MyTrilemmas = NebulaGameManager.Instance!.AllPlayerInfo.Where(IsMyTrilemma).ToArray();
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            Color trilemmaColor = colors[canSeeAllInfo ? trilemmaId : 0];
            
            bool canSee = false;

            if (AmOwner || canSeeAllInfo || IsMyTrilemmaFast(GamePlayer.LocalPlayer!))
            {
                canSee = true;
            }
            
            if (canSee) name += " ▲".Color(trilemmaColor);
        }


        string? RuntimeModifier.DisplayIntroBlurb => Language.Translate("role.trilemma.blurb").Color(MyRole.UnityColor);

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            ev.AppendText(Language.Translate("role.trilemma.taskText").Replace("%PLAYERS%", string.Join(", ", MyTrilemmas.Where(p => !p.AmOwner).Select(p => p.PlayerName)) ?? "Undefined").Color(MyRole.UnityColor));
        }

        #region Titles
        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (MyTrilemmas.All(p => p.AmOwner || !p.IsDead)) new StaticAchievementToken("trilemma.another1");
        }

        [Local]
        void OnAnyoneDead(PlayerDieEvent ev)
        {
            if (!MyPlayer.IsDead && MyTrilemmas.All(p => p.AmOwner || p.IsDead))
            {
                new StaticAchievementToken("trilemma.common1");
                GameOperatorManager.Instance.Subscribe<GameEndEvent>(ev =>
                {
                    if (ev.EndState.EndReason == GameEndReason.Situation && ev.EndState.Winners.Test(MyPlayer) && !MyPlayer.IsDead && NebulaGameManager.Instance?.LastDead?.MyKiller == MyPlayer)
                    {
                        new StaticAchievementToken("trilemma.challenge");
                    }
                }, this);
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (MyPlayer.IsDead && ev.EndState.Winners.Test(MyPlayer) && MyTrilemmas.All(p => p.AmOwner || !ev.EndState.Winners.Test(p))) new StaticAchievementToken("trilemma.common2");
        }
        #endregion Titles
    }
}

