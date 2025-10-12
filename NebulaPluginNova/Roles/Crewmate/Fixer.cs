using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
internal class Fixer : DefinedSingleAbilityRoleTemplate<Fixer.Ability>, DefinedRole
{
    private Fixer() : base("fixer", new(87, 124, 109), RoleCategory.CrewmateRole, Crewmate.MyTeam, [NumOfJammingOption, CanJamPlayerContinuouslyOption, JammingSealsVoteRightOption, MaxLeftVotingTimeForJamming])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }


    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static private readonly IntegerConfiguration NumOfJammingOption = NebulaAPI.Configurations.Configuration("options.role.fixer.numOfJamming", (1, 10), 2);
    static private readonly BoolConfiguration CanJamPlayerContinuouslyOption = NebulaAPI.Configurations.Configuration("options.role.fixer.canJamPlayerContinuously", false);
    static private readonly BoolConfiguration JammingSealsVoteRightOption = NebulaAPI.Configurations.Configuration("options.role.fixer.jammingSealsVoteRight", true);
    static private readonly FloatConfiguration MaxLeftVotingTimeForJamming = NebulaAPI.Configurations.Configuration("options.role.fixer.maxLeftVotingTimeForJamming", (0f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);

    static public readonly Fixer MyRole = new();
    static private readonly GameStatsEntry StatsAbility = NebulaAPI.CreateStatsEntry("stats.fixer.jamming", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        int leftJamming = NumOfJammingOption;
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                string prefix = Language.Translate("roles.fixer.jamming");
                Helpers.TextHudContent("FixerText", this, (tmPro) => tmPro.text = prefix + ": " + leftJamming, true);
            }
        }

        GamePlayer? lastChosen = null;

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(MeetingPlayerButtonManager.Icons.AsLoader(7),
               p =>
               {
                   if (MyPlayer.IsSameSideOf(p.MyPlayer))
                   {
                       if (MeetingHud.Instance.playerStates.Count(pva => pva.VotedFor == p.MyPlayer.PlayerId) >= 5)
                       {
                           GameOperatorManager.Instance?.SubscribeSingleListener<MeetingEndEvent>((ev) => { if (ev.Exiled.Any(p => !MyPlayer.IsSameSideOf(p))) new StaticAchievementToken("fixer.common2"); });
                       }
                   }
                   else
                   {
                       if (GamePlayer.AllPlayers.Count(p => !p.IsDead) <= 2 * GamePlayer.AllPlayers.Count(p => !p.IsDead && (p.IsImpostor || p.IsMadmate)))
                       {
                           GameOperatorManager.Instance?.SubscribeSingleListener<MeetingEndEvent>((ev) => { if (ev.Exiled.Any(exiled => p.MyPlayer.IsSameSideOf(exiled))) new StaticAchievementToken("fixer.challenge"); });
                       }
                   }

                   leftJamming--;
                   lastChosen = p.MyPlayer;
                   RpcInsulate.Invoke(p.MyPlayer);
                   StatsAbility.Progress();

               },
               p => leftJamming > 0 && MeetingHudExtension.LeftTime > MaxLeftVotingTimeForJamming && !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && !MyPlayer.IsDead && (CanJamPlayerContinuouslyOption || lastChosen != p.MyPlayer), true
               ));


        }
    }

    static private Image WifiBlockedImage = SpriteLoader.FromResource("Nebula.Resources.MeetingNoSignal.png", 100f);
    static private Image BlockedImage = SpriteLoader.FromResource("Nebula.Resources.MeetingNoSignalPlayer.png", 100f);
    static private RemoteProcess<GamePlayer> RpcInsulate = new("Insulate",
        (player, _) => {
            MeetingHudExtension.AddSealedMask(1 << player.PlayerId); //能力使用可能な対象、および投票対象から除外
            if(JammingSealsVoteRightOption)  MeetingHudExtension.RemoveCanVoteMask(1 << player.PlayerId); //投票権を没収
            if (player.AmOwner) MeetingHudExtension.CanUseAbility = false; //能力の使用を禁止
            MeetingHud.Instance.ResetPlayerState();
            MeetingHudExtension.ExpandDiscussionTime();
            

            if (player.AmOwner)
            {
                //自身の能力が封じられた場合
                var wifiMark = MeetingHud.Instance.meetingContents.GetChild(0).GetChild(5);
                var wifiBlockedMark = UnityHelper.CreateObject<SpriteRenderer>("Wi-fi Blocked", wifiMark, new(0.2f, 0.2f, -0.5f));
                wifiBlockedMark.sprite = WifiBlockedImage.GetSprite();

                new StaticAchievementToken("fixer.secret1");
            }

            {
                var pva = MeetingHud.Instance.GetPlayer(player.PlayerId).transform;
                var wifiBlockedMark = UnityHelper.CreateObject<SpriteRenderer>("Wi-fi Blocked", pva, MeetingHudExtension.VoteAreaPlayerIconPos);
                wifiBlockedMark.sprite = BlockedImage.GetSprite();
                wifiBlockedMark.transform.localScale = new(0.85f, 0.85f, 1f);
                IEnumerator CoUpdate()
                {
                    while (wifiBlockedMark)
                    {
                        float p = 0f;
                        while (p < Mathn.PI) {
                            if (!wifiBlockedMark) break;
                            var sin = Mathn.Sin(p);
                            wifiBlockedMark.color = new(1f, 1f, 1f, 1f - sin * 0.4f);
                            p += Time.deltaTime * 3.3f;
                            yield return null;
                        }
                        yield return Effects.Wait(0.2f);
                    }
                }
                MeetingHud.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());

                var noise = UnityHelper.CreateMeshRenderer("NoiseRenderer", pva, new(0f,0f,-0.1f), null, null, new(NebulaAsset.MeshDistShader));
                noise.filter.CreateRectMesh(new(2.85f, 0.7f));
                var noiseMat = noise.renderer.material;
                noiseMat.SetFloat("_GlitchYSize", 0.045f);
                noiseMat.SetFloat("_GlitchBlockSize", 0.007f);
                IEnumerator CoNoiseUpdate()
                {
                    void SetInactive() {
                        if (noise.renderer)
                        {
                            noiseMat.SetFloat("_GlitchYAmount", 0f);
                            noiseMat.SetFloat("_GlitchBlockAmount", 0.001f);
                        }
                    }
                    void SetActive()
                    {
                        if (noise.renderer)
                        {
                            noiseMat.SetFloat("_GlitchYAmount", 0.01f);
                            noiseMat.SetFloat("_GlitchBlockAmount", 0.005f);
                        }
                    }
                    while (noise.renderer)
                    {
                        SetInactive();
                        yield return Effects.Wait(2f + System.Random.Shared.NextSingle() * 1.5f);
                        SetActive();
                        yield return Effects.Wait(0.1f + System.Random.Shared.NextSingle() * 0.3f);
                        SetInactive();
                        yield return Effects.Wait(0.1f + System.Random.Shared.NextSingle() * 0.2f);
                        SetActive();
                        yield return Effects.Wait(0.5f + System.Random.Shared.NextSingle() * 0.8f);
                    }
                }

                NebulaAsset.PlaySE(NebulaAudioClip.Fixer);
                MeetingHud.Instance.StartCoroutine(CoNoiseUpdate().WrapToIl2Cpp());

            }
            
        });
}

