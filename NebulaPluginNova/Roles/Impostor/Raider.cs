using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Raider : DefinedRoleTemplate, DefinedRole
{
    static public Raider MyRole = new Raider();
    private Raider() : base("raider", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [ThrowCoolDownOption, AxeSizeOption, AxeSpeedOption,CanKillImpostorOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private IRelativeCoolDownConfiguration ThrowCoolDownOption = NebulaAPI.Configurations.KillConfiguration("role.raider.throwCoolDown", CoolDownType.Immediate, (10f, 60f, 2.5f), 20f, (-40f, 40f, 2.5f), -10f, (0.125f, 2f, 0.125f), 1f);
    static private FloatConfiguration AxeSizeOption = NebulaAPI.Configurations.Configuration("role.raider.axeSize", (0.25f, 4f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration AxeSpeedOption = NebulaAPI.Configurations.Configuration("role.raider.axeSpeed", (0.5f, 4f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private BoolConfiguration CanKillImpostorOption = NebulaAPI.Configurations.Configuration("role.raider.canKillImpostor", false);

    [NebulaPreLoad]
    public class RaiderAxe : NebulaSyncStandardObject, IGameOperator
    {
        public static string MyTag = "RaiderAxe";
        
        private static SpriteLoader staticAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxe.png", 150f);
        private static SpriteLoader thrownAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeThrown.png", 150f);
        private static SpriteLoader stuckAxeSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeCrashed.png", 150f);

        private float thrownAngle = 0f;
        private int state = 0;
        private float speed = AxeSpeedOption;
        private int killed = 0;
        private float thrownTime = 0f;
        AchievementToken<int>? acTokenChallenge = null;

        public RaiderAxe(PlayerControl owner) : base(owner.GetTruePosition(),ZOption.Front,false,staticAxeSprite.GetSprite())
        {
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
                    var size = AxeSizeOption;
                    if (!MeetingHud.Instance)
                    {
                        foreach (var p in PlayerControl.AllPlayerControls)
                        {
                            if (p.Data.IsDead || p.AmOwner) continue;
                            if (!CanKillImpostorOption && p.Data.Role.IsImpostor) continue;

                            if (!Helpers.AnyNonTriggersBetween(p.GetTruePosition(),pos,out var diff,Constants.ShipAndAllObjectsMask) && diff.magnitude < size * 0.4f)
                            {
                                //不可視なプレイヤーは無視
                                if (p.GetModInfo()?.Unbox().IsInvisible ?? false) continue;

                                if (PlayerControl.LocalPlayer.ModKill(p, false, PlayerState.Beaten, EventDetail.Kill) == KillResult.Kill)
                                {
                                    killed |= 1 << p.PlayerId;

                                    if (killed >= 3)
                                    {
                                        acTokenChallenge ??= new("raider.challenge", killed, (val, _) =>
                                        /*人数都合でゲームが終了している*/ NebulaGameManager.Instance!.EndState!.EndReason == GameEndReason.Situation &&
                                        /*勝利している*/ NebulaGameManager.Instance.EndState!.Winners.Test(Owner) &&
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

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? equipButton = null;
        private ModAbilityButton? killButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AxeButton.png", 115f);
        
        public RaiderAxe? MyAxe = null;
        public override bool HasVanillaKillButton => false;
        public Instance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;

        void RuntimeAssignable.OnActivated()
        {
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
                killButton.CoolDownTimer = Bind(new Timer(ThrowCoolDownOption.CoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabel("throw");
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
                killButton.SetCanUseByMouseClick();
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
            MyAxe = (NebulaSyncObject.RpcInstantiate(RaiderAxe.MyTag, new float[] { (float)PlayerControl.LocalPlayer.PlayerId }) as RaiderAxe);
        }

        void UnequipAxe()
        {
            if(MyAxe != null) NebulaSyncObject.RpcDestroy(MyAxe.ObjectId);
            MyAxe = null;
        }

        void RuntimeAssignable.OnInactivated()
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
