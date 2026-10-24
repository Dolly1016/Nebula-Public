using Nebula.Bridge;
using Nebula.Game.Statistics;
using Nebula.Modules.GUIWidget;
using Nebula.Roles.Abilities;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

internal class Havoc : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.havoc", new(181, 66, 0), TeamRevealType.OnlyMe);

    private Havoc() : base("havoc", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [KillCooldownOption, KillCooldownReductionOption, DevourCooldownOption, HavocCooldownOption, HavocDurationOption, RequiredKillingForVentOption, ClearHavocSEDistanceOption])
    {
        GameActionTypes.DevourCorpseAction = new("havoc.devour", this);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private IRelativeCooldownConfiguration KillCooldownOption = NebulaAPI.Configurations.KillConfiguration("options.role.havoc.killCooldown", CoolDownType.Immediate, (5f, 60f, 5f), 30f, (-40f, 20f, 5f), -5f, (0.125f, 2f, 0.125f), 0.875f);
    static private FloatConfiguration KillCooldownReductionOption = NebulaAPI.Configurations.Configuration("options.role.havoc.killCooldownReduction", (2.5f, 20f, 2.5f), 5f, FloatConfigurationDecorator.Second);
    static private IRelativeCooldownConfiguration DevourCooldownOption = NebulaAPI.Configurations.KillConfiguration("options.role.havoc.devourCooldown", CoolDownType.Immediate, (5f, 60f, 5f), 20f, (-40f, 20f, 5f), -10f, (0.125f, 2f, 0.125f), 0.75f);
    static private FloatConfiguration HavocCooldownOption = NebulaAPI.Configurations.Configuration("options.role.havoc.havocCooldown", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration HavocDurationOption = NebulaAPI.Configurations.Configuration("options.role.havoc.havocDuration", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration RequiredKillingForVentOption = NebulaAPI.Configurations.Configuration("options.role.havoc.requiredKillingForVent", (0, 10), 2);
    static private FloatConfiguration ClearHavocSEDistanceOption = NebulaAPI.Configurations.Configuration("options.role.havoc.clearHavocSEDistance", (2.5f, 30f, 2.5f), 7.5f, FloatConfigurationDecorator.Ratio);

    static public Havoc MyRole = new Havoc();
    static private GameStatsEntry StatsDevour = NebulaAPI.CreateStatsEntry("stats.havoc.devour", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsHavoc = NebulaAPI.CreateStatsEntry("stats.havoc.havoc", GameStatsCategory.Roles, MyRole);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasWinCondition => true;
    
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonSprite, "role.havoc.ability.devour");
        yield return new(devouredCorpseForHelpImage, "role.havoc.ability.devouredCorpse");
        yield return new(havocButtonSprite, "role.havoc.ability.havoc");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%REDUC%", KillCooldownReductionOption.GetValue().ToString());
        yield return new("%VENT%", RequiredKillingForVentOption.GetValue().ToString());
        yield return new("%DURATION%", HavocDurationOption.GetValue().ToString());
    }
    

    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.HavocDevourButton.png", 115f);
    static private Image havocButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.HavocButton.png", 115f);
    static private Image devouredCorpseImage = SpriteLoader.FromResource("Nebula.Resources.DevouredCorpse.png", 100f);
    static private ITextureLoader gaugeIconTexture = new ResourceTextureLoader("Nebula.Resources.HavocGuageIcon.png");
    static private Image gaugeIconImage = new SpriteLoader(gaugeIconTexture, 100f).SetPivot(new(0.35f, 0.145f));
    static private Image devouredCorpseForHelpImage = new SpriteLoader(gaugeIconTexture, 100f);

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        bool RuntimeRole.CanUseVent => true;
        bool RuntimeRole.PreventUsingVent =>  ate < RequiredKillingForVentOption;

        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            ate = arguments.Get(0, 0);
        }

        int[]? RuntimeAssignable.RoleArguments => [ate];

        int ate = 0;
        int myAte = 0;
        int myKill = 0;

        private float KillCooldown => Mathn.Max(0.1f, KillCooldownOption.Cooldown - ate * KillCooldownReductionOption);

        private void SetUpDeadBodyArrowAbility()
        {
            var arrowAbility = new DeadbodyArrowAbility();
            arrowAbility.Bind(this);
            arrowAbility.RegisterSelf();
            arrowAbility.Predicate = d => d.CanDrag == true;
            GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => arrowAbility.ShowArrow = !MyPlayer.IsDead, this);
        }

        private void SetUpDevourAbility(ModAbilityButton killButton, ModAbilityButton havocButton)
        {
            var devourTracker = ObjectTrackers.ForDeadBody(this, null, MyPlayer, (d) => !d.IsViperDeadBody && d.CanDrag);

            var eatButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                DevourCooldownOption.Cooldown, "devour", buttonSprite,
                _ => devourTracker.CurrentTarget != null);
            eatButton.OnClick = (button) => {
                var deadBody = devourTracker.CurrentTarget;
                if (deadBody != null)
                {
                    NebulaAsset.PlaySE(NebulaAudioClip.HavocDevour, false, 1f);
                    MyPlayer.Logic.SnapTo(deadBody.TruePosition);
                    RpcDevour.Invoke((MyPlayer.PlayerId, deadBody.Id));
                    StatsDevour.Progress();
                    myAte++;

                    if(killButton.CoolDownTimer is GameTimer timer)
                    {
                        var percentage = timer.Percentage;
                        timer.SetRange(0f, KillCooldown);
                        timer.Start(KillCooldown * percentage);
                    }

                    if (havocButton.IsInEffect) new StaticAchievementToken("havoc.common1");
                    new StaticAchievementToken("havoc.common2");
                }
                eatButton.StartCoolDown();
            };

            var Gauge = new DiscreteAbilityGauge(8, 27, gaugeIconImage, () => ate, true).Register(this);
        }

        static private DebugVariable<float> distance = new("havocDistance", 15f, t => float.TryParse(t, out var v) ? v : 0f, f => f.ToString());
        private void PlayHavocEffect(bool amHavoc, VVector2 position)
        {
            if (amHavoc)
            {
                var obj = GameObject.Instantiate(NebulaAsset.HavocWave, HudManager.Instance.transform);
                obj.transform.localPosition = new(0f, 0f, 1f);
                obj.transform.localScale = new(23f, 23f, 1f);
                obj.transform.eulerAngles = new(0f, 0f, System.Random.Shared.NextSingle() * 360f);
                var mat = obj.GetComponent<MeshRenderer>().material;
                mat.SetColor("_Tint", MyRole.Color.ToUnityColor());
                mat.SetVector("_L2ScrollAmount", new(0f, 5f, 0f, 0f));
                mat.SetVector("_ScrollAmount", new(0f, 1f, 0f, 0f));
                mat.SetFloat("_TotalDuration", HavocDurationOption);
                NebulaManager.Instance.StartDelayAction(HavocDurationOption + 5f, () => GameObject.Destroy(obj));

                NebulaAsset.PlaySE(NebulaAudioClip.HavocHowling, true, 1f);
            }
            else
            {
                float distance = GamePlayer.LocalPlayer?.Position.Distance(position) ?? 0f;
                float volume1 = Mathn.Clamp01(1.2f - distance / (ClearHavocSEDistanceOption / 1.2f));
                float volume2 = Mathn.Clamp01(Mathn.Clamp(2f - distance / 15f, 0.5f, 1f) - volume1 * 1.4f);
                NebulaAsset.PlaySE(NebulaAudioClip.HavocHowling, true, volume1);
                NebulaAsset.PlaySE(NebulaAudioClip.HavocHowlingFar, true, volume2);
            }

            GamePlayer.AllPlayers.Do(p => PlayerModInfo.RpcAttrModulator.LocalInvoke((p.PlayerId, new AttributeModulator(PlayerAttributes.Suppressed, HavocDurationOption, false, 100), true)));
        }

        private ModAbilityButton SetUpKillButton()
        {
            var killTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeLocalKillablePredicate(p), null, Impostor.Impostor.CanKillHidingPlayerOption);

            var killButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill,
                    KillCooldown, "kill", ModAbilityButton.LabelType.Impostor, null!,
                    (target, _) =>
                    {
                        var cancelable = GameOperatorManager.Instance?.Run(new PlayerTryVanillaKillLocalEvent(MyPlayer, target));
                        if (!(cancelable?.IsCanceled ?? false))
                        {
                            //キャンセルされなければキルを実行する
                            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);
                            myKill++;
                        }

                        //クールダウンをリセットする
                        if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    },
                    null,
                    _ => killTracker.CurrentTarget != null && !MyPlayer.IsDived,
                    _ => MyPlayer.AllowToShowKillButtonByAbilities
                    );
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());

            return killButton;
        }

        private ModAbilityButton SetUpHavocButton()
        {
            var havocButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, null,
                    HavocCooldownOption, HavocDurationOption, "havoc", havocButtonSprite, null, null);
            havocButton.SetLabelType(ModAbilityButton.LabelType.Impostor);
            havocButton.OnClick = b =>
            {
                if (!b.IsInEffect)
                {
                    StatsHavoc.Progress();
                    RpcHavoc.Invoke((MyPlayer.Position, MyPlayer));
                    MyPlayer.GainSpeedAttribute(0f, 1.2f, false, 100);
                    b.StartEffect();

                    int killed = 0;
                    GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev => {
                        if (ev.Murderer == MyPlayer && ev.Dead != MyPlayer) killed++;

                        if (killed == 2)
                        {
                            var lastDead = ev.Dead;
                            GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
                            {
                                if (ev.CheckWin(MyPlayer) && NebulaGameManager.Instance?.LastDead == lastDead) new StaticAchievementToken("havoc.challenge");
                            }, NebulaAPI.CurrentGame!);
                        }
                    }, new FunctionalLifespan(() => havocButton.IsInEffect));
                }
            };
            havocButton.OnEffectEnd = b => b.StartCoolDown();

            return havocButton;
        }

        void SetUpLockedVentButton()
        {
            var lifespan = new FunctionalLifespan(() => !this.IsDeadObject && ate < 2);
            lifespan.BindGameObject(HudManager.Instance.ImpostorVentButton.AddLockedOverlay().gameObject);
            GameOperatorManager.Instance?.RegisterOnReleased(HudManager.Instance.ImpostorVentButton.PlayFlashOnce, lifespan);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                SetUpDeadBodyArrowAbility();
                var killButton = SetUpKillButton();
                var havocButton = SetUpHavocButton();
                SetUpDevourAbility(killButton, havocButton);
                SetUpLockedVentButton();
            }
        }

        void ClaimJackalRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Havoc.MyTeam) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        bool RuntimeRole.HasImpostorVision => true;

        [Local, OnlyMyPlayer]
        private void OnDead(PlayerDieEvent ev)
        {
            if (myAte == 0 && myKill == 0) new StaticAchievementToken("havoc.another1");
        }
        static private readonly RemoteProcess<(int havoc, int deadBodyId)> RpcDevour = new("devour", (message, _) =>
        {
            if(ModSingleton<DeadBodyManager>.Instance.TryGetDeadBody(message.deadBodyId, out var deadBody) && deadBody.VanillaDeadBody.AsBoolFast(out var vanillaBody))
            {
                var renderer = vanillaBody.bodyRenderers[0];
                renderer.GetComponent<Animator>().enabled = false;
                renderer.sprite = devouredCorpseImage.GetSprite();
                deadBody.CanDrag = false;
                ModSingleton<HavocPatcher>.Instance.RegisterAsDevoured(deadBody.Player);
            }
            if(GamePlayer.GetPlayer((byte)message.havoc)?.Role is Instance havoc)
            {
                havoc.ate = Mathn.Min(8, havoc.ate + 1);
            }
        });

        static private readonly RemoteProcess<(VVector2 position, GamePlayer player)> RpcHavoc = new("Havoc", (message, _) =>
        {
            if(message.player.Role is Instance havoc)
            {
                havoc.PlayHavocEffect(message.player.AmOwner, message.position);
            }
        });
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
file class HavocPatcher : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static HavocPatcher() => DIManager.Instance.RegisterModule(() => new HavocPatcher().RegisterPermanently());

    private HavocPatcher()
    {
        ModSingleton<HavocPatcher>.Instance = this;
    }

    private HashSet<byte> devouredPlayers = [];
    public void RegisterAsDevoured(GamePlayer player) => devouredPlayers.Add(player.PlayerId);

    static Image devouredMarkImage = SpriteLoader.FromResource("Nebula.Resources.HavocMeetingIcon.png", 100f);
    void OnMeetingSetUp(MeetingSetUpVoteAreaEvent ev)
    {
        foreach (var pva in ev.VoteAreas)
        {
            if (devouredPlayers.Contains(pva.PlayerId.Value))
            {
                var xMark = pva.XMark;
                xMark.transform.localPosition = new(-0.908f, 0f, -1.9f);
                xMark.transform.localScale = new(0.9f, 0.9f, 1f);
                xMark.sprite = devouredMarkImage.GetSprite();
            }
        }
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
file class HavocCriteria : AbstractModule<IGameModeStandard>, IGameOperator
{
    static HavocCriteria() => DIManager.Instance.RegisterModule(() => new HavocCriteria().RegisterPermanently());

    [OnlyHost]
    void OnUpdate(GameUpdateEvent ev)
    {
        int totalAlive = 0;
        bool leftOtherKillers = GameOperatorManager.Instance?.Run(new KillerTeamCallback(NebulaTeams.HavocTeam)).RemainingOtherTeam ?? false;
        if (leftOtherKillers) return;

        Havoc.Instance? lastFoundHavoc = null;
        int allAliveHavocs = 0;
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            if (p.IsDead) continue;
            if (p.Role is Havoc.Instance hRole)
            {
                lastFoundHavoc = hRole;
                allAliveHavocs++;
            }
            totalAlive++;
        }

        if(allAliveHavocs == 1 && totalAlive <= 2) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.HavocWin, GameEndReason.Situation);
    }
};
