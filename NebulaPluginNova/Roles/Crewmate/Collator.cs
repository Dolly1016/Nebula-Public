using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;
using Virial.Text;

namespace Nebula.Roles.Crewmate;

public class Collator : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Collator():base("collator",new(37, 159, 148), RoleCategory.CrewmateRole, Crewmate.MyTeam, [SampleCoolDownOption, SelectiveCollatingOption, MaxTrialsOption, MaxTrialsPerMeetingOption, NumOfTubesOption, CarringOverSamplesOption, CanTakeDuplicateSampleOption, StrictClassificationOfNeutralRolesOption, MadmateIsClassifiedAsOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Collator.png");
    }
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.collator.sampleCoolDown", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration SelectiveCollatingOption = NebulaAPI.Configurations.Configuration("options.role.collator.selectiveCollating", false);
    static private IntegerConfiguration MaxTrialsOption = NebulaAPI.Configurations.Configuration("options.role.collator.maxTrials", (1, 15), 8);
    static private IntegerConfiguration MaxTrialsPerMeetingOption = NebulaAPI.Configurations.Configuration("options.role.collator.maxTrialsPerMeeting", (1, 5), 1, () => SelectiveCollatingOption);
    static private IntegerConfiguration NumOfTubesOption = NebulaAPI.Configurations.Configuration("options.role.collator.numOfTubes", (3, 14), 5, () => SelectiveCollatingOption);
    static private BoolConfiguration CarringOverSamplesOption = NebulaAPI.Configurations.Configuration("options.role.collator.carringOverSamples", false, () => SelectiveCollatingOption);
    static private BoolConfiguration CanTakeDuplicateSampleOption = NebulaAPI.Configurations.Configuration("options.role.collator.canTakeDuplicateSample", false, () => SelectiveCollatingOption);
    static private BoolConfiguration StrictClassificationOfNeutralRolesOption = NebulaAPI.Configurations.Configuration("options.role.collator.strictClassificationForNeutralRoles", false);
    static private ValueConfiguration<int> MadmateIsClassifiedAsOption = NebulaAPI.Configurations.Configuration("options.role.collator.madmateIsClassifiedAs", ["options.role.collator.madmateIsClassifiedAs.impostor", "options.role.collator.madmateIsClassifiedAs.crewmate"], 0);
    
    static public Collator MyRole = new Collator();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static private SpriteLoader meetingSprite = SpriteLoader.FromResource("Nebula.Resources.CollatorIcon.png", 115f);

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CollatorSampleButton.png", 100f);
        static private IDividedSpriteLoader tubeSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CollatorTube.png", 125f, 2, 1);

        public Instance(GamePlayer player, int[] arguments) : base(player){}

        private List<(GamePlayer player, RoleTeam team)> sampledPlayers = new();
        private (SpriteRenderer tube, SpriteRenderer sample)[] allSamples = null!;
        private int ActualSampledPlayers => sampledPlayers.DistinctBy(p => p.player.PlayerId).Count();
        private int trials = MaxTrialsOption;
        void UpdateSamples()
        {
            for(int i = 0; i < allSamples.Length; i++)
            {
                GamePlayer? player = sampledPlayers.Count > i ? sampledPlayers[i].player : null;
                if (player != null)
                {
                    allSamples[i].sample.gameObject.SetActive(true);
                    allSamples[i].sample.color = Palette.PlayerColors[player.PlayerId];
                }
                else allSamples[i].sample.gameObject.SetActive(false);
            }
        }

        
        void RegisterResult((GamePlayer player, RoleTeam team) player1, (GamePlayer player, RoleTeam team) player2)
        {
            bool matched = player1.team == player2.team;

            if (player1.player.IsImpostor && player2.player.IsImpostor) new StaticAchievementToken("collator.common4");
            if (!matched) new StaticAchievementToken("collator.common3");
            if (acTokenChallenge != null) acTokenChallenge.Value.Add(player1.player).Add(player2.player);

            NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new TranslateTextComponent("role.collator.ui.title")),
                new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), 
                new RawTextComponent(
                    Language.Translate("role.collator.ui.target") + ":<br>"
                    + "  " + player1.player.Unbox().ColoredDefaultName + "<br>"
                    + "  " + player2.player.Unbox().ColoredDefaultName + "<br>"
                    + "<br>"
                    + Language.Translate("role.collator.ui.result") + ": " + (matched ? Language.Translate("role.collator.ui.matched").Color(Color.green) : Language.Translate("role.collator.ui.unmatched").Color(Color.red)).Bold()                     
                )))
                , MeetingOverlayHolder.IconsSprite[2], MyRole.RoleColor);
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (ActualSampledPlayers >= 2)
            {
                if (SelectiveCollatingOption)
                {
                    //選択式

                    int leftTest = MaxTrialsPerMeetingOption;

                    var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
                    buttonManager?.RegisterMeetingAction(new(meetingSprite,
                    p =>
                    {
                        if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                        if (p.IsSelected)
                            p.SetSelect(false);
                        else
                        {
                            var selected = buttonManager.AllStates.FirstOrDefault(p => p.IsSelected);

                            if (selected != null)
                            {
                                selected.SetSelect(false);

                                var selectedSample = sampledPlayers.FirstOrDefault(s => s.player.PlayerId == selected.MyPlayer.PlayerId);
                                var mySample = sampledPlayers.FirstOrDefault(s => s.player.PlayerId == p.MyPlayer.PlayerId);
                                RegisterResult(selectedSample, mySample);
                                sampledPlayers.Remove(selectedSample);
                                sampledPlayers.Remove(mySample);
                                UpdateSamples();
                                leftTest--;
                                trials--;
                            }
                            else
                            {
                                p.SetSelect(true);
                            }
                        }
                    },
                    p => sampledPlayers.Any(s => s.player == p.MyPlayer) && leftTest > 0 && trials > 0 && ActualSampledPlayers >= 2 && !MyPlayer.IsDead
                    ));
                }
                else
                {
                    //2つしかサンプルを取れない場合

                    RegisterResult(sampledPlayers[0], sampledPlayers[1]);
                    sampledPlayers.Clear();
                    UpdateSamples();
                    trials--;
                }
            }
        }

        private RoleTeam CheckTeam(GamePlayer p)
        {
            switch (p.Role.Role.Category)
            {
                case RoleCategory.CrewmateRole:
                    if (p.Role.Role == Madmate.MyRole)
                        return MadmateIsClassifiedAsOption.GetValue() switch { 0 => NebulaTeams.ImpostorTeam, 1 => NebulaTeams.CrewmateTeam, _ => NebulaTeams.CrewmateTeam };
                    else
                        return NebulaTeams.CrewmateTeam;
                case RoleCategory.ImpostorRole:
                    return NebulaTeams.ImpostorTeam;
                case RoleCategory.NeutralRole:
                    return StrictClassificationOfNeutralRolesOption ? p.Role.Role.Team : NebulaTeams.JackalTeam;
            }

            return NebulaTeams.CrewmateTeam;
        }

        private IEnumerator CoShakeTube(int index)
        {
            var tube = allSamples[index].tube;
            float p = 0f;
            while (p < 1f) {
                p += Time.deltaTime * 1.15f;
                tube.transform.localEulerAngles = new(0f, 0f, 24f * Mathf.Sin(p * 29.2f) * (1f - p));
                tube.transform.localScale = Vector3.one * (1f + (1f - (p * p)) * 0.4f);
                yield return null;
            }
            tube.transform.localEulerAngles = new(0f, 0f, 0f);
            tube.transform.localScale = Vector3.one;
        }

        AchievementToken<EditableBitMask<GamePlayer>>? acTokenChallenge = null;
        AchievementToken<(GamePlayer? player, float time, bool clear)>? acTokenAnother1 = null;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("collator.challenge", BitMasks.AsPlayer(), (value, _) => NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnds.CrewmateGameEnd && (NebulaGameManager.Instance?.AllPlayerInfo().Where(p => (p as GamePlayer).IsImpostor).All(p => p.PlayerState == PlayerStates.Exiled && value.Test(p)) ?? false));
                acTokenAnother1 = new("collator.another1", (null, 0f, false), (value, _) => value.clear);

                //サンプル一覧の表示
                var IconsHolder = HudContent.InstantiateContent("CollatorIcons", true, true, false, true);
                this.Bind(IconsHolder.gameObject);
                var ajust = UnityHelper.CreateObject<ScriptBehaviour>("Ajust", IconsHolder.transform, Vector3.zero);
                ajust.UpdateHandler += () =>
                {
                    if (MeetingHud.Instance)
                    {
                        ajust.transform.localScale = new(0.65f, 0.65f, 1f);
                        ajust.transform.localPosition = new(-0.45f, -0.37f, 0f);
                    }
                    else
                    {
                        ajust.transform.localScale = Vector3.one;
                        ajust.transform.localPosition = Vector3.zero;
                    }
                };

                allSamples = new (SpriteRenderer tube, SpriteRenderer sample)[SelectiveCollatingOption ? NumOfTubesOption : 2];
                for(int i = 0;i<allSamples.Length;i++)
                {
                    var tube = UnityHelper.CreateObject<SpriteRenderer>("SampleTube", ajust.transform, Vector3.zero, LayerExpansion.GetUILayer());
                    tube.sprite = tubeSprite.GetSprite(0);

                    var sample = UnityHelper.CreateObject<SpriteRenderer>("SampleColored", tube.transform, new(0, 0, 0.1f));
                    sample.sprite = tubeSprite.GetSprite(1);
                    //sample.color = Palette.PlayerColors[target.DefaultOutfit.ColorId];

                    tube.transform.localPosition = new((float)i * 0.4f - 0.3f, 0f, 0f);

                    allSamples[i] = (tube, sample);
                }

                UpdateSamples();

                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.StandardPredicate(p) && ((CanTakeDuplicateSampleOption && (sampledPlayers.Count + 1 < allSamples.Length || ActualSampledPlayers >= 2)) || !sampledPlayers.Any(s => s.player.PlayerId == p.PlayerId))));
                var sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);

                AchievementToken<int> achCommon1Token = new("collator.common1", 0, (val, _) => val >= 5);
                AchievementToken<int> achCommon2Token = new("collator.common2", 0, (val, _) => val);

                sampleButton.SetSprite(buttonSprite.GetSprite());
                sampleButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.CanMove && sampledPlayers.Count < allSamples.Length;
                sampleButton.Visibility = (button) => !MyPlayer.IsDead && trials > 0;
                sampleButton.OnClick = (button) => {
                    var p = sampleTracker.CurrentTarget;
                    sampledPlayers.Add((p,CheckTeam(p)));
                    NebulaManager.Instance.StartCoroutine(CoShakeTube(sampledPlayers.Count - 1).WrapToIl2Cpp());
                    UpdateSamples();
                    button.StartCoolDown();

                    achCommon1Token.Value++;
                    achCommon2Token.Value++;

                    acTokenAnother1.Value.player = p;
                    acTokenAnother1.Value.time = NebulaGameManager.Instance!.CurrentTime;
                };
                sampleButton.OnStartTaskPhase = (button) => { if (!SelectiveCollatingOption || !CarringOverSamplesOption) sampledPlayers.Clear(); UpdateSamples(); };
                sampleButton.CoolDownTimer = Bind(new Timer(SampleCoolDownOption).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("collatorSample");
            }
        }

        [Local]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if(acTokenAnother1 != null)
            {
                if(acTokenAnother1.Value.player == ev.Murderer && NebulaGameManager.Instance!.CurrentTime < acTokenAnother1.Value.time + 5f) {
                    acTokenAnother1.Value.clear = true;
                }
            }
        }

    }
}
