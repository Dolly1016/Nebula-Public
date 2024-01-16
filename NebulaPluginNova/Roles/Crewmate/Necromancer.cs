using Nebula.Configuration;
using UnityEngine.AI;
using Virial.Assignable;

namespace Nebula.Roles.Crewmate;

public class Necromancer : ConfigurableStandardRole
{
    static public Necromancer MyRole = new Necromancer();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "necromancer";
    public override Color RoleColor => new Color(108f / 255f, 50f / 255f, 160f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration ReviveCoolDownOption = null!;
    private NebulaConfiguration ReviveDurationOption = null!;
    private NebulaConfiguration DetectedRangeOption = null!;
    private NebulaConfiguration ReviveMinRangeOption = null!;
    private NebulaConfiguration ReviveMaxRangeOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        ReviveCoolDownOption = new NebulaConfiguration(RoleConfig, "reviveCoolDown", null, 5f, 60f, 5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        ReviveDurationOption = new NebulaConfiguration(RoleConfig, "reviveDuration", null, 0.5f, 10f, 0.5f, 3f, 3f) { Decorator = NebulaConfiguration.SecDecorator };
        DetectedRangeOption = new NebulaConfiguration(RoleConfig, "detectedRange", null, 2.5f, 30f, 2.5f, 7.5f, 7.5f) { Decorator = NebulaConfiguration.OddsDecorator };
        ReviveMinRangeOption = new NebulaConfiguration(RoleConfig, "reviveMinRange", null, 0f, 12.5f, 2.5f, 7.5f, 7.5f) { Decorator = NebulaConfiguration.OddsDecorator };
        ReviveMaxRangeOption = new NebulaConfiguration(RoleConfig, "reviveMaxRange", null, 10f, 30f, 2.5f, 17.5f, 17.5f) { Decorator = NebulaConfiguration.OddsDecorator };
    }

    public class Instance : Crewmate.Instance
    {
        public override AbstractRole Role => MyRole;
        private Scripts.Draggable? draggable = null;
        private ModAbilityButton? reviveButton = null;
        private Arrow? myArrow;
        private TMPro.TextMeshPro message = null!;
        private SpriteRenderer? fullScreen;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ReviveButton.png", 115f);

        private Dictionary<byte, SystemTypes> resurrectionRoom = new();

        public Instance(PlayerModInfo player) : base(player)
        {
            draggable = Bind(new Scripts.Draggable());

            if (AmOwner)
            {
                fullScreen = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
                Bind(new GameObjectBinding(fullScreen.gameObject));
                fullScreen.color = MyRole.RoleColor.AlphaMultiplied(0f);
                fullScreen.gameObject.SetActive(true);
            }
        }

        public override void LocalUpdate()
        {
            bool flag = MyPlayer.HoldingDeadBodyId.HasValue;
            if (myArrow != null) myArrow.IsActive = flag;
            message.gameObject.SetActive(flag);
            if (flag) message.color = MyRole.RoleColor.AlphaMultiplied(MathF.Sin(Time.time * 2.4f) * 0.2f + 0.8f);

            if (fullScreen)
            {
                bool detected = false;
                var myPos = MyPlayer.MyControl.GetTruePosition();
                float maxDis = MyRole.DetectedRangeOption.GetFloat();

                foreach (var deadbody in Helpers.AllDeadBodies())
                {
                    if (MyPlayer.HoldingDeadBodyId == deadbody.ParentId) continue;
                    if ((deadbody.TruePosition - myPos).magnitude > maxDis) continue;

                    detected = true;
                    break;
                }

                float a = fullScreen!.color.a;
                a += ((detected ? 0.32f : 0) - a) * Time.deltaTime * 1.8f;
                fullScreen!.color = MyRole.RoleColor.AlphaMultiplied(a);
            }
        }

        public override void OnActivated()
        {
            draggable?.OnActivated(this);

            if (AmOwner)
            {
                message = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, HudManager.Instance.transform);
                new TextAttribute(TextAttribute.NormalAttr) { Size = new Vector2(5f, 0.9f) }.EditFontSize(2.7f, 2.7f, 2.7f).Reflect(message);
                message.transform.localPosition = new Vector3(0, -1.2f, -4f);
                Bind(new GameObjectBinding(message.gameObject));

                SystemTypes? currentTargetRoom = null;

                bool canReviveHere()
                {
                    return !(!currentTargetRoom.HasValue || !MyPlayer.HoldingDeadBodyId.HasValue || !ShipStatus.Instance.FastRooms[currentTargetRoom.Value].roomArea.OverlapPoint(MyPlayer.MyControl.GetTruePosition()));
                }

                myArrow = Bind(new Arrow());
                myArrow.IsActive = false;
                myArrow.SetColor(MyRole.RoleColor);

                draggable!.OnHoldingDeadBody = (deadBody) =>
                {
                    if (!resurrectionRoom.ContainsKey(deadBody.ParentId))
                    {
                        //復活部屋を計算
                        List<Tuple<float, PlainShipRoom>> cand = new();
                        foreach (var entry in ShipStatus.Instance.FastRooms)
                        {
                            if (entry.Key == SystemTypes.Ventilation) continue;

                            float d = entry.Value.roomArea.Distance(MyPlayer.MyControl.Collider).distance;
                            if (d < MyRole.ReviveMinRangeOption.GetFloat()) continue;

                            cand.Add(new(d, entry.Value));
                        }

                        //近い順にソートし、遠すぎる部屋は候補から外す 少なくとも1部屋は候補に入るようにする
                        cand.Sort((c1, c2) => Math.Sign(c1.Item1 - c2.Item1));
                        int lastIndex = cand.FindIndex((tuple) => tuple.Item1 > MyRole.ReviveMaxRangeOption.GetFloat());
                        if (lastIndex == -1) lastIndex = cand.Count;
                        if (lastIndex == 0) lastIndex = 1;

                        resurrectionRoom[deadBody.ParentId] = cand[System.Random.Shared.Next(lastIndex)].Item2.RoomId;
                    }

                    currentTargetRoom = resurrectionRoom[deadBody.ParentId];
                    myArrow.TargetPos = ShipStatus.Instance.FastRooms[currentTargetRoom.Value].roomArea.transform.position;
                    message.text = Language.Translate("role.necromancer.phantomMessage").Replace("%ROOM%", AmongUsUtil.ToDisplayString(currentTargetRoom.Value));
                };


                StaticAchievementToken? acTokenCommon = null;
                AchievementToken<(bool cleared, int bitFlag)> acTokenChalenge = new("necromancer.challenge", (false, 0), (val, _) => val.cleared);

                reviveButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                reviveButton.SetSprite(buttonSprite.GetSprite());
                reviveButton.Availability = (button) => MyPlayer.MyControl.CanMove && MyPlayer.HoldingDeadBodyId.HasValue && canReviveHere();
                reviveButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                reviveButton.OnClick = (button) => button.ActivateEffect();
                reviveButton.OnEffectEnd = (button) =>
                {
                    if (!button.EffectTimer!.IsInProcess)
                    {
                        acTokenCommon ??= new("necromancer.common1");
                        acTokenChalenge.Value.cleared |= (acTokenChalenge.Value.bitFlag & (1 << MyPlayer.HoldingDeadBodyId!.Value)) != 0;
                        acTokenChalenge.Value.bitFlag |= 1 << MyPlayer.HoldingDeadBodyId!.Value;

                        Helpers.GetPlayer(MyPlayer.HoldingDeadBodyId)?.ModRevive(MyPlayer.MyControl, MyPlayer.MyControl.transform.position, true);
                        button.CoolDownTimer!.Start();
                    }
                };
                reviveButton.OnMeeting = (button) =>
                {
                    reviveButton.InactivateEffect();
                };
                reviveButton.OnUpdate = (button) => {
                    if (!button.EffectActive) return;
                    if (!canReviveHere()) button.InactivateEffect();
                };
                reviveButton.CoolDownTimer = Bind(new Timer(MyRole.ReviveCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                reviveButton.EffectTimer = Bind(new Timer(MyRole.ReviveDurationOption.GetFloat()));
                reviveButton.SetLabel("revive");
            }
        }

        public override void OnDead()
        {
            draggable?.OnDead(this);
        }

        protected override void OnInactivated()
        {
            draggable?.OnInactivated(this);
        }

        public override void OnAnyoneDeadLocal(PlayerControl dead)
        {
            resurrectionRoom?.Remove(dead.PlayerId);
        }
    }
}

