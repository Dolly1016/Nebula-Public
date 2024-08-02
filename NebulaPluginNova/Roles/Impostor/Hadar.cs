using Nebula.Behaviour;
using Nebula.Map;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Hadar : DefinedRoleTemplate, DefinedRole
{
    private Hadar() : base("hadar", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [DiveCoolDownOption, AccelRateUndergroundOption, GushFromVentsOption, VentDetectionRangeOption, LeftDivingEvidenceOption]) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration DiveCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.hadar.diveCooldown", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration GushFromVentsOption = NebulaAPI.Configurations.Configuration("options.role.hadar.gushFromVents", false);
    static private FloatConfiguration VentDetectionRangeOption = NebulaAPI.Configurations.Configuration("options.role.hadar.ventDetectionRange", (2f, 20f, 1f), 5f, FloatConfigurationDecorator.Ratio, ()=>GushFromVentsOption);
    static private BoolConfiguration LeftDivingEvidenceOption = NebulaAPI.Configurations.Configuration("options.role.hadar.leftDivingEvidence", false);
    static private FloatConfiguration AccelRateUndergroundOption = NebulaAPI.Configurations.Configuration("options.role.hadar.accelRateUnderground", (1f, 2f, 0.125f), 1.125f, FloatConfigurationDecorator.Ratio);

    static public Hadar MyRole = new Hadar();

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class HadarEvidence : NebulaSyncStandardObject, IGameOperator
    {
        public static string MyTag = "HadarEvidence";
        private static IDividedSpriteLoader evidenceSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.HadarEvidence.png", 100f, 3);
        private int stage = 0;
        public HadarEvidence(Vector2 pos) : base(pos, ZOption.Back, true, evidenceSprite.GetSprite(0))
        {
        }

        static HadarEvidence()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new HadarEvidence(new Vector2(args[0], args[1])));
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            stage++;
            if (stage >= 3) NebulaSyncObject.LocalDestroy(this.ObjectId);
            else MyRenderer.sprite = evidenceSprite.GetSprite(stage);
        }
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? diveButton = null;
        private ModAbilityButton? gushButton = null;

        static private Image diveButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.HadarHideButton.png", 115f);
        static private Image gushButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.HadarAppearButton.png", 115f);

        AchievementToken<(float lastGush, bool cleared)> acToken1 = new("hadar.common1", (-100f, false), (a, _) => a.cleared);
        AchievementToken<(float lastKill, bool cleared)> acToken2 = new("hadar.common2", (-100f, false), (a, _) => a.cleared);
        AchievementToken<(bool cleared, bool triggered)> acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("hadar.another1");
        public Instance(GamePlayer player) : base(player)
        {
        }

        public bool IsDiving => MyPlayer.IsDived;

        void RuntimeAssignable.OnInactivated()
        {
            if (IsDiving)
            {
                MyPlayer.VanillaPlayer.ModDive(false);
                if (!MyPlayer.IsDead) MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetPlayersLayer();
            }
        }
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                Vector2 lastDivePoint = Vector2.zeroVector;

                var diveButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                diveButton.SetSprite(diveButtonSprite.GetSprite());
                diveButton.Availability = (button) => MyPlayer.VanillaPlayer.CanMove && !IsDiving;
                diveButton.Visibility = _ => !MyPlayer.IsDead && !IsDiving;
                diveButton.OnClick = (button) =>
                {
                    if (NebulaGameManager.Instance!.CurrentTime - acToken2.Value.lastKill < 8f) acToken2.Value.cleared = true;
                    acTokenAnother.Value.triggered = true;
                    lastDivePoint = MyPlayer.VanillaPlayer.transform.position;

                    MyPlayer.VanillaPlayer.ModDive(true);
                    MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
                    NebulaAsset.PlaySE(NebulaAudioClip.HadarDive);

                    NebulaManager.Instance.StartCoroutine(CoLight().WrapToIl2Cpp());
                    NebulaManager.Instance.StartCoroutine(CoPing().WrapToIl2Cpp());

                    if (AccelRateUndergroundOption > 1f) MyPlayer.GainAttribute(AccelRateUndergroundOption, 1000000f, false, 0, "nebula.hadar");

                    if (LeftDivingEvidenceOption)
                    {
                        NebulaManager.Instance.StartDelayAction(0.3f,() =>
                        {
                            NebulaSyncObject.RpcInstantiate(HadarEvidence.MyTag, [
                                PlayerControl.LocalPlayer.transform.localPosition.x,
                                PlayerControl.LocalPlayer.transform.localPosition.y - 0.35f
                            ]);
                        });
                    }
                };
                diveButton.CoolDownTimer = Bind(new Timer(DiveCoolDownOption).SetAsAbilityCoolDown().Start());
                diveButton.SetLabel("dive");

                var gushButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                gushButton.SetSprite(gushButtonSprite.GetSprite());

                void CheckGushAchievement()
                {
                    new StaticAchievementToken("hadar.common3"); //通算称号
                    if (MyPlayer.VanillaPlayer.transform.position.Distance(lastDivePoint) > 30f) new StaticAchievementToken("hadar.common5");
                    if (NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.AmOwner && !p.IsDead && p.VanillaPlayer.transform.position.Distance(MyPlayer.VanillaPlayer.transform.position) < 2f)) new StaticAchievementToken("hadar.common4");
                    acToken1.Value.lastGush = NebulaGameManager.Instance!.CurrentTime;
                }

                if (!GushFromVentsOption)
                {
                    gushButton.Availability = (button) => MyPlayer.VanillaPlayer.CanMove && MapData.GetCurrentMapData().CheckMapArea(PlayerControl.LocalPlayer.GetTruePosition());
                    gushButton.OnClick = (button) =>
                    {
                        MyPlayer.VanillaPlayer.ModDive(false);
                        MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetPlayersLayer();
                        NebulaAsset.PlaySE(NebulaAudioClip.HadarGush);
                        diveButton.StartCoolDown();

                        CheckGushAchievement();
                        if (AccelRateUndergroundOption > 1f) MyPlayer.GainAttribute(1f, 0f, false, 0, "nebula.hadar");
                    };
                }
                else
                {
                    Arrow? ventArrow = Bind(new Arrow(null, true));
                    ventArrow.SetColor(Palette.ImpostorRed);
                    ventArrow.IsActive = false;
                    var tracker = Bind(ObjectTrackers.ForVents(VentDetectionRangeOption, MyPlayer, v => true, Palette.ImpostorRed, true));
                    gushButton.Availability = (button) => MyPlayer.VanillaPlayer.CanMove && tracker.CurrentTarget != null;
                    gushButton.OnClick = button =>
                    {
                        MyPlayer.VanillaPlayer.ModDive(false, false);
                        MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetPlayersLayer();
                        MyPlayer.VanillaPlayer.moveable = false;
                        MyPlayer.VanillaPlayer.MyPhysics.RpcExitVent(tracker.CurrentTarget!.Id);
                        diveButton.StartCoolDown();

                        CheckGushAchievement();
                        if (AccelRateUndergroundOption > 1f) MyPlayer.GainAttribute(1f, 0f, false, 0, "nebula.hadar");
                    };
                    GameOperatorManager.Instance?.Register<GameUpdateEvent>(ev =>
                    {
                        if (tracker.CurrentTarget == null || !IsDiving)
                            ventArrow.IsActive = false;
                        else
                        {
                            ventArrow.IsActive = true;
                            ventArrow.TargetPos = tracker.CurrentTarget.transform.position;
                        }
                    }, ventArrow);
                    
                }
                gushButton.Visibility = (button) => !MyPlayer.IsDead && IsDiving;
                
                gushButton.SetLabel("gush");

                //称号

                //チャレンジ称号 ここから
                var acTokenChallenge = new AchievementToken<(float diving, int kill)>("hadar.challenge", (0f, 0), 
                    (a, _) => NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer) && !MyPlayer.IsDead && a.kill >= 3 && a.diving > 50f);

                GameOperatorManager.Instance!.Register<GameUpdateEvent>(_ => {
                    if (!MeetingHud.Instance && !ExileController.Instance && IsDiving) acTokenChallenge.Value.diving += Time.deltaTime;
                }, this);
                GameOperatorManager.Instance!.Register<PlayerKillPlayerEvent>(ev => {
                    if (ev.Murderer.AmOwner) acTokenChallenge.Value.kill++;
                }, this);
                //チャレンジ称号 ここまで

            }
        }

        void OnEmergencyMeeting()
        {
            if (IsDiving)
            {
                MyPlayer.VanillaPlayer.ModDive(false);
            }
            if (!MyPlayer.IsDead) MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetPlayersLayer();
        }

        void OnEmergencyMeetingStart(MeetingStartEvent ev) => OnEmergencyMeeting();
        void OnEmergencyMeetingEnd(MeetingVoteEndEvent ev) => OnEmergencyMeeting();

        private IEnumerator CoLight()
        {
            yield return Effects.Wait(2f);

            SpriteRenderer lightRenderer = AmongUsUtil.GenerateCustomLight(Vector2.zero);
            lightRenderer.transform.SetParent(MyPlayer.VanillaPlayer.transform);
            lightRenderer.transform.localScale = new(1.8f, 1.8f, 1.8f);
            lightRenderer.transform.localPosition = new(0f, 0f, -50f);

            float p = 0f;
            while (p < 1f && MyPlayer.IsDived)
            {
                p += Time.deltaTime * 0.85f;
                lightRenderer.material.color = new Color(1, 1, 1, p * 0.22f);
                yield return null;
            }

            while (MyPlayer.IsDived) yield return null;
            
            while (p > 0f)
            {
                p -= Time.deltaTime * 5f;
                lightRenderer.material.color = new Color(1, 1, 1, p * 0.22f);
                yield return null;
            }

            GameObject.Destroy(lightRenderer.gameObject);

            yield break;
        }

        private IEnumerator CoPing()
        {
            yield return Effects.Wait(2f);

            while (true)
            {
                bool playSE = true;
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if (!IsDiving) yield break;
                    if (p.AmOwner || p.IsDead) continue;
                    if (p.IsDived || p.Unbox().IsInvisible) continue;
                    if (p.VanillaPlayer.transform.position.Distance(MyPlayer.VanillaPlayer.transform.position) < 6f)
                    {
                        AmongUsUtil.Ping([p.VanillaPlayer.transform.position], false, playSE);
                        playSE = false;
                        yield return Effects.Wait(0.15f);
                    }
                }

                yield return Effects.Wait(2f);
            }
        }

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (NebulaGameManager.Instance!.CurrentTime - acToken1.Value.lastGush < 8f) acToken1.Value.cleared = true;
            acToken2.Value.lastKill = NebulaGameManager.Instance!.CurrentTime;
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }
        [Local]
        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.cleared |= acTokenAnother.Value.triggered;
        }
    }
}