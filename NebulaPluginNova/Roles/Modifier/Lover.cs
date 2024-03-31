using Il2CppSystem.Data;
using Nebula.Roles.Assignment;
using Nebula.Roles.Neutral;
using Nebula.VoiceChat;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Modifier;


public class Lover : ConfigurableModifier, HasCitation
{
    static public Lover MyRole = new Lover();
    public override string LocalizedName => "lover";
    public override string CodeName => "LVR";
    public override Color RoleColor => new Color(255f / 255f, 0f / 255f, 184f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    private NebulaConfiguration RoleChanceOption = null!;
    private NebulaConfiguration NumOfPairsOption = null!;
    private NebulaConfiguration ChanceOfAssigningImpostorsOption = null!;
    private NebulaConfiguration AllowExtraWinOption = null!;
    public NebulaConfiguration AvengerModeOption = null!;
    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments[0]);

    public override IEnumerable<IAssignableBase> RelatedOnConfig() { if (Avenger.MyRole.RoleConfig.IsShown) yield return Avenger.MyRole; }

    public override void Assign(IRoleAllocator.RoleTable roleTable) {
        var impostors = roleTable.GetPlayers(RoleCategory.ImpostorRole).Where(p => p.role.CanLoad(this)).OrderBy(_=>Guid.NewGuid()).ToArray();
        var others = roleTable.GetPlayers(RoleCategory.CrewmateRole | RoleCategory.NeutralRole).Where(p => p.role.CanLoad(this)).OrderBy(_ => Guid.NewGuid()).ToArray();
        int impostorsIndex = 0;
        int othersIndex = 0;


        int maxPairs = NumOfPairsOption;
        float chanceImpostor = ChanceOfAssigningImpostorsOption.GetFloat() / 100f;
        (byte playerId, AbstractRole role)? first,second;

        for (int i = 0; i < maxPairs; i++)
        {
            float chance = RoleChanceOption.GetFloat() / 100f;
            if ((float)System.Random.Shared.NextDouble() >= chance) continue;

            try
            {
                first = others[othersIndex++];
                second = (impostorsIndex < impostors.Length && (float)System.Random.Shared.NextDouble() < chanceImpostor) ? impostors[impostorsIndex++] : second = others[othersIndex++];

                roleTable.SetModifier(first.Value.playerId, this, new int[] { i });
                roleTable.SetModifier(second.Value.playerId, this, new int[] { i });
            }
            catch
            {
                //範囲外アクセス(これ以上割り当てできない)
                break;
            }
        }
    }

    protected override void LoadOptions()
    {
        RoleChanceOption = ConfigurableStandardModifier.Generate100PercentRoleChanceOption(RoleConfig);
        NumOfPairsOption = new(RoleConfig, "numOfPairs", null, 0, 7, 0, 0);
        RoleConfig.IsActivated = () => NumOfPairsOption > 0;

        ChanceOfAssigningImpostorsOption = new(RoleConfig, "chanceOfAssigningImpostors", null, 0f, 100f, 10f, 0f, 0f) { Decorator = NebulaConfiguration.PercentageDecorator };
        AllowExtraWinOption = new(RoleConfig, "allowExtraWin", null, true, true);

        AvengerModeOption = new(RoleConfig, "avengerMode", null, false, false);
    }

    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;

        static private Color[] colors = new Color[] { MyRole.RoleColor,
        (Color)new Color32(254, 132, 3, 255) ,
        (Color)new Color32(3, 254, 188, 255) ,
        (Color)new Color32(255, 255, 0, 255) ,
        (Color)new Color32(3, 183, 254, 255) ,
        (Color)new Color32(8, 255, 10, 255) ,
        (Color)new Color32(132, 3, 254, 255) };
        private int loversId; 
        public Instance(PlayerModInfo player,int loversId) : base(player)
        {
            this.loversId = loversId;
        }
        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => endCondition == NebulaGameEnd.LoversWin && !MyPlayer.IsDead;

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            Color loverColor = colors[loversId];
            var myLover = MyLover;
            bool canSee = false;

            if (AmOwner || (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || (MyLover?.AmOwner ?? false))
            {
                canSee = true;
            }else if (myLover?.Role.Role == Avenger.MyRole && !myLover.IsDead && MyPlayer.IsDead)
            {
                int optionValue = Avenger.MyRole.CanKnowExistanceOfAvengerOption.CurrentValue;
                if(optionValue == 2 || ((optionValue == 1) && ((myLover!.Role as Avenger.Instance)?.AvengerTarget?.AmOwner ?? false))){
                    canSee = true;
                    loverColor = Avenger.MyRole.RoleColor;
                }
            }

            if (canSee) text += " ♥".Color(loverColor);
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner)
            {
                if (endState.EndCondition == NebulaGameEnd.LoversWin)
                {
                    if (!MyPlayer.IsDead) new StaticAchievementToken("lover.common1");

                    if (MyPlayer.Role.Role.Category != RoleCategory.ImpostorRole && NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead && p.Role.Role.Category == RoleCategory.ImpostorRole) == 2)
                        new StaticAchievementToken("lover.challenge");
                }
            }
        }
        void IGamePlayerEntity.OnDead()
        {
            if (AmOwner && MyPlayer.MyState == PlayerState.Suicide) new StaticAchievementToken("lover.another1");
        }

        void IGamePlayerEntity.OnMurdered(GamePlayer murder)
        {
            if(AmOwner && murder.PlayerId != MyPlayer.PlayerId)
            {
                var myLover = MyLover;
                if (myLover?.IsDead ?? true) return;

                if (MyRole.AvengerModeOption)
                    myLover.RpcInvokerSetRole(Avenger.MyRole, [murder.PlayerId]).InvokeSingle();
                else
                    myLover.MyControl.ModSuicide( false,PlayerState.Suicide,EventDetail.Kill);
            }
        }

        void IGamePlayerEntity.OnExiled()
        {
            if (AmOwner)
            {
                MyLover?.MyControl.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);

                if(Helpers.CurrentMonth == 12) new StaticAchievementToken("christmas");
            }
        }

        public override void OnActivated()
        {
            NebulaGameManager.Instance?.CriteriaManager.AddCriteria(NebulaEndCriteria.LoversCriteria);

            if (AmOwner)
            {
                if (GeneralConfigurations.LoversRadioOption)
                    Bind(VoiceChatManager.GenerateBindableRadioScript(p=>p == MyLover, "voiceChat.info.loversRadio", MyRole.RoleColor));
            }
        }

        public override bool CheckExtraWins(CustomEndCondition endCondition, ExtraWinCheckPhase phase, int winnersMask, ref ulong extraWinMask)
        {
            if (phase != ExtraWinCheckPhase.LoversPhase) return false;

            if (!MyRole.AllowExtraWinOption) return false;

            var myLover = MyLover;
            if (myLover == null) return false;
            if (myLover.IsDead) return false;
            if ((myLover.PlayerId & winnersMask) == 0) return false;

            extraWinMask |= NebulaGameEnd.ExtraLoversWin.ExtraWinMask;
            return true;
        }

        public PlayerModInfo? MyLover => NebulaGameManager.Instance?.AllPlayerInfo().FirstOrDefault(p => p.PlayerId != MyPlayer.PlayerId && p.AllModifiers.Any(m => m is Lover.Instance lover && lover.loversId == loversId));
        public override string? IntroText => Language.Translate("role.lover.blurb").Replace("%NAME%", (MyLover?.DefaultName ?? "ERROR").Color(MyRole.RoleColor));
        public override bool InvalidateCrewmateTask => true;
    }
}
