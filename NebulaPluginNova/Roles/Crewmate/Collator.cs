using Nebula.Behaviour;
using Nebula.Modules.MetaWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Collator : ConfigurableStandardRole, HasCitation
{
    static public Collator MyRole = null!;// new Collator();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "collator";
    public override Color RoleColor => new Color(37f / 255f, 159f / 255f, 148f / 255f);
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments);

    NebulaConfiguration SampleCoolDownOption = null!;
    NebulaConfiguration SelectiveCollatingOption = null!;
    NebulaConfiguration MaxTrialsPerMeetingOption = null!;
    NebulaConfiguration NumOfTubesOption = null!;
    NebulaConfiguration CarringOverSamplesOption = null!;
    NebulaConfiguration StrictClassificationOfNeutralRolesOption = null!;
    NebulaConfiguration MadmateIsClassifiedAsOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();


        SampleCoolDownOption = new(RoleConfig, "sampleCoolDown", null, 0f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        SelectiveCollatingOption = new(RoleConfig, "selectiveCollating", null, false, false);
        MaxTrialsPerMeetingOption = new(RoleConfig, "maxTrialsPerMeeting", null, 1, 5, 1, 1) { Predicate = () => SelectiveCollatingOption };
        NumOfTubesOption = new(RoleConfig, "numOfTubes", null, 2, 14, 3, 2) { Predicate = ()=> SelectiveCollatingOption };
        CarringOverSamplesOption = new(RoleConfig, "carringOverSamples", null, false, true) { Predicate = () => SelectiveCollatingOption };
        StrictClassificationOfNeutralRolesOption = new(RoleConfig, "strictClassificationOfNeutralRoles", null, false, false);
        MadmateIsClassifiedAsOption = new(RoleConfig, "madmateIsClassifiedAs", null, [
            "role.options.collator.madmateIsClassifiedAs.impostor",
            "role.options.collator.madmateIsClassifiedAs.crewmate"], 0,0);

    }

    public class Instance : Crewmate.Instance, IGameEntity
    {
        static private SpriteLoader meetingSprite = SpriteLoader.FromResource("Nebula.Resources.CollatorIcon.png", 115f);

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CollatorSampleButton.png", 100f);
        static private IDividedSpriteLoader tubeSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CollatorTube.png", 125f, 2, 1);
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player, int[] arguments) : base(player){}

        private List<(PlayerModInfo player, RoleTeam team)> sampledPlayers = new();
        private (SpriteRenderer tube, SpriteRenderer sample)[] allSamples = null!;

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

        void RegisterResult((PlayerModInfo player, RoleTeam team) player1, (PlayerModInfo player, RoleTeam team) player2)
        {
            bool matched = player1.team == player2.team;
            NebulaGameManager.Instance?.MeetingOverlay.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new TranslateTextComponent("role.collator.ui.title")),
                new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), 
                new RawTextComponent(
                    Language.Translate("role.collator.ui.target") + ":<br>"
                    + "  " + player1.player.ColoredDefaultName + "<br>"
                    + "  " + player2.player.ColoredDefaultName + "<br>"
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
                            }
                            else
                            {
                                p.SetSelect(true);
                            }
                        }
                    },
                    p => sampledPlayers.Any(s => s.player == p.MyPlayer) && leftTest > 0 && sampledPlayers.Count >= 2
                    ));
                }
                else
                {
                    //2つしかサンプルを取れない場合

                    RegisterResult(sampledPlayers[0], sampledPlayers[1]);
                    sampledPlayers.Clear();
                    UpdateSamples();
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
        public override void OnActivated()
        {
            if (AmOwner)
            {
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

                sampleButton.SetSprite(buttonSprite.GetSprite());
                sampleButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove && sampledPlayers.Count < allSamples.Length;
                sampleButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                sampleButton.OnClick = (button) => {
                    var p = sampleTracker.CurrentTarget.GetModInfo()!;
                    sampledPlayers.Add((p,CheckTeam(p)));
                    NebulaManager.Instance.StartCoroutine(CoShakeTube(sampledPlayers.Count - 1).WrapToIl2Cpp());
                    UpdateSamples();
                    button.StartCoolDown();
                };
                sampleButton.OnStartTaskPhase = (button) => { if (MyRole.CarringOverSamplesOption) sampledPlayers.Clear(); UpdateSamples(); };
                sampleButton.CoolDownTimer = Bind(new Timer(MyRole.SampleCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("collatorSample");
            }
        }

    }
}
