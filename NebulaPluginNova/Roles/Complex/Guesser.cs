using Nebula.Behaviour;
using Nebula.Game.Statistics;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Complex;

static file class GuesserSystem
{
    static TextAttributeOld ButtonAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.3f, 0.3f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(2f, 1f, 2f);
    public static MetaScreen LastGuesserWindow = null!;

    static public MetaScreen OpenGuessWindow(int leftGuessPerMeeting, int leftGuess,Action<DefinedRole> onSelected)
    {
        var window = MetaScreen.GenerateWindow(new(7.4f, 4.2f), HudManager.Instance.transform, new Vector3(0, 0, -50f), true, false);

        MetaWidgetOld widget = new();

        MetaWidgetOld inner = new();
        inner.Append(Roles.AllRoles.Where(r => r.CanBeGuess && r.IsSpawnable), r => new MetaWidgetOld.Button(() => onSelected.Invoke(r), ButtonAttribute) { RawText = r.DisplayColoredName, PostBuilder = (_, renderer, _) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask }, 4, -1, 0, 0.59f);
        MetaWidgetOld.ScrollView scroller = new(new(6.6f, 3.8f), inner, true) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
        widget.Append(scroller);

        string leftStr;
        if (leftGuessPerMeeting < leftGuess)
            leftStr = $"{leftGuessPerMeeting.ToString()} ({leftGuess.ToString()})";
        else
            leftStr = leftGuess.ToString();

        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { MyText = new CombinedComponent(new TranslateTextComponent("role.guesser.leftGuess"), new RawTextComponent(" : " + leftStr)), Alignment = IMetaWidgetOld.AlignmentOption.Center });

        window.SetWidget(widget);

        IEnumerator CoCloseOnResult()
        {
            while (MeetingHud.Instance.state != MeetingHud.VoteStates.Results) yield return null;

            window.CloseScreen();
        }

        window.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());


        return window;
    }

    static private SpriteLoader targetSprite = SpriteLoader.FromResource("Nebula.Resources.TargetIcon.png", 115f);
    static public void OnMeetingStart(int leftGuess,Action guessDecrementer)
    {
        int leftGuessPerMeeting = Guesser.NumOfGuessPerMeetingOption;

        NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>()?.RegisterMeetingAction(new(targetSprite,
            state => {
                var p = state.MyPlayer;
                LastGuesserWindow = OpenGuessWindow(leftGuessPerMeeting, leftGuess, (r) =>
                {
                    if (PlayerControl.LocalPlayer.Data.IsDead) return;
                    if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                    if (p?.Role.Role == r)
                        NebulaAPI.CurrentGame?.LocalPlayer.MurderPlayer(p!, PlayerState.Guessed, EventDetail.Guess, KillParameter.MeetingKill);
                    else
                        NebulaAPI.CurrentGame?.LocalPlayer.MurderPlayer(NebulaAPI.CurrentGame.LocalPlayer, PlayerState.Misguessed, EventDetail.Missed, KillParameter.MeetingKill);

                    //のこり推察数を減らす
                    guessDecrementer.Invoke();
                    leftGuess--;
                    leftGuessPerMeeting--;

                    if (LastGuesserWindow) LastGuesserWindow.CloseScreen();
                    LastGuesserWindow = null!;
                });
            },
            p => !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && leftGuess > 0 && leftGuessPerMeeting > 0 && !PlayerControl.LocalPlayer.Data.IsDead
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
        var guessKills = NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.PlayerState == PlayerState.Guessed && p.MyKiller == myInfo) ?? 0;
        if (guessKills >= 1) new StaticAchievementToken("guesser.common1");
        if (guessKills >= 3) new StaticAchievementToken("guesser.challenge");
    }
}

public class Guesser : DefinedRoleTemplate, HasCitation, DefinedRole
{
    public bool IsEvil { get; private set; }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => IsEvil ? new EvilInstance(player,arguments) : new NiceInstance(player,arguments);

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
        IsEvil = isEvil;
        ConfigurationHolder?.AddTags(ConfigurationTags.TagChaotic);
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!, GuesserModifier.MyRole.ConfigurationHolder!]);
    }

    private static void OpenFilterEditor()
    {
        RoleOptionHelper.OpenFilterScreen("guessableFilter", Roles.AllRoles.Where(r => r.CanBeGuessDefault), r => r.CanBeGuess, (r, val) => r.CanBeGuessVariable!.CurrentValue = val,  r => r.CanBeGuessVariable!.CurrentValue = !r.CanBeGuess);
    }

    public class NiceInstance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyNiceRole;
        private int leftGuess = NumOfGuessOption;
        public NiceInstance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length >= 1) leftGuess = arguments[0];
        }

        void RuntimeAssignable.OnActivated() {}

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => GuesserSystem.OnMeetingStart(leftGuess, () => leftGuess--);


        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => GuesserSystem.OnDead();


        [Local]
        void OnGameEnd(GameEndEvent ev) => GuesserSystem.OnGameEnd(MyPlayer);

        bool RuntimeAssignable.CanCallEmergencyMeeting => CanCallEmergencyMeetingOption;
    }

    public class EvilInstance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyEvilRole;
        private int leftGuess = NumOfGuessOption;
        public EvilInstance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length >= 1) leftGuess = arguments[0];
        }

        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => GuesserSystem.OnMeetingStart(leftGuess, () => leftGuess--);


        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => GuesserSystem.OnDead();


        [Local]
        void OnGameEnd(GameEndEvent ev) => GuesserSystem.OnGameEnd(MyPlayer);

        bool RuntimeAssignable.CanCallEmergencyMeeting => CanCallEmergencyMeetingOption;
    }
}

public class GuesserModifier : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, HasCitation
{
    static public GuesserModifier MyRole = new GuesserModifier();
    private GuesserModifier() : base("guesser", "GSR", new(255, 255, 0), [Guesser.NumOfGuessOption, Guesser.NumOfGuessPerMeetingOption, Guesser.GuessableFilterEditorOption]) {
        ConfigurationHolder?.ScheduleAddRelated(() => [Guesser.MyNiceRole.ConfigurationHolder!, Guesser.MyEvilRole.ConfigurationHolder!]);
    }
    
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
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