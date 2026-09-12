using Nebula.Game.Statistics;
using System.Linq;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using Virial.Utilities;

namespace Nebula.Roles.Modifier;


public class Twin : DefinedModifierTemplate, DefinedAllocatableModifier, RoleFilter, IAssignableDocument
{
    private Twin() : base("twin", new(122, 196, 232), [NumOfPairsOption, RoleChanceOption, SelfSacrificeDelayOption, SelfSacrificeDelayDispersionOption])
    {
        ConfigurationHolder?.SetDisplayState(() => NumOfPairsOption == 0 ? ConfigurationHolderState.Inactivated : RoleChanceOption == 100 ? ConfigurationHolderState.Emphasized : ConfigurationHolderState.Activated);
    }

    string ICodeName.CodeName => "TWN";

    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.ModifierFilter?.Test(this) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.ModifierFilter?.ToggleAndShare(this);
    void AssignableFilter<DefinedRole>.SetAndShare(Virial.Assignable.DefinedRole role, bool val) => role.ModifierFilter?.SetAndShare(this, val);
    RoleFilter HasRoleFilter.RoleFilter => this;
    bool ISpawnable.IsSpawnable => NumOfPairsOption > 0;

    int HasAssignmentRoutine.AssignPriority => 1;

    static internal IntegerConfiguration NumOfPairsOption = NebulaAPI.Configurations.Configuration("options.role.twin.numOfPairs", (0, 7), 0);
    static internal IntegerConfiguration RoleChanceOption = NebulaAPI.Configurations.Configuration("options.role.twin.roleChance", (10, 100, 10), 100, decorator: num => num + "%", title: new TranslateTextComponent("options.role.chance"));

    static private readonly FloatConfiguration SelfSacrificeDelayOption = NebulaAPI.Configurations.Configuration("options.role.twin.selfSacrificeDelay", (0f, 10f, 0.5f), 2f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration SelfSacrificeDelayDispersionOption = NebulaAPI.Configurations.Configuration("options.role.twin.selfSacrificeDelayDispersion", (0f, 10f, 0.25f), 1f, FloatConfigurationDecorator.Second);

    static public Twin MyRole = new Twin();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    SpecialAssignment[] DefinedAllocatableModifier.SpecialAssignment => NumOfPairsOption > 0 ? [new(this, NumOfPairsOption, RoleChanceOption.GetValue(), null, null)] : [];

    void HasAssignmentRoutine.TryAssign(Virial.Assignable.IRoleTable roleTable)
    {
        //双子はクルーメイトの中からのみ選ばれる(第三陣営、インポスターは対象外)
        Queue<(byte playerId, DefinedRole role)> crewmates = new(roleTable.GetPlayers(RoleCategory.CrewmateRole).Where(p => p.role.CanLoad(this)).Shuffle());

        int maxPairs = NumOfPairsOption;
        float chance = RoleChanceOption / 100f;

        int assigned = 0;
        for (int i = 0; i < maxPairs; i++)
        {
            //これ以上ペアを作れない
            if (crewmates.Count < 2) break;

            //確率による割り当てスキップ
            if ((float)System.Random.Shared.NextDouble() >= chance) continue;

            roleTable.SetModifier(crewmates.Dequeue().playerId, this, [assigned]);
            roleTable.SetModifier(crewmates.Dequeue().playerId, this, [assigned]);

            assigned++;
        }
    }

    void IAssignToCategorizedRole.GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        assign100 = 0;
        assignRandom = 0;
        assignChance = 0;
    }

    //独自の勝利条件は持たない
    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => false;
    bool IAssignableDocument.HasWinCondition => false;

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public int TwinId => twinId;
        private int twinId;

        //相方は一度決定したら変更されないため、キャッシュさせる。
        public Cache<GamePlayer> MyTwin;

        public Instance(GamePlayer player, int twinId) : base(player)
        {
            this.twinId = twinId;

            MyTwin = new(() => NebulaGameManager.Instance?.AllPlayerInfo.FirstOrDefault(player => player.PlayerId != MyPlayer.PlayerId && player.Modifiers.Any(m => m is Twin.Instance twin && twin.twinId == twinId))!);
        }

        private Instance? MyTwinModifier => MyTwin.Get()?.GetModifiers<Instance>().FirstOrDefault(twin => twin.twinId == twinId);
        public bool IsMyTwin(GamePlayer? player) => player != null && player.PlayerId == (MyTwin.Get()?.PlayerId ?? byte.MaxValue);

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo || inEndScene || IsMyTwin(GamePlayer.LocalPlayer)) name += MyRole.GetRoleIconTagSmall();
        }

        string? RuntimeModifier.DisplayIntroBlurb => Language.Translate("role.twin.blurb").Replace("%NAME%", (MyTwin.Get()?.Name ?? "ERROR").Color(MyRole.Color));

        #region Guard
        //ガードを発動済みかどうか(ホストでのみ使用する)
        private bool guardIsUsed = false;
        //直前のキル判定を双子として防いだかどうか(ホストでのみ使用する)
        private bool guardedByMe = false;

        [OnlyMyPlayer]
        void CheckKill(PlayerCheckKilledEvent ev)
        {
            guardedByMe = false;

            //ガードは一度だけ発動する
            if (guardIsUsed) return;

            //キラーが自分自身の場合(自殺等)はガードしない
            if (ev.Killer.PlayerId == MyPlayer.PlayerId) return;

            //相方が既に死亡している場合、身代わりは立てられない
            if (MyTwin.Get()?.IsDead ?? true) return;

            //キラーにのみガードのエフェクトが見える
            ev.Result = KillResult.Guard;
            guardedByMe = true;
        }

        //相方の身代わりによる死亡を待機しているかどうか(ホストでのみ使用する)
        private bool sacrificeIsPending = false;

        private void SacrificeMyTwin()
        {
            if (!sacrificeIsPending) return;
            sacrificeIsPending = false;

            var myTwin = MyTwin.Get();
            if (myTwin?.IsDead ?? true) return;

            myTwin.Suicide(PlayerState.SelfSacrifice, EventDetail.Kill, KillParameter.NormalKill);
        }

        [OnlyMyPlayer, OnlyHost]
        void OnGuard(PlayerGuardEvent ev)
        {
            //双子以外の理由で防がれたキルでは身代わりにならない
            if (!guardedByMe) return;
            guardedByMe = false;

            //ガードを使い切る
            guardIsUsed = true;

            if (MyTwin.Get()?.IsDead ?? true) return;

            sacrificeIsPending = true;

            IEnumerator CoDelaySacrifice()
            {
                yield return Effects.Wait(SelfSacrificeDelayOption + SelfSacrificeDelayDispersionOption * (float)System.Random.Shared.NextDouble());

                SacrificeMyTwin();
            }
            NebulaManager.Instance.StartCoroutine(CoDelaySacrifice().WrapToIl2Cpp());
        }

        //会議が始まる場合は、待機中の身代わりを即座に成立させる
        [OnlyHost]
        void OnPreMeetingStart(MeetingPreStartEvent ev) => SacrificeMyTwin();
        #endregion

        #region Exile
        [OnlyMyPlayer, OnlyHost]
        void OnExiled(PlayerExiledEvent ev)
        {
            var myTwin = MyTwin.Get();
            if (myTwin?.IsDead ?? true) return;
            if (MeetingHudExtension.MarkedAsExtraVictims(myTwin.PlayerId)) return;

            //追放シーンで相方も後を追って自殺する
            myTwin.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
        }

        [OnlyMyPlayer, OnlyHost]
        void OnExtraExiled(PlayerExtraExiledEvent ev)
        {
            var myTwin = MyTwin.Get();
            if (myTwin?.IsDead ?? true) return;
            if (MeetingHudExtension.MarkedAsExtraVictims(myTwin.PlayerId)) return;

            myTwin.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole);
        }
        #endregion

        #region Vote
        [OnlyHost]
        void FixVotes(MeetingFixVoteHostEvent ev)
        {
            var myTwin = MyTwinModifier?.MyPlayer;
            if (myTwin == null) return;

            //双子のうち、プレイヤーIDの若い方でのみ処理する
            if (MyPlayer.PlayerId > myTwin.PlayerId) return;

            //どちらかが死亡している場合、票の制限は発生しない
            if (MyPlayer.IsDead || myTwin.IsDead) return;
            //どちらかが投票していない場合、投じられている票はそのまま有効になる
            if (!ev.GetDidVote(MyPlayer) || !ev.GetDidVote(myTwin)) return;

            //票が投じられない方をランダムに決定する
            ev.SetVote(System.Random.Shared.Next(2) == 0 ? MyPlayer : myTwin, 0);
        }
        #endregion
    }
}
