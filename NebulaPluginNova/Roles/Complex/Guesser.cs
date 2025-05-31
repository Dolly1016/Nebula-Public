using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Roles.Impostor;
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
    static TextAttributeOld ButtonAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.3f, 0.3f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(2f, 1f, 2f);
    static public MetaScreen OpenRoleSelectWindow(Func<DefinedRole, bool> predicate, string underText, Action<DefinedRole> onSelected)
    {
        var window = MetaScreen.GenerateWindow(new(7.6f, 4.2f), HudManager.Instance.transform, new Vector3(0, 0, -50f), true, false);

        MetaWidgetOld widget = new();

        MetaWidgetOld inner = new();
        inner.Append(Roles.AllRoles.Where(predicate), r => new MetaWidgetOld.Button(() => onSelected.Invoke(r), ButtonAttribute) { RawText = r.DisplayColoredName, PostBuilder = (_, renderer, _) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask }, 4, -1, 0, 0.59f);
        MetaWidgetOld.ScrollView scroller = new(new(6.9f, 3.8f), inner, true) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
        widget.Append(scroller);

        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { MyText = new RawTextComponent(underText), Alignment = IMetaWidgetOld.AlignmentOption.Center });

        window.SetWidget(widget);

        IEnumerator CoCloseOnResult()
        {
            while (MeetingHud.Instance.state != MeetingHud.VoteStates.Results) yield return null;

            window.CloseScreen();
        }

        window.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());


        return window;
    }
}

[NebulaRPCHolder]
static file class GuesserSystem
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

    static public MetaScreen OpenGuessWindow(int leftGuessPerMeeting, int leftGuess,Action<DefinedRole> onSelected)
    {
        string leftStr;
        if (leftGuessPerMeeting < leftGuess)
            leftStr = $"{leftGuessPerMeeting.ToString()} ({leftGuess.ToString()})";
        else
            leftStr = leftGuess.ToString();

        return MeetingRoleSelectWindow.OpenRoleSelectWindow(r => r.CanBeGuess && r.IsSpawnable, Language.Translate("role.guesser.leftGuess") + " : " + leftStr, onSelected);
    }

    static public void OnMeetingStart(int leftGuess,Action guessDecrementer, Func<bool>? guessIf = null)
    {
        bool awareOfUsurpation = false;
        int leftGuessPerMeeting = Guesser.NumOfGuessPerMeetingOption;

        NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>()?.RegisterMeetingAction(new(MeetingPlayerButtonManager.Icons.AsLoader(0),
            state => {
                var p = state.MyPlayer;
                LastGuesserWindow = OpenGuessWindow(leftGuessPerMeeting, leftGuess, (r) =>
                {
                    if (PlayerControl.LocalPlayer.Data.IsDead) return;
                    if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                    if (guessIf?.Invoke() ?? true)
                    {
                        GuesserModifier.StatsAllGuessed.Progress();
                        if (p?.Role.ExternalRecognitionRole == r)
                        {
                            NebulaAPI.CurrentGame?.LocalPlayer.MurderPlayer(p!, PlayerState.Guessed, EventDetail.Guess, KillParameter.MeetingKill);
                        }
                        else
                        {
                            GuesserModifier.StatsMisguessed.Progress();
                            NebulaAPI.CurrentGame?.LocalPlayer.MurderPlayer(NebulaAPI.CurrentGame.LocalPlayer, PlayerState.Misguessed, EventDetail.Missed, KillParameter.MeetingKill);
                            RpcShareExtraInfo.Invoke((NebulaAPI.CurrentGame!.LocalPlayer, p!, r));
                        }
                    }
                    else
                    {
                        NebulaAsset.PlaySE(NebulaAudioClip.ButtonBreaking, volume: 1f);
                        awareOfUsurpation = true;
                    }
                    //のこり推察数を減らす
                    guessDecrementer.Invoke();
                    leftGuess--;
                    leftGuessPerMeeting--;

                    if (LastGuesserWindow) LastGuesserWindow.CloseScreen();
                    LastGuesserWindow = null!;
                });
            },
            p => !awareOfUsurpation && !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && leftGuess > 0 && leftGuessPerMeeting > 0 && !PlayerControl.LocalPlayer.Data.IsDead && GameOperatorManager.Instance!.Run(new PlayerCanGuessPlayerLocalEvent(NebulaAPI.CurrentGame!.LocalPlayer, p.MyPlayer, true)).CanGuess
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
        if (LastGuesserWindow) LastGuesserWindow.CloseScreen();
        LastGuesserWindow = null!;
    }

    static public void OnGameEnd(GamePlayer myInfo)
    {
        var guessKills = NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.PlayerState == PlayerState.Guessed && p.MyKiller == myInfo) ?? 0;
        if (guessKills >= 1) new StaticAchievementToken("guesser.common1");
        if (guessKills >= 3) new StaticAchievementToken("guesser.challenge");
    }
}

public class Guesser : DefinedSingleAbilityRoleTemplate<Guesser.Ability>, HasCitation, DefinedRole
{
    public bool IsEvil => Category == RoleCategory.ImpostorRole;

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, NumOfGuessOption));
    bool DefinedRole.IsJackalizable => IsEvil;

    static internal IntegerConfiguration NumOfGuessOption = NebulaAPI.Configurations.Configuration("options.role.guesser.numOfGuess", (1, 15), 3);
    static internal IntegerConfiguration NumOfGuessPerMeetingOption = NebulaAPI.Configurations.Configuration("options.role.guesser.numOfGuessPerMeeting", (1, 15), 1);
    static internal BoolConfiguration CanCallEmergencyMeetingOption = NebulaAPI.Configurations.Configuration("options.role.guesser.canCallEmergencyMeeting", true);
    static internal IConfiguration GuessableFilterEditorOption = NebulaAPI.Configurations.Configuration(() => null, () => NebulaAPI.GUI.LocalizedButton(Virial.Media.GUIAlignment.Center, NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.OptionsTitleHalf), "role.guesser.guessableFilter", _ => OpenFilterEditor()));

    static public Guesser MyNiceRole = new(false);
    static public Guesser MyEvilRole = new(true);

    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable != GuesserModifier.MyRole;
    
    public Guesser(bool isEvil) : base(
        isEvil ? "evilGuesser" : "niceGuesser", 
        isEvil ? new(Palette.ImpostorRed) : new(255, 255, 0f), 
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
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);


    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public int LeftGuess = Guesser.NumOfGuessOption;

        public Instance(GamePlayer player,int[] arguments) : base(player){
            if (arguments.Length > 0)LeftGuess = arguments[0];
        }

        void RuntimeAssignable.OnActivated() { }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += " ⊕".Color(MyRole.UnityColor);
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