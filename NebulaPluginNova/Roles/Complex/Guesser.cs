using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Roles.Impostor;
using System.Data;
using System.Linq;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Complex;

static public class MeetingRoleSelectWindow
{
    static TextAttributeOld ButtonAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.05f, 0.3f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(2f, 1f, 2f);
    static TextAttributeOld TabAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    static public MetaScreen OpenRoleSelectWindow(IEnumerable<DefinedRole>? roles, Predicate<DefinedRole>? predicate, bool impRolesArrangeAtFirst, string underText, Action<DefinedRole> onSelected)
        => OpenRoleSelectWindowUsingTabs(roles, [(null, predicate)], impRolesArrangeAtFirst, underText, onSelected);

    static public MetaScreen OpenRoleSelectWindowUsingTabs(IEnumerable<DefinedRole>? roles, (string? tab, Predicate<DefinedRole>? predicate)[] tabs, bool impRolesArrangeAtFirst, string underText, Action<DefinedRole> onSelected)
    {
        var window = MetaScreen.GenerateWindow(new(7.6f, 4.2f), HudManager.Instance.transform, new Vector3(0, 0, -50f), true, false);

        MetaWidgetOld widget = new();

        MetaWidgetOld inner = new();

        if(roles == null)
        {
            HashSet<DefinedRole> roleSet = [];
            foreach (var r in Roles.AllRoles) foreach (var abilityRole in r.GetGuessableAbilityRoles()) roleSet.Add(abilityRole);
            foreach (var type in AssignmentType.AllTypes)
            {
                if (!type.CanGuessAsAbility) continue;
                foreach (var r in Roles.AllRoles)
                {
                    if (type.Predicate.Invoke(r.AssignmentStatus, r) && r.GetCustomAllocationParameters(type)?.RoleCountSum > 0) roleSet.Add(r);
                }
            }
            roles = roleSet;
        }
        
        int CategoryToInt(RoleCategory roleCategory) => roleCategory switch
        {
            RoleCategory.ImpostorRole => impRolesArrangeAtFirst ? 0 : 1,
            RoleCategory.CrewmateRole => impRolesArrangeAtFirst ? 1 : 0,
            _ => 2
        };

        bool isFirst = true;
        foreach (var tab in tabs)
        {
            var ary = roles.Where(r => tab.predicate?.Invoke(r) ?? true).ToArray();
            ary.Sort((r1, r2) =>
            {
                if (r1.Category == r2.Category) return r1.InternalName.CompareTo(r2.InternalName);
                return CategoryToInt(r1.Category).CompareTo(CategoryToInt(r2.Category));
            });

            if (isFirst) isFirst = false;
            else inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));
            
            if (tab.tab != null) inner.Append(new MetaWidgetOld.Text(TabAttribute) { MyText = new RawTextComponent(tab.tab), Alignment = IMetaWidgetOld.AlignmentOption.Center });
            inner.Append(ary, r => new CombinedWidgetOld(new MetaWidgetOld.HorizonalMargin(0.1f), new MetaWidgetOld.Button(() => onSelected.Invoke(r), ButtonAttribute) { RawText = r.DisplayColoredName, TextHorizonotalExtraMargin = 0.15f, PostBuilder = (button, renderer, text) =>
            {
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                button.transform.localPosition += new Vector3(0.05f, 0f, 0f);
                text.transform.localPosition += new Vector3(0.072f, 0f, 0f);

                var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", button.transform, new(-0.65f, 0f, -0.1f));
                icon.sprite = r.GetRoleIcon()?.GetSprite();
                icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                icon.material = RoleIcon.GetRoleIconMaterial(r, 0.8f);
                icon.transform.localScale = new(0.253f, 0.253f, 1f);
                icon.SetBothOrder(15);
            }
            }), 4, -1, 0, 0.59f);
        }

        MetaWidgetOld.ScrollView scroller = new(new(6.9f, 3.8f), inner, true) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
        widget.Append(scroller);

        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { MyText = new RawTextComponent(underText), Alignment = IMetaWidgetOld.AlignmentOption.Center });

        window.SetWidget(widget);

        IEnumerator CoCloseOnResult()
        {
            if (MeetingHud.Instance.AsBoolFast())
            {
                while (MeetingHud.Instance.state != MeetingHud.MeetingStates.Results) yield return null;
            }
            else
            {
                while (!MeetingHud.Instance.AsBoolFast()) yield return null;
            }
            window.CloseScreen();
        }

        window.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());


        return window;
    }
}

/// <summary>
/// 会議中の推測の振る舞いを定めます。
/// </summary>
internal class GuessMeetingSetting(int leftGuess, int guessesPerMeeting)
{
    /// <summary>
    /// 残りの推測回数。
    /// </summary>
    public int LeftGuess { get; } = leftGuess;
    /// <summary>
    /// 1回の会議で推測できる回数。
    /// </summary>
    public int GuessesPerMeeting { get; } = guessesPerMeeting;
    public GameStatsEntry? StatsGuess { get; init; } = null;
    public GameStatsEntry? StatsMisguess { get; init; } = null;
    /// <summary>
    /// 推測を行った後に呼び出されます。引数は推測が正解だったかどうかです。
    /// </summary>
    public Action<bool>? OnGuessed { get; init; } = null;
    /// <summary>
    /// 推測を有効に行えるとき<c>true</c>を返します。<c>false</c>を返した場合、推測は不発に終わり、この会議中は以降推測できません。
    /// </summary>
    public Func<bool>? GuessIf { get; init; } = null;
    /// <summary>
    /// 推測を誤った際に呼び出されます。推測者が死亡すべきとき<c>true</c>を返します。未設定の場合は常に死亡します。
    /// </summary>
    public Func<bool>? OnMisguess { get; init; } = null;
    /// <summary>
    /// 推測を誤った場合にも推測回数を消費するかどうか。
    /// </summary>
    public bool ConsumeOnMisguess { get; init; } = true;
    /// <summary>
    /// 推測ボタンを表示するための追加の条件。
    /// </summary>
    public Func<bool>? CanGuess { get; init; } = null;
    /// <summary>
    /// 推測ウィンドウの下部に表示するテキスト。引数はこの会議で残っている推測回数と、全体で残っている推測回数です。
    /// </summary>
    public Func<int, int, string>? UnderText { get; init; } = null;
}

[NebulaRPCHolder]
static internal class GuesserSystem
{
    private record MisguessedExtraDeadInfo(GamePlayer To, DefinedRole Role) : GamePlayer.ExtraDeadInfo(PlayerStates.Misguessed)
    {
        public override string ToStateText() => To.PlayerName + " as " + Role.DisplayColoredName;
    }
    static private RemoteProcess<(GamePlayer guesser, GamePlayer to, DefinedRole role)> RpcShareExtraInfo = new("ShareExInfoGuess",
        (message, _) => {
            message.guesser.PlayerStateExtraInfo = new MisguessedExtraDeadInfo(message.to, message.role);
        }
    );

    public static MetaScreen LastGuesserWindow = null!;

    static public string GetLeftGuessText(int leftGuessPerMeeting, int leftGuess)
    {
        if (leftGuessPerMeeting < leftGuess)
            return $"{leftGuessPerMeeting.ToString()} ({leftGuess.ToString()})";
        else
            return leftGuess.ToString();
    }

    static public MetaScreen OpenGuessWindow(string underText, Action<DefinedRole> onSelected)
    {
        return MeetingRoleSelectWindow.OpenRoleSelectWindow(null, r => r.CanBeGuess, GamePlayer.LocalPlayer?.FeelBeTrueCrewmate ?? false, underText, onSelected);
    }

    static public void OnMeetingStart(int leftGuess,Action guessDecrementer, Func<bool>? guessIf = null)
    {
        OnMeetingStart(new GuessMeetingSetting(leftGuess, Guesser.NumOfGuessPerMeetingOption)
        {
            StatsGuess = GuesserModifier.StatsAllGuessed,
            StatsMisguess = GuesserModifier.StatsMisguessed,
            OnGuessed = _ => guessDecrementer.Invoke(),
            GuessIf = guessIf,
        });
    }

    static public void OnMeetingStart(GuessMeetingSetting setting)
    {
        bool awareOfUsurpation = false;
        int leftGuess = setting.LeftGuess;
        int leftGuessPerMeeting = setting.GuessesPerMeeting;

        NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>()?.RegisterMeetingAction(new(MeetingPlayerButtonManager.Icons.AsLoader(0),
            state => {
                var p = state.MyPlayer;
                string underText = setting.UnderText?.Invoke(leftGuessPerMeeting, leftGuess) ?? (Language.Translate("role.guesser.leftGuess") + " : " + GetLeftGuessText(leftGuessPerMeeting, leftGuess));
                LastGuesserWindow = OpenGuessWindow(underText, (r) =>
                {
                    if (GamePlayer.LocalPlayer?.IsDead ?? false) return;
                    var meetingHud = MeetingHud.Instance;
                    var meetingState = meetingHud.state;
                    if (!(meetingState == MeetingHud.MeetingStates.Voted || meetingState == MeetingHud.MeetingStates.NotVoted)) return;
                    if (!MeetingHudExtension.CanUseAbilityForLocal(p, true)) return;

                    bool isCorrect = false;
                    if (setting.GuessIf?.Invoke() ?? true)
                    {
                        var localPlayer = GamePlayer.LocalPlayer;
                        setting.StatsGuess?.Progress();
                        if (Guesser.AbilityGuessOption ? p.Role.CheckGuessAbility(r) : (p.Role.ExternalRecognitionRole == r))
                        {
                            isCorrect = true;
                            localPlayer?.MurderPlayer(p, PlayerState.Guessed, EventDetail.Guess, KillParameter.MeetingKill, KillCondition.BothAlive);
                        }
                        else
                        {
                            setting.StatsMisguess?.Progress();
                            if (setting.OnMisguess?.Invoke() ?? true)
                            {
                                localPlayer?.MurderPlayer(localPlayer, PlayerState.Misguessed, EventDetail.Missed, KillParameter.MeetingKill, KillCondition.BothAlive);
                                RpcShareExtraInfo.Invoke((localPlayer!, p!, r));
                            }
                        }
                    }
                    else
                    {
                        NebulaAsset.PlaySE(NebulaAudioClip.ButtonBreaking, volume: 1f);
                        awareOfUsurpation = true;
                    }
                    setting.OnGuessed?.Invoke(isCorrect);
                    //のこり推察数を減らす
                    if (isCorrect || setting.ConsumeOnMisguess)
                    {
                        leftGuess--;
                        leftGuessPerMeeting--;
                    }

                    if (LastGuesserWindow.AsBoolFast()) LastGuesserWindow.CloseScreen();
                    LastGuesserWindow = null!;
                });
            },
            p => !awareOfUsurpation && (setting.CanGuess?.Invoke() ?? true) && !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && leftGuess > 0 && leftGuessPerMeeting > 0 && !(GamePlayer.LocalPlayer?.IsDead ?? true) && GameOperatorManager.Instance!.Run(new PlayerCanGuessPlayerLocalEvent(GamePlayer.LocalPlayer, p.MyPlayer, true)).CanGuess
            ));
        
        /*
        List<GameObject> guessIcons = new();

        foreach (var playerVoteArea in MeetingHud.Instance.playerStates)
        {
            if (playerVoteArea.AmDead || playerVoteArea.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId) continue;

            GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
            guessIcons.Add(targetBox);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new Vector3(-0.95f, 0.03f, -1f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = targetSprite.GetSprite();
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();


            var player = NebulaGameManager.Instance?.GetModPlayerInfo(playerVoteArea.TargetPlayerId);
            button.OnClick.AddListener(() =>
            {
                if (PlayerControl.LocalPlayer.Data.IsDead) return;
                if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                LastGuesserWindow = OpenGuessWindow(leftGuessPerMeeting, leftGuess, (r) =>
                {
                    if (PlayerControl.LocalPlayer.Data.IsDead) return;
                    if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                    if (player?.Role.Role == r)
                        PlayerControl.LocalPlayer.ModMeetingKill(player!.MyControl, true, PlayerState.Guessed, EventDetail.Guess);
                    else
                        PlayerControl.LocalPlayer.ModMeetingKill(PlayerControl.LocalPlayer, true, PlayerState.Misguessed, EventDetail.Missed);

                    //のこり推察数を減らす
                    guessDecrementer.Invoke();
                    leftGuess--;
                    leftGuessPerMeeting--;

                    if (leftGuess <= 0 || leftGuessPerMeeting <= 0) foreach (var obj in guessIcons) GameObject.Destroy(obj);
                    

                    if (LastGuesserWindow) LastGuesserWindow.CloseScreen();
                    LastGuesserWindow = null!;
                });
            });
        }
        */
    }

    static public void OnDead()
    {
        if (LastGuesserWindow.AsBoolFast()) LastGuesserWindow.CloseScreen();
        LastGuesserWindow = null!;
    }

    static public void OnGameEnd(GamePlayer myInfo)
    {
        var guessKills = NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.PlayerState == PlayerState.Guessed && p.MyKiller == myInfo) ?? 0;
        if (guessKills >= 1) new StaticAchievementToken("guesser.common1");
        if (guessKills >= 3) new StaticAchievementToken("guesser.challenge");
    }
}

public class Guesser : DefinedSingleAbilityRoleTemplate<Guesser.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    static internal Image IconImage = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/guesser.png");
    public bool IsEvil => Category == RoleCategory.ImpostorRole;
    Image? DefinedAssignable.IconImage => IconImage;

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, NumOfGuessOption));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => IsEvil ? AbilityAssignmentStatus.KillersSide : AbilityAssignmentStatus.CanLoadToMadmate;

    static internal IntegerConfiguration NumOfGuessOption = NebulaAPI.Configurations.Configuration("options.role.guesser.numOfGuess", (1, 15), 3);
    static internal IntegerConfiguration NumOfGuessPerMeetingOption = NebulaAPI.Configurations.Configuration("options.role.guesser.numOfGuessPerMeeting", (1, 15), 1);
    static internal BoolConfiguration CanCallEmergencyMeetingOption = NebulaAPI.Configurations.Configuration("options.role.guesser.canCallEmergencyMeeting", true);
    static internal BoolConfiguration AbilityGuessOption = NebulaAPI.Configurations.Configuration("options.role.guesser.abilityGuess", true);
    static internal IConfiguration GuessableFilterEditorOption = NebulaAPI.Configurations.Configuration(() => null, () => NebulaAPI.GUI.LocalizedButton(Virial.Media.GUIAlignment.Center, NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.OptionsTitleHalf), "role.guesser.guessableFilter", _ => OpenFilterEditor()));

    static public Guesser MyNiceRole = new(false);
    static public Guesser MyEvilRole = new(true);

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;

    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable != GuesserModifier.MyRole;
    
    public Guesser(bool isEvil) : base(
        isEvil ? "evilGuesser" : "niceGuesser", 
        isEvil ? VColor.ImpostorColor : new(1f, 1f, 0f), 
        isEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole,
        isEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam,
        [NumOfGuessOption, NumOfGuessPerMeetingOption, CanCallEmergencyMeetingOption, GuessableFilterEditorOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagChaotic);
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!, GuesserModifier.MyRole.ConfigurationHolder!]);
    }

    private static void OpenFilterEditor()
    {
        RoleOptionHelper.OpenFilterScreen("guessableFilter", Roles.AllRoles.Where(r => r.CanBeGuessDefault), r => r.CanBeGuess, (r, val) => r.CanBeGuessVariable!.CurrentValue = val,  r => r.CanBeGuessVariable!.CurrentValue = !r.CanBeGuess);
    }

    IEnumerable<DefinedAssignable> DefinedAssignable.AchievementGroups => [MyNiceRole, MyNiceRole, GuesserModifier.MyRole];
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private int leftGuess = NumOfGuessOption;
        private bool awareOfUsurpation = false;
        public Ability(GamePlayer player, bool isUsurped, int leftGuess) : base(player, isUsurped)
        {
            this.leftGuess = leftGuess;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if(!awareOfUsurpation) GuesserSystem.OnMeetingStart(leftGuess, () => { leftGuess--; awareOfUsurpation |= IsUsurped; }, () => !IsUsurped);
        }


        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => GuesserSystem.OnDead();


        [Local]
        void OnGameEnd(GameEndEvent ev) => GuesserSystem.OnGameEnd(MyPlayer);

        bool IPlayerAbility.BlockCallingEmergencyMeeting => !CanCallEmergencyMeetingOption;
    }
}

public class GuesserModifier : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, HasCitation
{
    static public GuesserModifier MyRole = new GuesserModifier();
    static internal GameStatsEntry StatsAllGuessed = NebulaAPI.CreateStatsEntry("stats.guesser.allGuess", GameStatsCategory.Roles, MyRole);
    static internal GameStatsEntry StatsMisguessed = NebulaAPI.CreateStatsEntry("stats.guesser.misguessed", GameStatsCategory.Roles, MyRole);

    private GuesserModifier() : base("guesser", "GSR", new(255, 255, 0), [Guesser.NumOfGuessOption, Guesser.NumOfGuessPerMeetingOption, Guesser.CanCallEmergencyMeetingOption, Guesser.GuessableFilterEditorOption]) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Guesser.MyNiceRole.ConfigurationHolder!, Guesser.MyEvilRole.ConfigurationHolder!]);
    }

    IEnumerable<DefinedAssignable> DefinedAssignable.AchievementGroups => [Guesser.MyNiceRole, Guesser.MyNiceRole, MyRole];

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    Image? DefinedAssignable.IconImage => Guesser.IconImage;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);


    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public int LeftGuess = Guesser.NumOfGuessOption;

        public Instance(GamePlayer player,int[] arguments) : base(player){
            if (arguments.Length > 0)LeftGuess = arguments[0];
        }

        void RuntimeAssignable.OnActivated() { }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += MyRole.GetRoleIconTagSmall();
        }


        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            //追加役職Guesserは役職としてのGuesserがある場合効果を発揮しない
            if (MyPlayer.Role.Role is Guesser) return;
            GuesserSystem.OnMeetingStart(LeftGuess, () => LeftGuess--);
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => GuesserSystem.OnDead();

        [Local]
        void OnGameEnd(GameEndEvent ev) => GuesserSystem.OnGameEnd(MyPlayer);

        string? RuntimeModifier.DisplayIntroBlurb => Language.Translate("role.guesser.blurb");

        bool RuntimeAssignable.CanCallEmergencyMeeting => Guesser.CanCallEmergencyMeetingOption;
    }
}