using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Nebula.Roles.Crewmate;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
[NebulaRPCHolder]
public class OracleSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static OracleSystem() => DIManager.Instance.RegisterModule(() => new OracleSystem());
    
    private OracleSystem()
    {
        ModSingleton<OracleSystem>.Instance = this;
        this.RegisterPermanently();
    }

    private record RolePool(List<DefinedRole> CrewmateRoles, List<DefinedRole> ImpostorRoles, List<DefinedRole> NeutralRoles)
    {
        public List<DefinedRole> GetPoolFromCategory(RoleCategory category) => category switch { RoleCategory.CrewmateRole => CrewmateRoles, RoleCategory.ImpostorRole => ImpostorRoles, RoleCategory.NeutralRole => NeutralRoles, _ => [] };
        public void FilterBy(ISet<DefinedRole> excludedRoles)
        {
            CrewmateRoles.RemoveAll(excludedRoles.Contains);
            ImpostorRoles.RemoveAll(excludedRoles.Contains);
            NeutralRoles.RemoveAll(excludedRoles.Contains);
        }

        public bool IsEmpty(RoleCategory category) => GetPoolFromCategory(category).Count == 0;
    }
    private RolePool initialPool = null!;

    static private bool IsCrewmateRole(DefinedRole role) => role.Category == RoleCategory.CrewmateRole;
    static private bool IsImpostorRole(DefinedRole role) => role.Category == RoleCategory.ImpostorRole;
    static private bool IsNeutralRole(DefinedRole role) => role.Category == RoleCategory.NeutralRole;
    static private RoleCategory GetCategory(DefinedRole role) => IsNeutralRole(role) ? RoleCategory.NeutralRole : IsImpostorRole(role) ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole;
    private RolePool GetCurrentRolePool()
    {
        RolePool set = new([], [], []);
        NebulaGameManager.Instance!.AllPlayerInfo.Do(p =>
        {
            if (IsCrewmateRole(p.Role.ExternalRecognitionRole))
                set.CrewmateRoles.Add(p.Role.ExternalRecognitionRole);
            else if (IsNeutralRole(p.Role.ExternalRecognitionRole))
                set.NeutralRoles.Add(p.Role.ExternalRecognitionRole);
            else
                set.ImpostorRoles.Add(p.Role.ExternalRecognitionRole);
        });
        return set;
    }
    void OnGameStarted(GameStartEvent ev) => initialPool = GetCurrentRolePool();

    public HashSet<DefinedRole> GetExcludedRoles(GamePlayer oracle)
    {
        HashSet<DefinedRole> excludedRoles = [];

        bool CheckNonAssignedElse(GamePlayer player, DefinedRole role) => !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => p.Role.Role == role && p.PlayerId != player.PlayerId);
        if (CheckNonAssignedElse(oracle, Oracle.MyRole)) excludedRoles.Add(Oracle.MyRole);
        if (Oracle.DieOnDividingOpportunistOption) excludedRoles.Add(Neutral.Opportunist.MyRole);
        if (Oracle.DieOnDividingOracleOption) excludedRoles.Add(Oracle.MyRole);
        foreach (var lover in oracle.GetModifiers<Lover.Instance>())
        {
            if (!lover.IsAloneLover && CheckNonAssignedElse(lover.MyLover.Get(), lover.MyLover.Get().Role.Role)) 
                excludedRoles.Add(lover.MyLover.Get().Role.Role);
        }
        if (!NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.Role.Role == Neutral.Scarlet.MyRole)) excludedRoles.Add(Neutral.Scarlet.MyRole);
        return excludedRoles;
    }
    
    //役職候補を計算する。
    public DefinedRole[] GetRoleCandidate(GamePlayer oracle, GamePlayer target, int numOfCandidates)
    {
        var targetRole = target.Role.ExternalRecognitionRole;
        List<DefinedRole> result = [targetRole];

        var pool = GetCurrentRolePool();
        pool.FilterBy(GetExcludedRoles(oracle));
        pool.GetPoolFromCategory(GetCategory(targetRole)).RemoveAll(r => r == targetRole);
        
        var lastAddedRole = targetRole;
        
        //占い候補フラグとフラグの状態から占い候補の陣営を決定する関数およびフラグをリセットする関数, 選択済みあるいは選択できえない場合フラグを立てる = 選択できるカテゴリのフラグはfalse。
        bool crewFlag, impFlag, neuFlag;
        RoleCategory DetermineCategory()
        {
            if (!crewFlag && !impFlag && !neuFlag)
            {
                //3カテゴリともに対象にできる場合
                if (Helpers.Prob(0.33f))
                {
                    return RoleCategory.CrewmateRole;
                }
                return Helpers.Prob(0.5f) ? RoleCategory.ImpostorRole : RoleCategory.NeutralRole;
            }
            else if (crewFlag ^ impFlag ^ neuFlag)
            {
                //2カテゴリだけ対象のとき
                return Helpers.Prob(0.5f) ? (!crewFlag ? RoleCategory.CrewmateRole : RoleCategory.ImpostorRole) : (!neuFlag ? RoleCategory.NeutralRole : RoleCategory.ImpostorRole);
            }
            else
            {
                //1カテゴリ対象のとき
                return !crewFlag ? RoleCategory.CrewmateRole : !impFlag ? RoleCategory.ImpostorRole : RoleCategory.NeutralRole;
            }
        }
        void ResetFlags()
        {
            crewFlag = pool.CrewmateRoles.IsEmpty();
            impFlag = pool.ImpostorRoles.IsEmpty();
            neuFlag = pool.NeutralRoles.IsEmpty();
        }
        bool NoCandidates() => crewFlag && impFlag && neuFlag;
        ResetFlags();

        while (result.Count < numOfCandidates)
        {
            //前に追加した役職からフラグを更新する。
            crewFlag |= IsCrewmateRole(lastAddedRole);
            impFlag |= IsImpostorRole(lastAddedRole);
            neuFlag |= IsNeutralRole(lastAddedRole);

            //全候補から役職を出しきったらリセットする。それでも候補が無ければ終了する。
            if (NoCandidates()) ResetFlags();
            if (NoCandidates()) break;

            //プールを選択して、役職をランダムに排出する。
            var category = DetermineCategory();
            var selectedPool = pool.GetPoolFromCategory(category);
            lastAddedRole = selectedPool.Random();
            result.Add(lastAddedRole);
            selectedPool.RemoveAll(r => r == lastAddedRole);
        }
        
        return result.ToArray();
    }
    
}
internal class Oracle : DefinedSingleAbilityRoleTemplate<Oracle.Ability>, DefinedRole, IAssignableDocument
{
    private Oracle() : base("oracle", new(214, 156, 45), RoleCategory.CrewmateRole, Crewmate.MyTeam, [MaxOracleOption, OracleCooldownOption, OracleAdditionalCooldownOption, OracleDurationOption, NumOfCandidatesOption, DieOnDividingOpportunistOption, DieOnDividingOracleOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagChaotic);
    }

    static private readonly FloatConfiguration OracleCooldownOption = NebulaAPI.Configurations.Configuration("options.role.oracle.oracleCooldown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration OracleAdditionalCooldownOption = NebulaAPI.Configurations.Configuration("options.role.oracle.oracleAdditionalCooldown", (float[])[0f,0.5f,1f,2f,2.5f,3f,4f,5f,7.5f,10f,12.5f,15f,20f,30f], 1f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration OracleDurationOption = NebulaAPI.Configurations.Configuration("options.role.oracle.oracleDuration", (float[])[0f,0.5f,1f,1.5f,2f,2.5f,3f,3.5f,4f,5f,6f,7f,8f,9f,10f], 2f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration MaxOracleOption = NebulaAPI.Configurations.Configuration("options.role.oracle.maxOracle", (0, 10, 1), 5, null, num => num == 0 ? Language.Translate("options.noLimit") : num.ToString());
    static private readonly IntegerConfiguration NumOfCandidatesOption = NebulaAPI.Configurations.Configuration("options.role.oracle.numOfCandidates", (1, 6), 6);
    static internal readonly BoolConfiguration DieOnDividingOpportunistOption = NebulaAPI.Configurations.Configuration("options.role.oracle.dieOnDiviningOpportunist", true);
    static internal readonly BoolConfiguration DieOnDividingOracleOption = NebulaAPI.Configurations.Configuration("options.role.oracle.dieOnDiviningOracle", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, -1), arguments.Skip(2).ToArray());
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static public readonly Oracle MyRole = new();
    static private readonly GameStatsEntry StatsOracle = NebulaAPI.CreateStatsEntry("stats.oracle.oracle", GameStatsCategory.Roles, MyRole);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonImage, "role.oracle.ability.oracle");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%CAND%", Language.Translate(NumOfCandidatesOption >= 2 ? "role.oracle.ability.main.multiple" : "role.oracle.ability.main.one"));
        yield return new("%NUM%", NumOfCandidatesOption.GetValue().ToString());
        List<DefinedRole> roles = [];
        if (DieOnDividingOpportunistOption) roles.Add(Neutral.Opportunist.MyRole);
        if (DieOnDividingOracleOption) roles.Add(MyRole);
        yield return new("%DIE%", roles.Count > 0 ? Language.Translate("role.oracle.ability.oracle.die") : "");
        yield return new("%ROLES%", string.Join(Language.Translate("role.oracle.ability.oracle.die.split"), roles.Select(r => r.DisplayColoredName)));
    }

    static private readonly Image buttonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.OracleButton.png", 115f);
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), leftUses, ..divideResults.Select(entry => (int[])[entry.Key, entry.Value.role.Length, ..entry.Value.role.Select(r => r.Id)]).Smooth()];

        GUIWidget? IPlayerAbility.ProgressWidget => ProgressGUI.Holder(
            ProgressGUI.OneLineText(Language.Translate("role.oracle.gui.results")),
                (divideResults.Count == 0 ?
                    ProgressGUI.OneLineText(Language.Translate("role.oracle.gui.results.zero")) :
                    ProgressGUI.Holder(
                    divideResults.Select(result => ProgressGUI.OneLineText((GamePlayer.GetPlayer(result.Key)?.ColoredName ?? "???") + " ⇒" + result.Value.longName)))
                ).Move(new(0.1f, 0f))
            );


        Dictionary<byte, (DefinedRole[] role, string longName, string shortName)> divideResults = [];
        int leftUses;

        void ParseResult(int[] result) {
            int i = 0;
            while(i + 1 < result.Length)
            {
                int targetId = result[i++];
                int roleLength = result[i++];
                if (i + roleLength > result.Length) break;
                DefinedRole[] roles = new DefinedRole[roleLength];
                bool error = false;
                for (int k = 0; k < roleLength; k++)
                {
                    roles[k] = Roles.GetRole(result[i++])!;
                    error |= roles[k] == null;
                }
                if (error) roles = roles.NotNull().ToArray();
                AddResult((byte)targetId, roles);
            }
        }
        public Ability(GamePlayer player, bool isUsurped, int leftUses, int[] result) : base(player, isUsurped)
        {
            this.leftUses = leftUses;
            if (this.leftUses == -1) this.leftUses = MaxOracleOption == 0 ? 1000 : MaxOracleOption;

            ParseResult(result);
            if (AmOwner)
            {
                var playerTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeStandardPredicate(p) && !divideResults.ContainsKey(p.RealPlayer.PlayerId));

                var oracleButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    OracleCooldownOption, "oracle", buttonImage, 
                    _ => playerTracker.CurrentTarget != null, _ => this.leftUses > 0);
                if(this.leftUses < 20) oracleButton.ShowUsesIcon(this.leftUses.ToString(), MyRole.RoleColor);
                

                bool PredicateRole()
                {
                    var target = playerTracker.CurrentTarget!.RealPlayer;
                    if (
                        (DieOnDividingOracleOption && target.Role.Role == MyRole) ||
                        (DieOnDividingOpportunistOption && target.Role.Role == Neutral.Opportunist.MyRole)
                        )
                    {
                        MyPlayer.Suicide(PlayerStates.Punished, null, KillParameter.NormalKill);
                        return false;
                    }

                    var result = ModSingleton<OracleSystem>.Instance.GetRoleCandidate(MyPlayer, target, NumOfCandidatesOption);
                    var shuffled = result.Shuffled();
                    oracleButton.UpdateUsesIcon((this.leftUses - 1).ToString());
                    RpcOracle.Invoke((MyPlayer, target, shuffled));
                    (oracleButton.CoolDownTimer as GameTimer)?.Expand(OracleAdditionalCooldownOption);
                    StatsOracle.Progress();
                    return true;
                }
                oracleButton.OnClick = (button) => {
                    if (OracleDurationOption > 0f)
                    {
                        playerTracker.KeepAsLongAsPossible = true;
                        button.StartEffect();
                    }
                    else
                    {
                        PredicateRole();
                        button.StartCoolDown();
                    }
                };
                oracleButton.OnEffectEnd = (button) =>
                {
                    playerTracker.KeepAsLongAsPossible = false;
                    if (playerTracker.CurrentTarget == null) return;
                    if (MeetingHud.Instance) return;

                    if (GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, playerTracker.CurrentTarget, new(RealPlayerOnly: true))).IsCanceled ?? true) return;

                    if (!button.EffectTimer!.IsProgressing) PredicateRole();
                    oracleButton.StartCoolDown();
                };
                oracleButton.OnUpdate = (button) => {
                    if (!button.IsInEffect) return;
                    if (playerTracker.CurrentTarget == null) button.InterruptEffect();
                };
                oracleButton.EffectTimer = NebulaAPI.Modules.Timer(this, OracleDurationOption);
                oracleButton.SetLabel("oracle");
                oracleButton.SetAsUsurpableButton(this);
            }
        }

        void AddResult(byte targetId, DefinedRole[] result)
        {
            divideResults[targetId] = (
                        result,
                        string.Join(", ", result.Select(r => r.DisplayColoredName)),
                        string.Join(", ", result.Select(r => result.Length >= 2 ? r.GetRoleIconTagSmall() + " " + r.DisplayColoredShort : r.DisplayColoredName))
                        );
        }

        static readonly RemoteProcess<(GamePlayer oracle, GamePlayer target, DefinedRole[] result)> RpcOracle = new("oracle.oracle", (message, _) => { 
            if(message.oracle.TryGetAbility<Ability>(out var oracle))
            {
                oracle.AddResult(message.target.PlayerId, message.result);
                oracle.leftUses--;
            }
        });

        [Local]
        void ReflectRoleName(PlayerSetFakeRoleNameEvent ev)
        {
            if (divideResults.TryGetValue(ev.Player.PlayerId, out var roleName)) ev.Alternate(ev.InMeeting ? roleName.longName : roleName.shortName);
        }

        #region Titles

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (MeetingHudExtension.LastVotedForMap.TryGetValue(MyPlayer.PlayerId, out var voteTo) && ev.Exiled.Find(p => p.PlayerId == voteTo, out var exiled) && divideResults.ContainsKey((byte)voteTo))
            {
                //占ったプレイヤーを追放したとき
                if(exiled.IsTrueCrewmate) new StaticAchievementToken("oracle.another1");
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerKillPlayerEvent ev)
        {
            if(ev.Dead.PlayerState == PlayerStates.Guessed && divideResults.ContainsKey((byte)ev.Dead.PlayerId))
            {
                if (ev.Dead.IsImpostor && ev.Murderer.IsTrueCrewmate) GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev => { 
                    if(ev.EndState.Winners.Test(MyPlayer) && !GamePlayer.AllPlayers.Any(p => p.PlayerState == PlayerStates.Exiled && (p.MyKiller?.IsTrueCrewmate ?? false)))
                    {
                        new StaticAchievementToken("oracle.challenge");
                    }
                }, this);
            }
            if(ev.Dead.PlayerState == PlayerStates.Misguessed && ev.Murderer.AmOwner) new StaticAchievementToken("oracle.another1");
        }
        #endregion Titles
    }
}

