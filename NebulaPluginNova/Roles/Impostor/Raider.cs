using Nebula.Configuration;
using Nebula.Player;
using Sentry.Unity.NativeUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;
using static Nebula.Roles.Crewmate.Phosphorus;
using static UnityEngine.UI.GridLayoutGroup;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Raider : ConfigurableStandardRole
{
    static public Raider MyRole = new Raider();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "raider";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private KillCoolDownConfiguration ThrowCoolDownOption = null!;
    private NebulaConfiguration AxeSizeOption = null!;
    private NebulaConfiguration AxeSpeedOption = null!;
    private NebulaConfiguration CanKillImpostorOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagFunny, ConfigurationHolder.TagDifficult);

        ThrowCoolDownOption = new(RoleConfig, "throwCoolDown", KillCoolDownConfiguration.KillCoolDownType.Immediate, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 20f, -10f, 1f);
        AxeSizeOption = new(RoleConfig, "axeSize", null, 0.25f, 4f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
        AxeSpeedOption = new(RoleConfig, "axeSpeed", null, 0.5f, 4f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
        CanKillImpostorOption = new(RoleConfig, "canKillImpostor", null, false, false);
    }

    [NebulaPreLoad]
    public class RaiderAxe : NebulaSyncStandardObject, IGameEntity
    {
        public static string MyTag = "RaiderAxe";
        
        private static SpriteLoader staticAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxe.png", 150f);
        private static SpriteLoader thrownAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeThrown.png", 150f);
        private static SpriteLoader stuckAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeCrashed.png", 150f);

        private float thrownAngle = 0f;
        private int state = 0;
        private float speed = MyRole.AxeSpeedOption.GetFloat();
        private int killed = 0;
        private float thrownTime = 0f;
        AchievementToken<int>? acTokenChallenge = null;

        public RaiderAxe(PlayerControl owner) : base(owner.GetTruePosition(),ZOption.Front,false,staticAxeSprite.GetSprite())
        {
        }

        void IGameEntity.HudUpdate()
        {
            if (state == 0)
            {
                if (AmOwner) Owner.Unbox().RequireUpdateMouseAngle();
                MyRenderer.transform.localEulerAngles = new Vector3(0, 0, Owner.MouseAngle * 180f / Mathf.PI);
                var pos = Owner.MyCon.transform.position + new Vector3(Mathf.Cos(Owner.MouseAngle), Mathf.Sin(Owner.MouseAngle), -1f) * 0.67f;
                var diff = (pos - MyRenderer.transform.position) * Time.deltaTime * 7.5f;
                Position += (Vector2)diff;
                MyRenderer.flipY = Mathf.Cos(Owner.MouseAngle) < 0f;

                if (AmOwner)
                {
                    var vec = MyRenderer.transform.position - PlayerControl.LocalPlayer.transform.position;
                    if(PhysicsHelpers.AnyNonTriggersBetween(PlayerControl.LocalPlayer.GetTruePosition(),(Vector2)vec.normalized,((Vector2)vec).magnitude, Constants.ShipAndAllObjectsMask))
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
                    var size = MyRole.AxeSizeOption.GetFloat();
                    if (!MeetingHud.Instance)
                    {
                        foreach (var p in PlayerControl.AllPlayerControls)
                        {
                            if (p.Data.IsDead || p.AmOwner) continue;
                            if (!MyRole.CanKillImpostorOption && p.Data.Role.IsImpostor) continue;

                            if (!Helpers.AnyNonTriggersBetween(p.GetTruePosition(),pos,out var diff,Constants.ShipAndAllObjectsMask) && diff.magnitude < size * 0.4f)
                            {
                                //不可視なプレイヤーは無視
                                if (p.GetModInfo()?.IsInvisible ?? false) continue;

                                if (PlayerControl.LocalPlayer.ModKill(p, false, PlayerState.Beaten, EventDetail.Kill) == KillResult.Kill)
                                {
                                    killed |= 1 << p.PlayerId;

                                    if (killed >= 3)
                                    {
                                        acTokenChallenge ??= new("raider.challenge", killed, (val, _) =>
                                        /*人数都合でゲームが終了している*/ NebulaGameManager.Instance!.EndState!.EndReason == GameEndReason.Situation &&
                                        /*勝利している*/ NebulaGameManager.Instance.EndState!.CheckWin(Owner.PlayerId) &&
                                        /*最後の死亡者がこの斧によってキルされている*/ (killed & (1 << (NebulaGameManager.Instance.GetLastDead?.PlayerId ?? -1))) != 0
                                        );
                                        acTokenChallenge.Value = killed;
                                    }
                                }
                            }
                        }
                    }
                }

                if (NebulaPhysicsHelpers.AnyNonTriggersBetween(MyRenderer.transform.position, vec, speed * 4f * Time.deltaTime, Constants.ShipAndAllObjectsMask, out var d))
                {
                    state = 2;
                    MyRenderer.sprite = stuckAxeSprite.GetSprite();
                    MyRenderer.transform.eulerAngles = new Vector3(0f, 0f, thrownAngle * 180f / Mathf.PI);

                    if (AmOwner && killed == 0)
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Missed, NebulaGameManager.Instance.CurrentTime - thrownTime, PlayerControl.LocalPlayer, 0);
                }
                else
                {
                    MyRenderer.transform.localEulerAngles += new Vector3(0f, 0f, MyRenderer.flipY ? Time.deltaTime * 2000f : Time.deltaTime * -2000f);
                }

                Position += vec * d;
            }
            else if (state == 2) { }
        }

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

        public static void Load()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new RaiderAxe(Helpers.GetPlayer((byte)args[0])!));
        }
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? equipButton = null;
        private ModAbilityButton? killButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AxeButton.png", 115f);
        public override AbstractRole Role => MyRole;
        public RaiderAxe? MyAxe = null;
        public override bool HasVanillaKillButton => false;
        public Instance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;
        
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("raider.another1");

                equipButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                equipButton.SetSprite(buttonSprite.GetSprite());
                equipButton.Availability = (button) => MyPlayer.VanillaPlayer.CanMove;
                equipButton.Visibility = (button) => !MyPlayer.IsDead;
                equipButton.OnClick = (button) =>
                {
                    if (MyAxe == null)
                        equipButton.SetLabel("unequip");
                    else
                        equipButton.SetLabel("equip");

                    if (MyAxe == null) EquipAxe(); else UnequipAxe();
                };
                equipButton.SetLabel("equip");

                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => MyAxe != null && MyPlayer.CanMove && MyAxe.MyRenderer.color.b > 0.5f;
                killButton.Visibility = (button) => !MyPlayer.IsDead;
                killButton.OnClick = (button) =>
                {
                    if (MyAxe != null)
                    {
                        RpcThrow.Invoke((MyAxe!.ObjectId, MyAxe!.Position, MyPlayer.Unbox().MouseAngle));
                        NebulaAsset.PlaySE(NebulaAudioClip.ThrowAxe);
                    }
                    MyAxe = null;
                    button.StartCoolDown();
                    equipButton.SetLabel("equip");

                    acTokenAnother.Value.triggered = true;
                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.ThrowCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabel("throw");
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
                killButton.SetCanUseByMouseClick();
            }
        }

        void IGameEntity.OnMeetingStart()
        {
            UnequipAxe();
            equipButton?.SetLabel("equip");
        }

        void IGamePlayerEntity.OnDead()
        {
            if (AmOwner && MyAxe != null) UnequipAxe();

            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        void IGamePlayerEntity.OnKillPlayer(GamePlayer target)
        {
            if(AmOwner && target.PlayerState == PlayerState.Beaten)
                acTokenCommon ??= new("raider.common1");
            
        }

        void IGameEntity.OnMeetingEnd(GamePlayer[] exiled)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }

        void EquipAxe()
        {
            MyAxe = (NebulaSyncObject.RpcInstantiate(RaiderAxe.MyTag, new float[] { (float)PlayerControl.LocalPlayer.PlayerId }) as RaiderAxe);
        }

        void UnequipAxe()
        {
            if(MyAxe != null) NebulaSyncObject.RpcDestroy(MyAxe.ObjectId);
            MyAxe = null;
        }

        protected override void OnInactivated()
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
}
