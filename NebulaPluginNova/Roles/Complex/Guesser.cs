using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Configuration;
using Nebula.Game;
using Nebula.Roles.Neutral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;
using Virial.Game;
using static Mono.CSharp.Parameter;

namespace Nebula.Roles.Complex;

static file class GuesserSystem
{
    static TextAttributeOld ButtonAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.3f, 0.3f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(2f, 1f, 2f);
    public static MetaScreen LastGuesserWindow = null!;

    static public MetaScreen OpenGuessWindow(int leftGuessPerMeeting, int leftGuess,Action<AbstractRole> onSelected)
    {
        var window = MetaScreen.GenerateWindow(new(7.4f, 4.2f), HudManager.Instance.transform, new Vector3(0, 0, -50f), true, false);

        MetaWidgetOld widget = new();

        MetaWidgetOld inner = new();
        inner.Append(Roles.AllRoles.Where(r => r.CanBeGuess && r.IsSpawnable), r => new MetaWidgetOld.Button(() => onSelected.Invoke(r), ButtonAttribute) { RawText = r.DisplayName.Color(r.RoleColor), PostBuilder = (_, renderer, _) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask }, 4, -1, 0, 0.59f);
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
        int leftGuessPerMeeting = Guesser.NumOfGuessPerMeetingOption.GetMappedInt();

        NebulaGameManager.Instance?.MeetingPlayerButtonManager.RegisterMeetingAction(new(targetSprite,
            state => {
                var p = state.MyPlayer;
                LastGuesserWindow = OpenGuessWindow(leftGuessPerMeeting, leftGuess, (r) =>
                {
                    if (PlayerControl.LocalPlayer.Data.IsDead) return;
                    if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                    if (p?.Role.Role == r)
                        PlayerControl.LocalPlayer.ModMeetingKill(p!.MyControl, true, PlayerState.Guessed, EventDetail.Guess);
                    else
                        PlayerControl.LocalPlayer.ModMeetingKill(PlayerControl.LocalPlayer, true, PlayerState.Misguessed, EventDetail.Missed);

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

    static public void OnGameEnd(PlayerModInfo myInfo)
    {
        var guessKills = NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.MyState == PlayerState.Guessed && p.MyKiller == myInfo) ?? 0;
        if (guessKills >= 1) new StaticAchievementToken("guesser.common1");
        if (guessKills >= 3) new StaticAchievementToken("guesser.challenge");
    }
}

public class Guesser : ConfigurableStandardRole, HasCitation
{
    static public Guesser MyNiceRole = new(false);
    static public Guesser MyEvilRole = new(true);

    public bool IsEvil { get; private set; }
    public override RoleCategory Category => IsEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole;

    public override string LocalizedName => IsEvil ? "evilGuesser" : "niceGuesser";
    public override Color RoleColor => IsEvil ? Palette.ImpostorRed : new Color(1f, 1f, 0f);
    public override RoleTeam Team => IsEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { if(MyNiceRole != this) yield return MyNiceRole; if (MyEvilRole != this) yield return MyEvilRole; yield return GuesserModifier.MyRole; }
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => IsEvil ? new EvilInstance(player,arguments) : new NiceInstance(player,arguments);

    static public NebulaConfiguration NumOfGuessOption = null!;
    static public NebulaConfiguration NumOfGuessPerMeetingOption = null!;
    static public NebulaConfiguration CanCallEmergencyMeetingOption = null!;
    static public NebulaConfiguration GuessableFilterEditorOption = null!;

    public override bool CanLoadDefault(IntroAssignableModifier modifier) => modifier != GuesserModifier.MyRole;
    
    public Guesser(bool isEvil)
    {
        IsEvil = isEvil;
    }

    public static NebulaConfiguration GenerateCommonEditor(ConfigurationHolder holder) => GenerateCommonEditor(holder, NumOfGuessOption, NumOfGuessPerMeetingOption, CanCallEmergencyMeetingOption, GuessableFilterEditorOption);

    private static void OpenFilterEditor(MetaScreen? screen = null)
    {
        if (!screen) screen = MetaScreen.GenerateWindow(new Vector2(5f, 3.2f), HudManager.Instance.transform, Vector3.zero, true, true);
        
        MetaWidgetOld inner = new();
        inner.Append(Roles.AllRoles.Where(r => r.CanBeGuessOption != null), (role) => new MetaWidgetOld.Button(() => { role.CanBeGuessOption?.ChangeValue(true); OpenFilterEditor(screen); }, NebulaSettingMenu.RelatedInsideButtonAttr)
        {
            RawText = role.DisplayName.Color(role.RoleColor),
            PostBuilder = (button, renderer, text) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask,
            Alignment = IMetaWidgetOld.AlignmentOption.Center,
            Color = role.CanBeGuessOption! ? Color.white : new Color(0.14f, 0.14f, 0.14f)
        }, 3, -1, 0, 0.6f);

        screen!.SetWidget(new MetaWidgetOld.ScrollView(new(5f, 3.1f), inner, true) { ScrollerTag = "guessableFilter" });
    }

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagChaotic);

        NumOfGuessOption ??= new NebulaConfiguration(null, "role.guesser.numOfGuess", null, 1, 15, 3, 3);
        NumOfGuessPerMeetingOption ??= new NebulaConfiguration(null, "role.guesser.numOfGuessPerMeeting", null, 1, 15, 1, 1);
        CanCallEmergencyMeetingOption ??= new NebulaConfiguration(null, "role.guesser.canCallEmergencyMeeting", null, true, true);
        GuessableFilterEditorOption = new NebulaConfiguration(null, () => new MetaWidgetOld.Button(()=>OpenFilterEditor(), TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "role.guesser.guessableFilter" });

        GenerateCommonEditor(RoleConfig);
    }

    public class NiceInstance : Crewmate.Crewmate.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyNiceRole;
        private int leftGuess = NumOfGuessOption;
        public NiceInstance(PlayerModInfo player, int[] arguments) : base(player)
        {
            if(arguments.Length>=1)leftGuess = arguments[0];
        }

        void IGameEntity.OnMeetingStart()
        {
            if (AmOwner) GuesserSystem.OnMeetingStart(leftGuess, () => leftGuess--);
        }

        void IGamePlayerEntity.OnDead()
        {
            if (AmOwner) GuesserSystem.OnDead();
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner) GuesserSystem.OnGameEnd(MyPlayer);
        }

        public override bool CanCallEmergencyMeeting => CanCallEmergencyMeetingOption;
    }

    public class EvilInstance : Impostor.Impostor.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyEvilRole;
        private int leftGuess = NumOfGuessOption;
        public EvilInstance(PlayerModInfo player, int[] arguments) : base(player)
        {
            if (arguments.Length >= 1) leftGuess = arguments[0];
        }

        void IGameEntity.OnMeetingStart()
        {
            if (AmOwner) GuesserSystem.OnMeetingStart(leftGuess, () => leftGuess--);
        }

        void IGamePlayerEntity.OnDead()
        {
            if (AmOwner) GuesserSystem.OnDead();
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner) GuesserSystem.OnGameEnd(MyPlayer);
        }

        public override bool CanCallEmergencyMeeting => CanCallEmergencyMeetingOption;
    }
}

public class GuesserModifier : ConfigurableStandardModifier, HasCitation
{
    static public GuesserModifier MyRole = new GuesserModifier();

    public override string LocalizedName => "guesser";
    public override string CodeName => "GSR";
    public override Color RoleColor => Guesser.MyNiceRole.RoleColor;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return Guesser.MyNiceRole; yield return Guesser.MyEvilRole; }
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments);

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagChaotic);

        Guesser.GenerateCommonEditor(RoleConfig);
    }

    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;
        public int LeftGuess = Guesser.NumOfGuessOption;

        public Instance(PlayerModInfo player,int[] arguments) : base(player){
            if (arguments.Length > 0)LeftGuess = arguments[0];
        }

        void IGameEntity.OnMeetingStart()
        {
            //追加役職Guesserは役職としてのGuesserがある場合効果を発揮しない
            if (MyPlayer.Role.Role is Guesser) return;

            if (AmOwner) GuesserSystem.OnMeetingStart(LeftGuess, () => LeftGuess--);
        }

        void IGamePlayerEntity.OnDead()
        {
            if (AmOwner) GuesserSystem.OnDead();
        }
        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (AmOwner || (NebulaGameManager.Instance?.CanSeeAllInfo ?? false)) text += " ⊕".Color(MyRole.RoleColor);
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner) GuesserSystem.OnGameEnd(MyPlayer);
        }

        public override string? IntroText => Language.Translate("role.guesser.blurb");

        public override bool CanCallEmergencyMeeting => Guesser.CanCallEmergencyMeetingOption;
    }
}