using Nebula.Behaviour;
using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Sniper : ConfigurableStandardRole, HasCitation
{
    static public Sniper MyRole = new Sniper();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "sniper";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TownOfImpostors;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private KillCoolDownConfiguration SnipeCoolDownOption = null!;
    private NebulaConfiguration ShotSizeOption = null!;
    private NebulaConfiguration ShotEffectiveRangeOption = null!;
    private NebulaConfiguration ShotNoticeRangeOption = null!;
    private NebulaConfiguration StoreRifleOnFireOption = null!;
    private NebulaConfiguration StoreRifleOnUsingUtilityOption = null!;
    private NebulaConfiguration CanSeeRifleInShadowOption = null!;
    private NebulaConfiguration CanKillHidingPlayerOption = null!;
    private NebulaConfiguration AimAssistOption = null!;
    private NebulaConfiguration DelayInAimAssistOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagFunny, ConfigurationHolder.TagDifficult);

        SnipeCoolDownOption = new(RoleConfig, "snipeCoolDown", KillCoolDownConfiguration.KillCoolDownType.Immediate, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 20f, -10f, 1f);
        ShotSizeOption = new(RoleConfig, "shotSize", null, 0.25f, 4f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
        ShotEffectiveRangeOption = new(RoleConfig, "shotEffectiveRange", null, 2.5f, 60f, 2.5f, 25f, 25f) { Decorator = NebulaConfiguration.OddsDecorator };
        ShotNoticeRangeOption = new(RoleConfig, "shotNoticeRange", null, 2.5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.OddsDecorator };
        StoreRifleOnFireOption = new(RoleConfig, "storeRifleOnFire", null, true, true);
        StoreRifleOnUsingUtilityOption = new(RoleConfig, "storeRifleOnUsingUtility", null, false, false);
        CanSeeRifleInShadowOption = new(RoleConfig, "canSeeRifleInShadow", null, false, false);
        CanKillHidingPlayerOption = new(RoleConfig, "canKillHidingPlayer", null, false, false);
        AimAssistOption = new(RoleConfig, "aimAssist", null, false, false);
        DelayInAimAssistOption = new(RoleConfig, "delayInAimAssistActivation", null, 0f, 20f, 1f, 3f, 3f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    [NebulaRPCHolder]
    public class SniperRifle : INebulaScriptComponent, IGameOperator
    {
        public GamePlayer Owner { get; private set; }
        public SpriteRenderer Renderer { get; private set; }
        private static SpriteLoader rifleSprite = SpriteLoader.FromResource("Nebula.Resources.SniperRifle.png", 100f);
        public SniperRifle(GamePlayer owner) : base()
        {
            Owner = owner;
            Renderer = UnityHelper.CreateObject<SpriteRenderer>("SniperRifle", null, owner.VanillaPlayer.transform.position, LayerExpansion.GetObjectsLayer());
            Renderer.sprite = rifleSprite.GetSprite();
            Renderer.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            Renderer.gameObject.layer = MyRole.CanSeeRifleInShadowOption ? LayerExpansion.GetObjectsLayer() : LayerExpansion.GetDefaultLayer();
        }

        void HudUpdate(GameHudUpdateEvent ev)
        {
            var o = Owner.Unbox();
            if (Owner.AmOwner) o.RequireUpdateMouseAngle();
            Renderer.transform.localEulerAngles = new Vector3(0, 0, o.MouseAngle * 180f / Mathf.PI);
            var pos = Owner.VanillaPlayer.transform.position + new Vector3(Mathf.Cos(o.MouseAngle), Mathf.Sin(o.MouseAngle), -1f) * 0.87f;
            var diff = (pos - Renderer.transform.position) * Time.deltaTime * 7.5f;
            Renderer.transform.position += diff;
            Renderer.flipY = Mathf.Cos(o.MouseAngle) < 0f;
        }

        void IGameOperator.OnReleased()
        {
            if (Renderer) GameObject.Destroy(Renderer.gameObject);
            Renderer = null!;
        }

        public GamePlayer? GetTarget(float width,float maxLength)
        {
            float minLength = maxLength;
            GamePlayer? result = null;

            foreach(var p in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (p.IsDead || p.AmOwner || ((!MyRole.CanKillHidingPlayerOption) && p.VanillaPlayer.inVent)) continue;

                //インポスターは無視
                if (p.Role.Role.Category == RoleCategory.ImpostorRole) continue;
                //不可視なプレイヤーは無視
                if (p.Unbox().IsInvisible) continue;

                var pos = p.VanillaPlayer.GetTruePosition();
                Vector2 diff = pos - (Vector2)Renderer.transform.position;

                //移動と回転を施したベクトル
                var vec = diff.Rotate(-Renderer.transform.eulerAngles.z);

                if(vec.x>0 && vec.x< minLength && Mathf.Abs(vec.y) < width / 2f)
                {
                    result = p;
                    minLength= vec.x;
                }
            }

            return result;
        }
    }

    [NebulaRPCHolder]
    public class Instance : Impostor.Instance, IGamePlayerOperator
    {
        private ModAbilityButton? equipButton = null;
        private ModAbilityButton? killButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SnipeButton.png", 115f);
        static private ISpriteLoader aimAssistSprite = SpriteLoader.FromResource("Nebula.Resources.SniperGuide.png", 100f);
        public override AbstractRole Role => MyRole;
        public SniperRifle? MyRifle = null;
        public override bool HasVanillaKillButton => false;

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;

        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void LocalUpdate()
        {
            if (MyRifle != null && MyRole.StoreRifleOnUsingUtilityOption)
            {
                var p = MyPlayer.VanillaPlayer;
                if (p.onLadder || p.inMovingPlat || p.inVent) RpcEquip.Invoke((MyPlayer.PlayerId, false));
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("sniper.another1");
                AchievementToken<int> acTokenChallenge = new("sniper.challenge", 0, (val, _) => val >= 2);

                equipButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                equipButton.SetSprite(buttonSprite.GetSprite());
                equipButton.Availability = (button) => MyPlayer.CanMove;
                equipButton.Visibility = (button) => !MyPlayer.IsDead;
                equipButton.OnClick = (button) =>
                {
                    if (MyRifle == null)
                    {
                        NebulaAsset.PlaySE(NebulaAudioClip.SniperEquip);
                        equipButton.SetLabel("unequip");
                    }
                    else
                        equipButton.SetLabel("equip");

                    RpcEquip.Invoke((MyPlayer.PlayerId, MyRifle == null));

                    if(MyRifle != null)
                    {
                        var circle = EffectCircle.SpawnEffectCircle(PlayerControl.LocalPlayer.transform, Vector3.zero, Palette.ImpostorRed, MyRole.ShotNoticeRangeOption.GetFloat(), null, true);
                        var script = circle.gameObject.AddComponent<ScriptBehaviour>();
                        script.UpdateHandler += () =>
                        {
                            if (MyRifle == null) circle.Disappear();
                        };
                        Bind(new GameObjectBinding(circle.gameObject));
                    }
                };
                equipButton.SetLabel("equip");

                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => MyRifle != null && MyPlayer.CanMove;
                killButton.Visibility = (button) => !MyPlayer.IsDead;
                killButton.OnClick = (button) =>
                {
                    NebulaAsset.PlaySE(NebulaAudioClip.SniperShot);
                    var target = MyRifle?.GetTarget(MyRole.ShotSizeOption.GetFloat(), MyRole.ShotEffectiveRangeOption.GetFloat());
                    if (target != null)
                    {
                        MyPlayer.MurderPlayer(target, PlayerState.Sniped, EventDetail.Kill, false);

                        acTokenCommon ??= new("sniper.common1");
                        if (MyPlayer.VanillaPlayer.GetTruePosition().Distance(target!.VanillaPlayer.GetTruePosition()) > 20f) acTokenChallenge.Value++;
                    }
                    else
                    {
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Missed, MyPlayer.VanillaPlayer, 0);
                    }
                    Sniper.RpcShowNotice.Invoke(MyPlayer.VanillaPlayer.GetTruePosition());

                    button.StartCoolDown();

                    if (MyRole.StoreRifleOnFireOption) RpcEquip.Invoke((MyPlayer.PlayerId, false));

                    acTokenAnother.Value.triggered = true;

                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.SnipeCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
                killButton.SetLabel("snipe");
                killButton.SetCanUseByMouseClick();
            }
        }

        void IGamePlayerOperator.OnDead()
        {
            if (AmOwner && MyRifle != null) RpcEquip.Invoke((MyPlayer.PlayerId, false));

            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MyRifle != null) RpcEquip.Invoke((MyPlayer.PlayerId, false));
            equipButton?.SetLabel("equip");
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
                    if (MeetingHud.Instance || MyPlayer.IsDead || MyRifle == null) break;

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
                    if (isFirst)
                    {
                        dir = tempDir;
                        isFirst = false;
                    }
                    else
                    {
                        dir = (tempDir + dir).normalized;
                    }
                    
                    float angle = Mathf.Atan2(dir.y, dir.x);
                    renderer.transform.eulerAngles = new Vector3(0, 0, angle * 180f / (float)Math.PI);
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

            yield return new WaitForSeconds(MyRole.DelayInAimAssistOption.GetFloat());

            foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
            {
                if (!p.AmOwner) NebulaManager.Instance.StartCoroutine(CoUpdateAimAssistArrow(p).WrapToIl2Cpp());
            }
        }

        void EquipRifle()
        {
            MyRifle = Bind(new SniperRifle(MyPlayer));

            if (AmOwner && MyRole.AimAssistOption) NebulaManager.Instance.StartCoroutine(CoShowAimAssist().WrapToIl2Cpp());
        }

        void UnequipRifle()
        {
            if (MyRifle != null) MyRifle.ReleaseIt();
            MyRifle = null;
        }

        static RemoteProcess<(byte playerId, bool equip)> RpcEquip = new(
        "EquipRifle",
        (message, _) =>
        {
            var role = NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Role;
            if (role is Sniper.Instance sniper)
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
            if ((message - (Vector2)PlayerControl.LocalPlayer.transform.position).magnitude < Sniper.MyRole.ShotNoticeRangeOption.GetFloat())
            {
                var arrow = new Arrow(snipeNoticeSprite.GetSprite(), false) { IsSmallenNearPlayer = false, IsAffectedByComms = false, FixedAngle = true };
                arrow.TargetPos = message;
                NebulaManager.Instance.StartCoroutine(arrow.CoWaitAndDisappear(3f).WrapToIl2Cpp());
            }
        }
        );
}
