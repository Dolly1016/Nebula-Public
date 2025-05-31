using Cpp2IL.Core.Extensions;
using Nebula.Game.Statistics;
using Nebula.Map;
using Nebula.Roles.Abilities;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Raider : DefinedSingleAbilityRoleTemplate<Raider.Ability>, DefinedRole
{
    private Raider() : base("raider", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [ThrowCoolDownOption, AxeSizeOption, AxeSpeedOption,CanKillImpostorOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Raider.png");

        MetaAbility.RegisterCircle(new("role.raider.axeSize", () => AxeSizeOption * 0.4f, () => null, UnityColor));

        GameActionTypes.RaiderEquippingAction = new("raider.equipping", this, isEquippingAction: true);
        GameActionTypes.RaiderThrowingAction = new("raider.throwing", this, isPhysicalAction: true);
    }


    static private IRelativeCoolDownConfiguration ThrowCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.raider.throwCoolDown", CoolDownType.Immediate, (10f, 60f, 2.5f), 20f, (-40f, 40f, 2.5f), -10f, (0.125f, 2f, 0.125f), 1f);
    static private FloatConfiguration AxeSizeOption = NebulaAPI.Configurations.Configuration("options.role.raider.axeSize", (0.25f, 4f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration AxeSpeedOption = NebulaAPI.Configurations.Configuration("options.role.raider.axeSpeed", (0.5f, 4f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private BoolConfiguration CanKillImpostorOption = NebulaAPI.Configurations.Configuration("options.role.raider.canKillImpostor", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Raider MyRole = new();
    static private GameStatsEntry StatsThrown = NebulaAPI.CreateStatsEntry("stats.raider.thrown", GameStatsCategory.Roles, MyRole);
    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class RaiderAxe : NebulaSyncStandardObject, IGameOperator
    {
        public static readonly string MyTag = "RaiderAxe";
        public static readonly string MyLocalFakeTag = "RaiderAxeLocalFake";
        public static readonly string MyGlobalFakeTag = "RaiderAxeGlobalFake";

        private static SpriteLoader staticAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxe.png", 150f);
        private static SpriteLoader thrownAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeThrown.png", 150f);
        private static SpriteLoader stuckAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeCrashed.png", 150f);

        private float thrownAngle = 0f;
        private int state = 0;
        private float speed = AxeSpeedOption;
        private int killedMask = 0;
        private int tryKillMask = 0;
        private int killedNum = 0;
        private float thrownTime = 0f;
        private float thrownDistance = 0f;
        AchievementToken<int>? acTokenChallenge = null;

        private bool fakeLocal = false;
        

        public RaiderAxe(PlayerControl owner) : base(owner.GetTruePosition(),ZOption.Front,false,staticAxeSprite.GetSprite())
        {
        }

        public RaiderAxe(PlayerControl owner, bool fakeLocal, Vector2? pos = null) : this(owner)
        {
            this.fakeLocal = fakeLocal;
            this.state = 2;
            MyRenderer.sprite = stuckAxeSprite.GetSprite();
            CanSeeInShadow = true;

            float thrownAngle = 0f;

            if (pos.HasValue) {
                Position = pos.Value;
                var diff = (pos.Value - (Vector2)owner.transform.position);
                thrownAngle = Mathf.Atan2(diff.y, diff.x);
                MyRenderer.flipY = diff.x < 0f;
            }

            MyRenderer.transform.eulerAngles = new Vector3(0f, 0f, thrownAngle * 180f / Mathf.PI);
        }

        void HudUpdate(GameHudUpdateEvent ev)
        {
            if (state == 0)
            {
                if (AmOwner) Owner.Unbox().RequireUpdateMouseAngle();
                MyRenderer.transform.localEulerAngles = new Vector3(0, 0, Owner.Unbox().MouseAngle * 180f / Mathf.PI);
                var pos = Owner.VanillaPlayer.transform.position + new Vector3(Mathf.Cos(Owner.Unbox().MouseAngle), Mathf.Sin(Owner.Unbox().MouseAngle), -1f) * 0.67f;
                var diff = (pos - MyRenderer.transform.position) * Time.deltaTime * 7.5f;
                Position += (Vector2)diff;
                MyRenderer.flipY = Mathf.Cos(Owner.Unbox().MouseAngle) < 0f;

                if (AmOwner)
                {
                    var vec = MyRenderer.transform.position - PlayerControl.LocalPlayer.transform.position;
                    if(PhysicsHelpers.AnyNonTriggersBetween(PlayerControl.LocalPlayer.GetTruePosition(),(Vector2)vec.normalized,((Vector2)vec).magnitude, Constants.ShipAndAllObjectsMask) && !Physics2D.Raycast(PlayerControl.LocalPlayer.GetTruePosition(), vec, vec.magnitude, 1 << LayerExpansion.GetRaiderColliderLayer()))
                        MyRenderer.color = Color.red;
                    else
                        MyRenderer.color = Color.white;
                }
            }
            else if (state == 1)
            {
                //進行方向ベクトル
                var vec = new Vector2(Mathf.Cos(thrownAngle), Mathf.Sin(thrownAngle));

                if (AmOwner)
                {
                    var pos = Position;
                    var size = AxeSizeOption;
                    if (!MeetingHud.Instance)
                    {
                        foreach (var p in NebulaGameManager.Instance.AllPlayerInfo)
                        {
                            if (p.IsDead || p.AmOwner) continue;

                            if (!CanKillImpostorOption && !Owner.CanKill(p)) continue;


                            //ベント内、吹っ飛ばされ中、および地底のプレイヤーを無視
                            if (p.IsDived || p.VanillaPlayer.inVent || p.IsBlown || p.WillDie) continue;

                            if ((tryKillMask & (1 << p.PlayerId)) != 0) continue;//一度キルを試行しているならなにもしない。

                            if (!Helpers.AnyNonTriggersBetween(p.TruePosition,pos,out var diff,Constants.ShipAndAllObjectsMask) && diff.magnitude < size * 0.4f)
                            {
                                //不可視なプレイヤーは無視
                                if (p.Unbox().IsInvisible) continue;

                                Owner.MurderPlayer(p, PlayerState.Beaten, EventDetail.Kill, KillParameter.RemoteKill, KillCondition.TargetAlive, result =>
                                {
                                    if(result == KillResult.Kill)
                                    {
                                        if (p.VanillaPlayer.inMovingPlat && Helpers.CurrentMonth == 7) new StaticAchievementToken("tanabata");

                                        killedMask |= 1 << p.PlayerId;
                                        killedNum++;
                                        if (killedNum >= 3)
                                        {
                                            acTokenChallenge ??= new("raider.challenge", killedMask, (val, _) =>
                                            /*人数都合でゲームが終了している*/ NebulaGameManager.Instance!.EndState!.EndReason == GameEndReason.Situation &&
                                            /*勝利している*/ NebulaGameManager.Instance.EndState!.Winners.Test(Owner) &&
                                            /*最後の死亡者がこの斧によってキルされている*/ (killedMask & (1 << (NebulaGameManager.Instance.LastDead?.PlayerId ?? -1))) != 0
                                            );
                                            acTokenChallenge.Value = killedMask;
                                        }
                                    }
                                });
                                tryKillMask |= 1 << p.PlayerId;
                            }
                        }
                    }
                }

                float d = speed * 4f * Time.deltaTime;
                if (thrownDistance > 50f)
                {
                    state = 2;
                    MyRenderer.gameObject.SetActive(false);
                    NebulaManager.Instance.StartCoroutine(ManagedEffects.CoDisappearEffect(MyRenderer.gameObject.layer, null, MyRenderer.transform.position, 0.8f).WrapToIl2Cpp());
                }
                else if (!OverlapAxeIgnoreArea(MyRenderer.transform.position) && NebulaPhysicsHelpers.AnyNonTriggersBetween(MyRenderer.transform.position, vec, speed * 4f * Time.deltaTime, Constants.ShipAndAllObjectsMask, out d))
                {
                    state = 2;
                    MyRenderer.sprite = stuckAxeSprite.GetSprite();
                    MyRenderer.transform.eulerAngles = new Vector3(0f, 0f, thrownAngle * 180f / Mathf.PI);

                    if (AmOwner && killedMask == 0)
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Missed, NebulaGameManager.Instance.CurrentTime - thrownTime, PlayerControl.LocalPlayer, 0);
                }
                else
                {
                    MyRenderer.transform.localEulerAngles += new Vector3(0f, 0f, MyRenderer.flipY ? Time.deltaTime * 2000f : Time.deltaTime * -2000f);
                }

                Position += vec * d;
                thrownDistance += d;
            }
            else if (state == 2) {
                if (fakeLocal)
                {
                    var angle = PlayerModInfo.LocalMouseInfo.angle;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    MyRenderer.flipY = dir.x < 0f;
                    MyRenderer.transform.localEulerAngles = new Vector3(0, 0, angle * 180f / Mathf.PI);
                    var hits = Physics2D.RaycastAll(Owner.Position, dir, 0.8f, Constants.ShipAndAllObjectsMask).Where(h => !h.collider.isTrigger).ToArray();

                    if(hits.Length > 0)
                    {
                        var hit = hits.MinBy(h => h.distance);
                        Position = hit.point;
                        Color = Color.cyan;
                        MyRenderer.sprite = stuckAxeSprite.GetSprite();
                    }
                    else
                    {
                        Position = (Vector2)Owner.Position + (dir * 0.8f);
                        Color = Color.red;
                        MyRenderer.sprite = staticAxeSprite.GetSprite();
                    }
                }
            }
        }

        public bool CanThrow => MyRenderer.color.b > 0.5f;
        public void Throw(Vector2 pos, float angle)
        {
            thrownAngle = angle;
            state = 1;
            Position = pos;
            ZOrder = ZOption.Just;
            CanSeeInShadow = true;
            MyRenderer.sprite = thrownAxeSprite.GetSprite();
            thrownTime = NebulaGameManager.Instance!.CurrentTime;
            MyRenderer.color = Color.white;
        }

        static RaiderAxe()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new RaiderAxe(Helpers.GetPlayer((byte)args[0])!));
            NebulaSyncObject.RegisterInstantiater(MyLocalFakeTag, (args) => new RaiderAxe(Helpers.GetPlayer((byte)args[0])!, true));
            NebulaSyncObject.RegisterInstantiater(MyGlobalFakeTag, (args) => new RaiderAxe(Helpers.GetPlayer((byte)args[0])!, false, new(args[1], args[2])));
        }
        
    }

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButton? equipButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AxeButton.png", 115f);
        
        public RaiderAxe? MyAxe = null;
        bool IPlayerAbility.HideKillButton => !(equipButton?.IsBroken ?? false);

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                new GuideLineAbility(MyPlayer, () => MyAxe != null).Register(this);

                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("raider.another1");

                equipButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "raider.equip",
                    0f, "equip", buttonSprite).SetAsUsurpableButton(this);
                equipButton.OnClick = (button) =>
                {
                    if (MyAxe == null)
                    {
                        NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.RaiderEquippingAction);
                        equipButton.SetLabel("unequip");
                    }
                    else
                    {
                        equipButton.SetLabel("equip");
                    }
                    if (MyAxe == null) EquipAxe(); else UnequipAxe();
                };
                equipButton.OnBroken = (button) =>
                {
                    if (MyAxe != null)
                    {
                        equipButton.SetLabel("equip");
                        UnequipAxe();
                    }
                    Snatcher.RewindKillCooldown();
                };
                equipButton.SetLabel("equip");

                var killButton = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: true)
                    .BindKey(Virial.Compat.VirtualKeyInput.Kill, "raider.kill")
                    .SetLabel("throw").SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor)
                    .SetAsMouseClickButton().SetAsUsurpableButton(this);
                killButton.Availability = (button) => MyAxe != null && MyPlayer.CanMove && MyAxe.CanThrow;
                killButton.Visibility = (button) => !MyPlayer.IsDead && !equipButton.IsBroken;
                killButton.OnClick = (button) =>
                {
                    if (MyAxe != null)
                    {
                        NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.RaiderThrowingAction);
                        RpcThrow.Invoke((MyAxe!.ObjectId, MyAxe!.Position, MyPlayer.Unbox().MouseAngle));
                        NebulaAsset.PlaySE(NebulaAudioClip.ThrowAxe, true);
                        StatsThrown.Progress();
                    }
                    MyAxe = null;

                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    equipButton.SetLabel("equip");

                    acTokenAnother.Value.triggered = true;
                };
                killButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, ThrowCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown)).SetAsKillCoolTimer().Start();
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            UnequipAxe();
            equipButton?.SetLabel("equip");
        }

        [Local]
        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (MyAxe != null) UnequipAxe();
            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        [OnlyMyPlayer]
        [Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if(ev.Dead.PlayerState == PlayerState.Beaten) acTokenCommon ??= new("raider.common1");
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }

        void EquipAxe()
        {
            MyAxe = (NebulaSyncObject.RpcInstantiate(RaiderAxe.MyTag, [(float)PlayerControl.LocalPlayer.PlayerId]).SyncObject as RaiderAxe);
        }

        void UnequipAxe()
        {
            if(MyAxe != null) NebulaSyncObject.RpcDestroy(MyAxe.ObjectId);
            MyAxe = null;
        }

        void IGameOperator.OnReleased()
        {
            UnequipAxe();
        }
    }

    static RemoteProcess<(int objectId, Vector2 pos, float angle)> RpcThrow = new(
        "ThrowAxe",
        (message,_) => {
            var axe = NebulaSyncObject.GetObject<RaiderAxe>(message.objectId);
            axe?.Throw(message.pos, message.angle);
        }
        );

    private static int RaiderIgnoreLayerMask = 1 << LayerExpansion.GetRaiderColliderLayer();
    public static bool OverlapAxeIgnoreArea(Vector2 pos) => Physics2D.OverlapPoint(pos, RaiderIgnoreLayerMask);
    
}
