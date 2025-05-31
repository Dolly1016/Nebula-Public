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
public class Sniper : DefinedSingleAbilityRoleTemplate<Sniper.Ability>, HasCitation, DefinedRole
{
    private Sniper() : base("sniper", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SnipeCoolDownOption, ShotSizeOption,ShotEffectiveRangeOption,ShotNoticeRangeOption,StoreRifleOnFireOption,StoreRifleOnUsingUtilityOption,CanSeeRifleInShadowOption,CanKillHidingPlayerOption,AimAssistOption,DelayInAimAssistOption, CanKillImpostorOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Sniper.png");

        MetaAbility.RegisterCircle(new("role.sniper.shotRange", () => ShotEffectiveRangeOption, () => null, UnityColor));
        MetaAbility.RegisterCircle(new("role.sniper.soundRange", () => ShotNoticeRangeOption, () => null, UnityColor));
        MetaAbility.RegisterCircle(new("role.sniper.shotSize", () => ShotSizeOption * 0.25f, () => null, UnityColor));

        GameActionTypes.SniperEquippingAction = new("sniper.equipping", this, isEquippingAction: true);
    }
    Citation? HasCitation.Citation => Citations.TownOfImpostors;

    static private IRelativeCoolDownConfiguration SnipeCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sniper.snipeCoolDown", CoolDownType.Immediate, (0f, 60f, 2.5f), 20f, (-40f, 40f, 2.5f), -10f, (0.125f, 2f, 0.125f), 1f);
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

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, player.IsImpostor ? AmongUsUtil.VanillaKillCoolDown : Jackal.KillCooldown, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static public Sniper MyRole = new Sniper();

    static private GameStatsEntry StatsShot = NebulaAPI.CreateStatsEntry("stats.sniper.shot", GameStatsCategory.Roles, MyRole, null, 10);
    static private GameStatsEntry StatsMisshot = NebulaAPI.CreateStatsEntry("stats.sniper.misshot", GameStatsCategory.Roles, MyRole, null, 9);
    [NebulaRPCHolder]
    public class SniperRifle : EquipableAbility, IGameOperator
    {
        private static SpriteLoader rifleSprite = SpriteLoader.FromResource("Nebula.Resources.SniperRifle.png", 100f);
        public SniperRifle(GamePlayer owner) : base(owner, CanSeeRifleInShadowOption, "SniperRifle")
        {
            Renderer.sprite = rifleSprite.GetSprite();
        }

        public GamePlayer? GetTarget(float width,float maxLength)
        {
            float minLength = maxLength;
            GamePlayer? result = null;

            foreach(var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDead || p.AmOwner || ((!CanKillHidingPlayerOption) && p.VanillaPlayer.inVent || p.IsDived)) continue;

                //仲間は無視
                if (!CanKillImpostorOption && !Owner.CanKill(p)) continue;

                //吹っ飛ばされているプレイヤーは無視しない

                //不可視なプレイヤーは無視
                if (p.Unbox().IsInvisible || p.WillDie) continue;

                var pos = p.VanillaPlayer.GetTruePosition();
                Vector2 diff = pos - (Vector2)Renderer.transform.position;

                //移動と回転を施したベクトル
                var vec = diff.Rotate(-Renderer.transform.eulerAngles.z);

                if(vec.x>0 && vec.x< minLength && Mathf.Abs(vec.y) < width * 0.5f)
                {
                    result = p;
                    minLength= vec.x;
                }
            }

            return result;
        }
    }

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {


        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SnipeButton.png", 115f);
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

                equipButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "sniper.equip",
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
                        var circle = EffectCircle.SpawnEffectCircle(PlayerControl.LocalPlayer.transform, Vector3.zero, Palette.ImpostorRed, ShotNoticeRangeOption, null, true);
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
                    SnipeCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "snipe", null,
                    _ => MyRifle != null, _ => !equipButton.IsBroken)
                    .SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor)
                    .SetAsMouseClickButton().SetAsUsurpableButton(this);
                killButton.OnClick = (button) =>
                {
                    StatsShot.Progress();
                    NebulaAsset.PlaySE(NebulaAudioClip.SniperShot, true);
                    var target = MyRifle?.GetTarget(ShotSizeOption, ShotEffectiveRangeOption);
                    if (target != null)
                    {
                        bool isBlown = target.IsBlown;
                        MyPlayer.MurderPlayer(target, PlayerState.Sniped, EventDetail.Kill, KillParameter.RemoteKill, result =>
                        {
                            if (result == KillResult.Kill)
                            {
                                if (target.VanillaPlayer.inMovingPlat && Helpers.CurrentMonth == 7) new StaticAchievementToken("tanabata");
                                acTokenCommon ??= new("sniper.common1");
                                if (isBlown) new StaticAchievementToken("sniper.common2");
                                if (MyPlayer.VanillaPlayer.GetTruePosition().Distance(target!.VanillaPlayer.GetTruePosition()) > 20f) acTokenChallenge.Value++;
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

                    if (StoreRifleOnFireOption) RpcEquip.Invoke((MyPlayer.PlayerId, false));

                    acTokenAnother.Value.triggered = true;

                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }
        }

        [Local]
        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (MyRifle != null) RpcEquip.Invoke((MyPlayer.PlayerId, false));

            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MyRifle != null) RpcEquip.Invoke((MyPlayer.PlayerId, false));
        }

        void OnMeetingEnd(GamePlayer[] exiled)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }


        IEnumerator CoShowAimAssist()
        {
            IEnumerator CoUpdateAimAssistArrow(PlayerControl player)
            {
                DeadBody? deadBody = null;
                Vector2 pos = Vector2.zero;
                Vector2 dir = Vector2.zero;
                Vector2 tempDir = Vector2.zero;
                bool isFirst = true;

                Color targetColor = new Color(55f / 225f, 1f, 0f);
                float t = 0f;

                SpriteRenderer? renderer = null;

                while (true)
                {
                    if (MeetingHud.Instance || MyPlayer.IsDead || MyRifle == null || IsDeadObject) break;

                    if (player.Data.IsDead && !deadBody) deadBody = Helpers.GetDeadBody(player.PlayerId);

                    //死亡して、死体も存在しなければ追跡を終了
                    if (player.Data.IsDead && !deadBody) break;

                    if(renderer == null)
                    {
                        renderer = UnityHelper.CreateObject<SpriteRenderer>("AimAssist", HudManager.Instance.transform, Vector3.zero);
                        renderer.sprite = aimAssistSprite.GetSprite();
                    }

                    pos = player.Data.IsDead ? deadBody!.transform.position : player.transform.position;
                    tempDir = (pos - (Vector2)PlayerControl.LocalPlayer.transform.position).normalized;

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
                        dir = (tempDir + dir).normalized;
                    }

                    float angle = Mathf.Atan2(dir.y, dir.x) + localRotateZ.DegToRad();
                    renderer.transform.eulerAngles = new Vector3(0, 0, angle.RadToDeg());
                    renderer.transform.localPosition = new Vector3(Mathf.Cos(angle) * 2f, Mathf.Sin(angle) * 2f, -30f);

                    t += Time.deltaTime / 0.8f;
                    if (t > 1f) t = 1f;
                    renderer.color = Color.Lerp(Color.white, targetColor, t).AlphaMultiplied(0.6f);

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

            foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
            {
                if (!p.AmOwner) NebulaManager.Instance.StartCoroutine(CoUpdateAimAssistArrow(p).WrapToIl2Cpp());
            }
        }

        void EquipRifle()
        {
            MyRifle = new SniperRifle(MyPlayer).Register(this);

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
            var role = NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Role;
            var sniper = role.GetAbility<Ability>();
            if (sniper != null)
            {
                if (message.equip)
                    sniper.EquipRifle();
                else
                    sniper.UnequipRifle();
            }
        }
        );
    }

    private static SpriteLoader snipeNoticeSprite = SpriteLoader.FromResource("Nebula.Resources.SniperRifleArrow.png", 200f);
    public static RemoteProcess<Vector2> RpcShowNotice = new(
        "ShowSnipeNotice",
        (message, _) =>
        {
            if ((message - (Vector2)PlayerControl.LocalPlayer.transform.position).magnitude < ShotNoticeRangeOption)
            {
                var arrow = new Arrow(snipeNoticeSprite.GetSprite(), false) { IsSmallenNearPlayer = false, IsAffectedByComms = false, FixedAngle = true, OnJustPoint = true };
                arrow.Register(arrow);
                arrow.TargetPos = message;
                NebulaManager.Instance.StartCoroutine(arrow.CoWaitAndDisappear(3f).WrapToIl2Cpp());
            }
        }
        );
}
