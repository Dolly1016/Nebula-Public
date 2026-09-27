using Epic.OnlineServices.Mods;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Roles.Abilities;
using Nebula.Roles.Neutral;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Sniper : DefinedSingleAbilityRoleTemplate<Sniper.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Sniper() : base("sniper", VColor.ImpostorColor, RoleCategory.ImpostorRole, Impostor.MyTeam, [SnipeCoolDownOption, ShotSizeOption,ShotEffectiveRangeOption,ShotNoticeRangeOption,StoreRifleOnFireOption,StoreRifleOnUsingUtilityOption,CanSeeRifleInShadowOption,CanKillHidingPlayerOption,AimAssistOption,DelayInAimAssistOption, CanKillImpostorOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Sniper.png");

        MetaAbility.RegisterCircle(new("role.sniper.shotRange", () => ShotEffectiveRangeOption, () => null, RoleColor));
        MetaAbility.RegisterCircle(new("role.sniper.soundRange", () => ShotNoticeRangeOption, () => null, RoleColor));
        MetaAbility.RegisterCircle(new("role.sniper.shotSize", () => ShotSizeOption * 0.25f, () => null, RoleColor));

        GameActionTypes.SniperEquippingAction = new("sniper.equip", this, isEquippingAction: true);
    }
    Citation? HasCitation.Citation => Citations.TownOfImpostors;

    static private IRelativeCooldownConfiguration SnipeCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sniper.snipeCoolDown", CoolDownType.Immediate, (0f, 60f, 2.5f), 20f, (-40f, 40f, 2.5f), -10f, (0.125f, 2f, 0.125f), 1f);
    static private FloatConfiguration ShotSizeOption = NebulaAPI.Configurations.Configuration("options.role.sniper.shotSize", (0.25f, 4f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration ShotEffectiveRangeOption = NebulaAPI.Configurations.Configuration("options.role.sniper.shotEffectiveRange", (2.5f, 50f, 2.5f), 25f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration ShotNoticeRangeOption = NebulaAPI.Configurations.Configuration("options.role.sniper.shotNoticeRange", (2.5f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Ratio);
    static private BoolConfiguration StoreRifleOnFireOption = NebulaAPI.Configurations.Configuration("options.role.sniper.storeRifleOnFire", true);
    static private BoolConfiguration StoreRifleOnUsingUtilityOption = NebulaAPI.Configurations.Configuration("options.role.sniper.storeRifleOnUsingUtility", false);
    static private BoolConfiguration CanSeeRifleInShadowOption = NebulaAPI.Configurations.Configuration("options.role.sniper.canSeeRifleInShadow", false);
    static private BoolConfiguration CanKillHidingPlayerOption = NebulaAPI.Configurations.Configuration("options.role.sniper.canKillHidingPlayer", false);
    static private BoolConfiguration AimAssistOption = NebulaAPI.Configurations.Configuration("options.role.sniper.aimAssist", false);
    static private FloatConfiguration DelayInAimAssistOption = NebulaAPI.Configurations.Configuration("options.role.sniper.delayInAimAssistActivation", (0f, 20f, 1f), 3f, FloatConfigurationDecorator.Second, () => AimAssistOption);
    static private BoolConfiguration CanKillImpostorOption = NebulaAPI.Configurations.Configuration("options.role.sniper.canKillImpostor", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, player.IsImpostor ? AmongUsLLImpl.Instance.VanillaKillCooldown : Jackal.KillCooldown, arguments.GetAsBool(0));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.Killers;

    static public Sniper MyRole = new Sniper();

    static private GameStatsEntry StatsShot = NebulaAPI.CreateStatsEntry("stats.sniper.shot", GameStatsCategory.Roles, MyRole, null, 10);
    static private GameStatsEntry StatsMisshot = NebulaAPI.CreateStatsEntry("stats.sniper.misshot", GameStatsCategory.Roles, MyRole, null, 9);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonSprite, "role.sniper.ability.equip");
        yield return new(ModAbilityButtonImpl.VanillaKillImage, "role.sniper.ability.kill");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%AIMASSIST%", AimAssistOption ? Language.Translate("role.sniper.tips.aimAssist") : "");
        yield return new("%DELAY%", DelayInAimAssistOption.GetValue().DecimalToString("1"));
        yield return new("%SOUND%", ShotNoticeRangeOption.GetValue().DecimalToString("1"));
    }

    [NebulaRPCHolder]
    public class SniperRifle : EquipableAbility, IGameOperator
    {
        private static SpriteLoader rifleSprite = SpriteLoader.FromResource("Nebula.Resources.SniperRifle.png", 100f);
        public SniperRifle(GamePlayer owner) : base(owner, CanSeeRifleInShadowOption, "SniperRifle")
        {
            Renderer.sprite = rifleSprite.GetSprite();
        }

        public IPlayerlike? GetTarget(float width,float maxLength)
        {
            float minLength = maxLength;
            IPlayerlike? result = null;

            var rendererTransform = Renderer.transform;
            foreach (var p in GamePlayer.AllPlayerlikes)
            {
                if (p.IsDead || p.AmOwner || ((!CanKillHidingPlayerOption) && p.Logic.InVent || p.IsDived)) continue;

                //仲間は無視
                if (!CanKillImpostorOption && !Owner.CanKill(p.RealPlayer)) continue;

                //吹っ飛ばされているプレイヤーは無視しない

                //不可視なプレイヤーは無視
                if (p.IsInvisible || p.WillDie) continue;

                var pos = p.TruePosition;
                VVector2 diff = pos - (VVector2)rendererTransform.GetPositionFast();

                //移動と回転を施したベクトル
                var vec = diff.Rotate(-rendererTransform.eulerAngles.z);

                if(vec.x>0 && vec.x< minLength && Mathn.Abs(vec.y) < width * 0.5f)
                {
                    result = p;
                    minLength= vec.x;
                }
            }

            return result;
        }
    }

    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;

    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SnipeButton.png", 115f);
    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image aimAssistSprite = SpriteLoader.FromResource("Nebula.Resources.SniperGuide.png", 100f);
        
        public SniperRifle? MyRifle = null;
        bool IPlayerAbility.HideKillButton => !(equipButton?.IsBroken ?? false);

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;


        [Local]
        void LocalUpdate(GameUpdateEvent ev)
        {
            if (MyRifle != null && StoreRifleOnUsingUtilityOption)
            {
                var p = MyPlayer.VanillaPlayer;
                if (p.onLadder || p.inMovingPlat || p.inVent) RpcEquip.Invoke((MyPlayer.PlayerId, false));
            }
        }

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        ModAbilityButton equipButton = null!;

        public Ability(GamePlayer player, float defaultCooldown, bool isUsurped) :base(player, isUsurped)
        {
            if (AmOwner)
            {
                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("sniper.another1");
                AchievementToken<int> acTokenChallenge = new("sniper.challenge", 0, (val, _) => val >= 2);

                equipButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.FixedAbility, "sniper.equip",
                    0f, "equip", buttonSprite).SetAsUsurpableButton(this);
                equipButton.OnClick = (button) =>
                {
                    if (MyRifle == null)
                    {
                        NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.SniperEquippingAction);
                        NebulaAsset.PlaySE(NebulaAudioClip.SniperEquip, true);
                        equipButton.SetLabel("unequip");
                    }
                    else
                        equipButton.SetLabel("equip");

                    RpcEquip.Invoke((MyPlayer.PlayerId, MyRifle == null));

                    if(MyRifle != null)
                    {
                        var circle = EffectCircle.SpawnEffectCircle(AmongUsLLImpl.LocalPlayer.transform, VVector3.Zero, VColor.ImpostorColor, ShotNoticeRangeOption, null, true);
                        var script = circle.gameObject.AddComponent<ScriptBehaviour>();
                        script.UpdateHandler += () =>
                        {
                            if (MyRifle == null) circle.Disappear();
                        };
                        this.BindGameObject(circle.gameObject);
                    }
                };
                equipButton.OnBroken = (button) =>
                {
                    if (MyRifle != null)
                    {
                        equipButton.SetLabel("equip");
                        RpcEquip.Invoke((MyPlayer.PlayerId, false));
                    }
                    Snatcher.RewindKillCooldown();
                };
                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev => equipButton.SetLabel("equip"), this);

                var killButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, true, false, Virial.Compat.VirtualKeyInput.Kill, "sniper.kill",
                    SnipeCoolDownOption.GetCooldown(MyPlayer.TeamKillCooldown), "snipe", null,
                    _ => MyRifle != null, _ => !equipButton.IsBroken)
                    .SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor)
                    .SetAsMouseClickButton().SetAsUsurpableButton(this);
                killButton.OnClick = (button) =>
                {
                    StatsShot.Progress();
                    NebulaAsset.PlaySE(NebulaAudioClip.SniperShot, true);
                    var target = MyRifle?.GetTarget(ShotSizeOption, ShotEffectiveRangeOption);
                    if (target != null && !(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, target, new(IsKillInteraction: true))).IsCanceled ?? false))
                    {
                        bool isBlown = target.IsBlown;
                        MyPlayer.MurderPlayer(target, PlayerState.Sniped, EventDetail.Kill, KillParameter.RemoteKill, result =>
                        {
                            if (result == KillResult.Kill)
                            {
                                if (target.Logic.InMovingPlat && Helpers.CurrentMonth == 7) new StaticAchievementToken("tanabata");
                                acTokenCommon ??= new("sniper.common1");
                                if (isBlown) new StaticAchievementToken("sniper.common2");
                                if (MyPlayer.VanillaPlayer.GetTruePosition().Distance(target!.TruePosition) > 20f) acTokenChallenge.Value++;
                            }
                        });
                    }
                    else
                    {
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Missed, MyPlayer.VanillaPlayer, 0);
                        StatsMisshot.Progress();
                    }
                    Sniper.RpcShowNotice.Invoke(MyPlayer.Position);

                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();

                    if (StoreRifleOnFireOption)
                    {
                        RpcEquip.Invoke((MyPlayer.PlayerId, false));
                        equipButton.SetLabel("equip");
                    }

                    acTokenAnother.Value.triggered = true;

                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }
        }

        [Local]
        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        [OnlyMyPlayer]
        void OnDeadForAllPlayer(PlayerDieEvent ev)
        {
            RpcEquip.LocalInvoke((MyPlayer.PlayerId, false));
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MyRifle != null)
            {
                if (AmOwner) RpcEquip.Invoke((MyPlayer.PlayerId, false));
                else RpcEquip.LocalInvoke((MyPlayer.PlayerId, false));
            }
        }

        void OnMeetingEnd(GamePlayer[] exiled)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }

        [Local]
        void OnInfrequentUpdate(GameInfrequentUpdateEvent ev)
        {
            RpcSniperHertbeat.Invoke((MyPlayer.PlayerId, MyRifle != null));
        }

        IEnumerator CoShowAimAssist()
        {
            IEnumerator CoUpdateAimAssistArrow(GamePlayer player)
            {
                Virial.Game.DeadBody? deadBody = null;
                VVector2 pos = VVector2.Zero;
                VVector2 dir = VVector2.Zero;
                VVector2 tempDir = VVector2.Zero;
                bool isFirst = true;

                VColor targetColor = new VColor(55f / 225f, 1f, 0f);
                float t = 0f;

                SpriteRenderer? renderer = null;

                while (true)
                {
                    if (MeetingHud.Instance.AsBoolFast() || MyPlayer.IsDead || MyRifle == null || IsDeadObject) break;

                    //既に死亡していて、死体もないならば何もしない
                    if (player.IsDead && !ModSingleton<DeadBodyManager>.Instance.AllDeadBodies.Find(d => d.Player.PlayerId == player.PlayerId, out deadBody)) break;
                    

                    if(!renderer.AsBoolFast())
                    {
                        renderer = UnityHelper.CreateObject<SpriteRenderer>("AimAssist", AmongUsLLImpl.HudManagerBridge.MyTransform, Vector3.zero);
                        renderer.sprite = aimAssistSprite.GetSprite();
                    }

                    pos = player.IsDead ? deadBody!.Position : player.Position;
                    tempDir = (pos - (GamePlayer.LocalPlayer?.Position ?? new(0f,0f))).Normalized;

                    NebulaGameManager.Instance!.WideCamera.CheckPlayerState(out var localScale, out var localRotateZ);
                    tempDir.x *= localScale.x;
                    tempDir.y *= localScale.y;

                    if (isFirst)
                    {
                        dir = tempDir;
                        isFirst = false;
                    }
                    else
                    {
                        dir = (tempDir + dir).Normalized;
                    }

                    float angle = Mathn.Atan2(dir.y, dir.x) + localRotateZ.DegToRad();
                    renderer.transform.eulerAngles = new Vector3(0, 0, angle.RadToDeg());
                    renderer.transform.localPosition = new Vector3(Mathn.Cos(angle) * 2f, Mathn.Sin(angle) * 2f, -30f);

                    t += Time.deltaTime / 0.8f;
                    if (t > 1f) t = 1f;
                    renderer.color = VColor.Lerp(VColor.White, targetColor, t).AlphaMultiplied(0.6f).ToUnityColor();

                    yield return null;
                }

                if (renderer == null) yield break;

                float a = 0.6f;
                while(a > 0f)
                {
                    a -= Time.deltaTime / 0.8f;
                    var color = renderer.color;
                    color.a = a;
                    renderer.color = color;
                    yield return null;
                }
                
                GameObject.Destroy(renderer.gameObject);
            }

            yield return new WaitForSeconds(DelayInAimAssistOption);

            foreach (var p in GamePlayer.AllPlayers)
            {
                if (!p.AmOwner) NebulaManager.Instance.StartCoroutine(CoUpdateAimAssistArrow(p).WrapToIl2Cpp());
            }
        }

        void EquipRifle()
        {
            if (MyRifle != null && MyRifle.IsDeadObject) MyRifle = null;
            if (MyRifle == null) MyRifle = new SniperRifle(MyPlayer).Register(this);

            if (AmOwner && AimAssistOption) NebulaManager.Instance.StartCoroutine(CoShowAimAssist().WrapToIl2Cpp());
        }

        void UnequipRifle()
        {
            if (MyRifle != null) MyRifle.Release();
            MyRifle = null;
        }

        static RemoteProcess<(byte playerId, bool equip)> RpcEquip = new(
        "EquipRifle",
        (message, _) =>
        {
            var player = NebulaGameManager.Instance?.GetPlayer(message.playerId);
            if (player?.TryGetAbility<Ability>(out var sniper) ?? false)
            {
                if (message.equip)
                {
                    sniper.EquipRifle();
                }
                else
                {
                    sniper.UnequipRifle();
                }
            }
        }
        );


        static RemoteProcess<(byte playerId, bool equip)> RpcSniperHertbeat = new("SniperHeartbeat", (message, _) =>
        {
            RpcEquip.LocalInvoke((message.playerId, message.equip));
        }, false);
    }

    private static SpriteLoader snipeNoticeSprite = SpriteLoader.FromResource("Nebula.Resources.SniperRifleArrow.png", 200f);
    public static RemoteProcess<VVector2> RpcShowNotice = new(
        "ShowSnipeNotice",
        (message, _) =>
        {
            if ((message - GamePlayer.LocalPlayer.Position).Magnitude < ShotNoticeRangeOption)
            {
                var arrow = new Arrow(snipeNoticeSprite.GetSprite(), false) { IsSmallenNearPlayer = false, IsAffectedByComms = false, FixedAngle = true, OnJustPoint = true };
                arrow.Register(arrow);
                arrow.TargetPos = message;
                NebulaManager.Instance.StartCoroutine(arrow.CoWaitAndDisappear(3f).WrapToIl2Cpp());
            }
        }
        );
}
