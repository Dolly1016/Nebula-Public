using Nebula.Game.Statistics;
using Nebula.Roles.Assignment;
using Nebula.Roles.Neutral;
using Nebula.VoiceChat;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Modifier;


public class Lover : DefinedModifierTemplate, DefinedAllocatableModifier, HasCitation, RoleFilter
{
    private Lover() : base("lover", new(255, 0, 184), [NumOfPairsOption, RoleChanceOption, ChanceOfAssigningImpostorsOption, AllowExtraWinOption, AvengerModeOption]) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Neutral.Avenger.MyRole.ConfigurationHolder!]);
        ConfigurationHolder?.SetDisplayState(() => NumOfPairsOption == 0 ? ConfigurationHolderState.Inactivated : RoleChanceOption == 100 ? ConfigurationHolderState.Emphasized : ConfigurationHolderState.Activated);
    }
    string ICodeName.CodeName => "LVR";
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.ModifierFilter?.Test(this) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.ModifierFilter?.ToggleAndShare(this);
    void AssignableFilter<DefinedRole>.SetAndShare(Virial.Assignable.DefinedRole role, bool val) => role.ModifierFilter?.SetAndShare(this, val);
    RoleFilter HasRoleFilter.RoleFilter => this;
    bool ISpawnable.IsSpawnable => NumOfPairsOption > 0;

    int HasAssignmentRoutine.AssignPriority => 1;

    static internal IntegerConfiguration NumOfPairsOption = NebulaAPI.Configurations.Configuration("options.role.lover.numOfPairs", (0, 7), 0);
    static private IntegerConfiguration RoleChanceOption = NebulaAPI.Configurations.Configuration("options.role.lover.roleChance", (10, 100, 10), 100, decorator: num => num + "%",title: new TranslateTextComponent("options.role.chance"));
    static private IntegerConfiguration ChanceOfAssigningImpostorsOption = NebulaAPI.Configurations.Configuration("options.role.lover.chanceOfAssigningImpostors", (0,100,10), 0, decorator: num => num + "%");
    static private BoolConfiguration AllowExtraWinOption = NebulaAPI.Configurations.Configuration("options.role.lover.allowExtraWin", true);
    static internal BoolConfiguration AvengerModeOption = NebulaAPI.Configurations.Configuration("options.role.lover.avengerMode", false);

    static public Lover MyRole = new Lover();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments[0]);


    void HasAssignmentRoutine.TryAssign(Virial.Assignable.IRoleTable roleTable){
        var impostors = roleTable.GetPlayers(RoleCategory.ImpostorRole).Where(p => p.role.CanLoad(this)).OrderBy(_=>Guid.NewGuid()).ToArray();
        var others = roleTable.GetPlayers(RoleCategory.CrewmateRole | RoleCategory.NeutralRole).Where(p => p.role.CanLoad(this)).OrderBy(_ => Guid.NewGuid()).ToArray();
        int impostorsIndex = 0;
        int othersIndex = 0;


        int maxPairs = NumOfPairsOption;
        float chanceImpostor = ChanceOfAssigningImpostorsOption / 100f;
        (byte playerId, DefinedRole role)? first,second;

        int assigned = 0;
        for (int i = 0; i < maxPairs; i++)
        {
            float chance = RoleChanceOption / 100f;
            if ((float)System.Random.Shared.NextDouble() >= chance) continue;

            try
            {
                first = others[othersIndex++];
                second = (impostorsIndex < impostors.Length && (float)System.Random.Shared.NextDouble() < chanceImpostor) ? impostors[impostorsIndex++] : second = others[othersIndex++];

                roleTable.SetModifier(first.Value.playerId, this, new int[] { assigned });
                roleTable.SetModifier(second.Value.playerId, this, new int[] { assigned });

                assigned++;
            }
            catch
            {
                //範囲外アクセス(これ以上割り当てできない)
                break;
            }
        }
    }

    void IAssignToCategorizedRole.GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        assign100 = 0;
        assignRandom = 0;
        assignChance = 0;
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        static private Color[] colors = new Color[] { MyRole.UnityColor,
        (Color)new Color32(254, 132, 3, 255) ,
        (Color)new Color32(3, 254, 188, 255) ,
        (Color)new Color32(255, 255, 0, 255) ,
        (Color)new Color32(3, 183, 254, 255) ,
        (Color)new Color32(8, 255, 10, 255) ,
        (Color)new Color32(132, 3, 254, 255) };
        private int loversId; 
        public Instance(GamePlayer player,int loversId) : base(player)
        {
            this.loversId = loversId;
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.LoversWin && !MyPlayer.IsDead);

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            Color loverColor = colors[loversId];
            var myLover = MyLover;
            bool canSee = false;

            if (AmOwner || canSeeAllInfo || (MyLover?.AmOwner ?? false))
            {
                canSee = true;
            }else if (myLover?.Role.Role == Avenger.MyRole && !myLover.IsDead && MyPlayer.IsDead)
            {
                int optionValue = Avenger.CanKnowExistanceOfAvengerOption.GetValue();
                if(optionValue == 2 || ((optionValue == 1) && ((myLover!.Role as Avenger.Instance)?.AvengerTarget?.AmOwner ?? false))){
                    canSee = true;
                    loverColor = Avenger.MyRole.UnityColor;
                }
            }

            if (canSee) name += " ♥".Color(loverColor);
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.EndCondition == NebulaGameEnd.LoversWin)
            {
                if (!MyPlayer.IsDead) new StaticAchievementToken("lover.common1");

                if (MyPlayer.Role.Role.Category != RoleCategory.ImpostorRole && NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead && p.Role.Role.Category == RoleCategory.ImpostorRole) == 2)
                    new StaticAchievementToken("lover.challenge");
            }
        }

        [OnlyMyPlayer, Local]
        void OnDead(PlayerDieEvent ev)
        {
            if (MyPlayer.PlayerState == PlayerState.Suicide) new StaticAchievementToken("lover.another1");
        }

        [OnlyMyPlayer, Local]
        void OnMurdered(PlayerDieEvent ev)
        {
            var myLover = MyLover;
            if (myLover?.IsDead ?? true) return;

            if (ev is PlayerMurderedEvent pme)
            {
                if (!pme.Murderer.AmOwner)
                {
                    if (AvengerModeOption)
                        myLover.Unbox().RpcInvokerSetRole(Avenger.MyRole, [pme.Murderer.PlayerId]).InvokeSingle();
                    else
                        myLover.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                }
            }
            else
            {   
                myLover.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole);
            }
        }

        [OnlyMyPlayer, Local]
        void OnExtraExiled(PlayerExtraExiledEvent ev)
        {
            if (!(MyLover?.IsDead ?? false))
            {
                MyLover?.VanillaPlayer.ModMeetingKill(MyLover.VanillaPlayer, PlayerState.Suicide, PlayerState.Suicide, KillParameter.NormalKill);
            }
        }

        [OnlyMyPlayer, Local]
        void OnExiled(PlayerExtraExiledEvent ev)
        {
            if (!(MyLover?.IsDead ?? false))
            {
                MyLover?.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);

                if(Helpers.CurrentMonth == 12) new StaticAchievementToken("christmas");
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                if (GeneralConfigurations.LoversRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript(p=>p == MyLover, "voiceChat.info.loversRadio", MyRole.UnityColor));
            }
        }

        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.LoversPhase) return;
            if (!AllowExtraWinOption) return;

            var myLover = MyLover;
            if (myLover == null) return;
            if (myLover.IsDead && myLover.Role.Role != Jester.MyRole) return;
            if (!ev.WinnersMask.Test(myLover)) return;
            if (ev.WinnersMask.Test(MyPlayer)) return;

            ev.ExtraWinMask.Add(NebulaGameEnd.ExtraLoversWin);
            ev.IsExtraWin = true;
        }

        public GamePlayer? MyLover => NebulaGameManager.Instance?.AllPlayerInfo().FirstOrDefault(p => p.PlayerId != MyPlayer.PlayerId && p.Modifiers.Any(m => m is Lover.Instance lover && lover.loversId == loversId));
        string? RuntimeModifier.DisplayIntroBlurb => Language.Translate("role.lover.blurb").Replace("%NAME%", (MyLover?.Name ?? "ERROR").Color(MyRole.UnityColor));
        bool RuntimeModifier.InvalidateCrewmateTask => true;
        bool RuntimeModifier.MyCrewmateTaskIsIgnored => true;
    }
}
