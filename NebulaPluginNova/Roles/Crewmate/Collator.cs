using Hazel;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Crewmate;

public class Collator : ConfigurableStandardRole, HasCitation
{
    static public Collator MyRole = null;//new Collator();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "collator";
    public override Color RoleColor => new Color(37f / 255f, 159f / 255f, 148f / 255f);
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments);

    NebulaConfiguration SampleCoolDownOption = null!;
    NebulaConfiguration SelectiveCollatingOption = null!;
    NebulaConfiguration MaxTrialsOption = null!;
    NebulaConfiguration MaxTrialsPerMeetingOption = null!;
    NebulaConfiguration NumOfTubesOption = null!;
    NebulaConfiguration CarringOverSamplesOption = null!;
    NebulaConfiguration StrictClassificationOfNeutralRolesOption = null!;
    NebulaConfiguration MadmateIsClassifiedAsOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagSNR);

        SampleCoolDownOption = new(RoleConfig, "sampleCoolDown", null, 0f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        SelectiveCollatingOption = new(RoleConfig, "selectiveCollating", null, false, false);
        MaxTrialsOption = new(RoleConfig, "maxTrials", null, 1, 15, 8, 8);
        MaxTrialsPerMeetingOption = new(RoleConfig, "maxTrialsPerMeeting", null, 1, 5, 1, 1) { Predicate = () => SelectiveCollatingOption };
        NumOfTubesOption = new(RoleConfig, "numOfTubes", null, 2, 14, 3, 2) { Predicate = ()=> SelectiveCollatingOption };
        CarringOverSamplesOption = new(RoleConfig, "carringOverSamples", null, false, true) { Predicate = () => SelectiveCollatingOption };
        StrictClassificationOfNeutralRolesOption = new(RoleConfig, "strictClassificationForNeutralRoles", null, false, false);
        MadmateIsClassifiedAsOption = new(RoleConfig, "madmateIsClassifiedAs", null, [
            "options.role.collator.madmateIsClassifiedAs.impostor",
            "options.role.collator.madmateIsClassifiedAs.crewmate"], 0, 0);
    }

    public class Instance : Crewmate.Instance, IGamePlayerEntity
    {
        static private SpriteLoader meetingSprite = SpriteLoader.FromResource("Nebula.Resources.CollatorIcon.png", 115f);

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CollatorSampleButton.png", 100f);
        static private IDividedSpriteLoader tubeSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CollatorTube.png", 125f, 2, 1);
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player, int[] arguments) : base(player){}

        private List<(PlayerModInfo player, RoleTeam team)> sampledPlayers = new();
        private (SpriteRenderer tube, SpriteRenderer sample)[] allSamples = null!;

        private int trials = MyRole.MaxTrialsOption.GetMappedInt();
        void UpdateSamples()
        {
            for(int i = 0; i < allSamples.Length; i++)
            {
                PlayerModInfo? player = sampledPlayers.Count > i ? sampledPlayers[i].player : null;
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

            NebulaGameManager.Instance?.MeetingOverlay.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
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

        void IGameEntity.OnMeetingStart()
        {
            if (AmOwner && sampledPlayers.Count >= 2)
            {
                if (MyRole.SelectiveCollatingOption)
                {
                    //選択式

                    int leftTest = MyRole.MaxTrialsPerMeetingOption;
                    NebulaGameManager.Instance?.MeetingPlayerButtonManager.RegisterMeetingAction(new(meetingSprite,
                    p =>
                    {
                        if(p.IsSelected)
                            p.SetSelect(false);
                        else
                        {
                            var selected = NebulaGameManager.Instance?.MeetingPlayerButtonManager.AllStates.FirstOrDefault(p => p.IsSelected);

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
                    p => sampledPlayers.Any(s => s.player == p.MyPlayer) && leftTest > 0 && trials > 0 && sampledPlayers.Count >= 2
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

        private RoleTeam CheckTeam(PlayerModInfo p)
        {
            switch (p.Role.Role.Category)
            {
                case RoleCategory.CrewmateRole:
                    if (p.Role.Role == Madmate.MyRole)
                        return MyRole.MadmateIsClassifiedAsOption.CurrentValue switch { 0 => NebulaTeams.ImpostorTeam, 1 => NebulaTeams.CrewmateTeam, _ => NebulaTeams.CrewmateTeam };
                    else
                        return NebulaTeams.CrewmateTeam;
                case RoleCategory.ImpostorRole:
                    return NebulaTeams.ImpostorTeam;
                case RoleCategory.NeutralRole:
                    return MyRole.StrictClassificationOfNeutralRolesOption ? p.Role.Role.Team : NebulaTeams.JackalTeam;
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

        AchievementToken<BitMask<GamePlayer>>? acTokenChallenge = null;
        AchievementToken<(GamePlayer? player, float time, bool clear)>? acTokenAnother1 = null;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("collator.challenge", BitMasks.AsPlayer(), (value, _) => NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnds.CrewmateGameEnd && (NebulaGameManager.Instance?.AllPlayerInfo().Where(p => (p as GamePlayer).IsImpostor).All(p => p.MyState == PlayerStates.Exiled && value.Test(p)) ?? false));
                acTokenAnother1 = new("collator.another1", (null, 0f, false), (value, _) => value.clear);

                //サンプル一覧の表示
                var IconsHolder = HudContent.InstantiateContent("CollatorIcons", true, true, false);
                this.Bind(IconsHolder.gameObject);

                allSamples = new (SpriteRenderer tube, SpriteRenderer sample)[MyRole.SelectiveCollatingOption ? MyRole.NumOfTubesOption : 2];
                for(int i = 0;i<allSamples.Length;i++)
                {
                    var tube = UnityHelper.CreateObject<SpriteRenderer>("SampleTube", IconsHolder.transform, Vector3.zero, LayerExpansion.GetUILayer());
                    tube.sprite = tubeSprite.GetSprite(0);

                    var sample = UnityHelper.CreateObject<SpriteRenderer>("SampleColored", tube.transform, new(0, 0, 0.1f));
                    sample.sprite = tubeSprite.GetSprite(1);
                    //sample.color = Palette.PlayerColors[target.DefaultOutfit.ColorId];

                    tube.transform.localPosition = new((float)i * 0.4f - 0.3f, 0f, 0f);

                    allSamples[i] = (tube, sample);
                }

                UpdateSamples();

                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => p.PlayerId != MyPlayer.PlayerId && !p.Data.IsDead && !sampledPlayers.Any(s => s.player.PlayerId == p.PlayerId), false));
                var sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);

                AchievementToken<int> achCommon1Token = new("collator.common1", 0, (val, _) => val >= 5);
                AchievementToken<int> achCommon2Token = new("collator.common2", 0, (val, _) => val);

                sampleButton.SetSprite(buttonSprite.GetSprite());
                sampleButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove && sampledPlayers.Count < allSamples.Length;
                sampleButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && trials > 0;
                sampleButton.OnClick = (button) => {
                    var p = sampleTracker.CurrentTarget.GetModInfo()!;
                    sampledPlayers.Add((p,CheckTeam(p)));
                    NebulaManager.Instance.StartCoroutine(CoShakeTube(sampledPlayers.Count - 1).WrapToIl2Cpp());
                    UpdateSamples();
                    button.StartCoolDown();

                    achCommon1Token.Value++;
                    achCommon2Token.Value++;

                    acTokenAnother1.Value.player = p;
                    acTokenAnother1.Value.time = NebulaGameManager.Instance!.CurrentTime;
                };
                sampleButton.OnStartTaskPhase = (button) => { if (MyRole.CarringOverSamplesOption) sampledPlayers.Clear(); UpdateSamples(); };
                sampleButton.CoolDownTimer = Bind(new Timer(MyRole.SampleCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("collatorSample");
            }
        }

        void IGamePlayerEntity.OnMurdered(Virial.Game.Player murder)
        {
            if(AmOwner && acTokenAnother1 != null)
            {
                if(acTokenAnother1.Value.player == murder && NebulaGameManager.Instance!.CurrentTime < acTokenAnother1.Value.time + 5f) {
                    acTokenAnother1.Value.clear = true;
                }
            }
        }

    }
}
