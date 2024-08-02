﻿using Nebula.Roles.Abilities;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Necromancer : DefinedRoleTemplate, DefinedRole
{
    private Necromancer() : base("necromancer", new(108,50,160), RoleCategory.CrewmateRole, Crewmate.MyTeam, [ReviveCoolDownOption, ReviveDurationOption, DetectedRangeOption, ReviveMinRangeOption, ReviveMaxRangeOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);

        MetaAbility.RegisterCircle(new("role.necromancer.reviveRange", () => DetectedRangeOption, () => null, UnityColor));
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration ReviveCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.necromancer.reviveCoolDown", (5f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration ReviveDurationOption = NebulaAPI.Configurations.Configuration("options.role.necromancer.reviveDuration", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DetectedRangeOption = NebulaAPI.Configurations.Configuration("options.role.necromancer.detectedRange", (2.5f, 30f, 2.5f), 7.5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration ReviveMinRangeOption = NebulaAPI.Configurations.Configuration("options.role.necromancer.reviveMinRange", (0f, 12.5f, 2.5f), 7.5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration ReviveMaxRangeOption = NebulaAPI.Configurations.Configuration("options.role.necromancer.reviveMaxRange", (10f, 30f, 2.5f), 17.5f, FloatConfigurationDecorator.Ratio);

    static public Necromancer MyRole = new Necromancer();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private Scripts.Draggable? draggable = null;
        private ModAbilityButton? reviveButton = null;
        private Arrow? myArrow;
        private TMPro.TextMeshPro message = null!;
        private SpriteRenderer? fullScreen;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ReviveButton.png", 115f);

        private Dictionary<byte, SystemTypes> resurrectionRoom = new();

        public Instance(GamePlayer player) : base(player)
        {
            draggable = Bind(new Scripts.Draggable());

            if (AmOwner)
            {
                fullScreen = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
                Bind(new GameObjectBinding(fullScreen.gameObject));
                fullScreen.color = MyRole.UnityColor.AlphaMultiplied(0f);
                fullScreen.gameObject.SetActive(true);
            }
        }

        [Local]
        void LocalUpdate(GameUpdateEvent ev)
        {
            bool flag = MyPlayer.HoldingAnyDeadBody;

            message.gameObject.SetActive(flag);
            if (flag) message.color = MyRole.UnityColor.AlphaMultiplied(MathF.Sin(Time.time * 2.4f) * 0.2f + 0.8f);

            if (fullScreen)
            {
                bool detected = false;
                var myPos = MyPlayer.VanillaPlayer.GetTruePosition();
                float maxDis = DetectedRangeOption;

                byte currentHolding = MyPlayer.HoldingDeadBody?.PlayerId ?? byte.MaxValue;
                foreach (var deadbody in Helpers.AllDeadBodies())
                {
                    if (currentHolding == deadbody.ParentId) continue;
                    if ((deadbody.TruePosition - myPos).magnitude > maxDis) continue;

                    detected = true;
                    break;
                }

                float a = fullScreen!.color.a;
                a += ((detected ? 0.32f : 0) - a) * Time.deltaTime * 1.8f;
                fullScreen!.color = MyRole.UnityColor.AlphaMultiplied(a);
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            draggable?.OnActivated(this);

            if (AmOwner)
            {
                message = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, HudManager.Instance.transform);
                new TextAttributeOld(TextAttributeOld.NormalAttr) { Size = new Vector2(5f, 0.9f) }.EditFontSize(2.7f, 2.7f, 2.7f).Reflect(message);
                message.transform.localPosition = new Vector3(0, -1.2f, -4f);
                Bind(new GameObjectBinding(message.gameObject));

                SystemTypes? currentTargetRoom = null;

                bool canReviveHere()
                {
                    return currentTargetRoom.HasValue && MyPlayer.VanillaPlayer.moveable && MyPlayer.HoldingAnyDeadBody && ShipStatus.Instance.FastRooms[currentTargetRoom.Value].roomArea.OverlapPoint(MyPlayer.TruePosition);
                }

                myArrow = Bind(new Arrow());
                myArrow.IsActive = false;
                myArrow.SetColor(MyRole.UnityColor);

                GameOperatorManager.Instance?.Register<GameUpdateEvent>((ev) => {
                    if (MyPlayer.HoldingAnyDeadBody && currentTargetRoom.HasValue && !canReviveHere())
                    {
                        myArrow.IsActive = true;
                        myArrow.TargetPos = ShipStatus.Instance.FastRooms[currentTargetRoom.Value].roomArea.ClosestPoint(MyPlayer.VanillaPlayer.transform.position);
                    }
                    else
                    {
                        myArrow.IsActive = false;
                    }
                }, this);

                draggable!.OnHoldingDeadBody = (deadBody) =>
                {
                    if (!resurrectionRoom.ContainsKey(deadBody.ParentId))
                    {
                        //復活部屋を計算
                        List<Tuple<float, PlainShipRoom>> cand = new();
                        foreach (var entry in ShipStatus.Instance.FastRooms)
                        {
                            if (entry.Key == SystemTypes.Ventilation) continue;
                            
                            float d = Physics2D.ClosestPoint_Collider(MyPlayer.VanillaPlayer.transform.position, entry.Value.roomArea).magnitude;
                            if (d < ReviveMinRangeOption) continue;

                            cand.Add(new(d, entry.Value));
                        }

                        //近い順にソートし、遠すぎる部屋は候補から外す 少なくとも1部屋は候補に入るようにする
                        cand.Sort((c1, c2) => Math.Sign(c1.Item1 - c2.Item1));
                        int lastIndex = cand.FindIndex((tuple) => tuple.Item1 > ReviveMaxRangeOption);
                        if (lastIndex == -1) lastIndex = cand.Count;
                        if (lastIndex == 0) lastIndex = 1;

                        resurrectionRoom[deadBody.ParentId] = cand[System.Random.Shared.Next(lastIndex)].Item2.RoomId;
                    }

                    currentTargetRoom = resurrectionRoom[deadBody.ParentId];
                    //myArrow.TargetPos = ShipStatus.Instance.FastRooms[currentTargetRoom.Value].roomArea.bounds.center;
                    message.text = Language.Translate("role.necromancer.phantomMessage").Replace("%ROOM%", AmongUsUtil.ToDisplayString(currentTargetRoom.Value));
                };

                AchievementToken<(bool cleared, int bitFlag)> acTokenChalenge = new("necromancer.challenge", (false, 0), (val, _) => val.cleared);

                reviveButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                reviveButton.SetSprite(buttonSprite.GetSprite());
                reviveButton.Availability = (button) => MyPlayer.VanillaPlayer.CanMove && MyPlayer.HoldingAnyDeadBody && canReviveHere();
                reviveButton.Visibility = (button) => !MyPlayer.IsDead;
                reviveButton.OnClick = (button) => button.ActivateEffect();
                reviveButton.OnEffectEnd = (button) =>
                {
                    if (!button.EffectTimer!.IsProgressing)
                    {
                        var currentHolding = MyPlayer.HoldingDeadBody!;

                        new StaticAchievementToken("necromancer.common1");
                        if (!currentHolding.IsCrewmate) new StaticAchievementToken("necromancer.another1");
                        acTokenChalenge.Value.cleared |= (acTokenChalenge.Value.bitFlag & (1 << currentHolding.PlayerId)) != 0;
                        acTokenChalenge.Value.bitFlag |= 1 << currentHolding.PlayerId;

                        currentHolding.Revive(MyPlayer, new(MyPlayer.VanillaPlayer.transform.position), true);
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
                reviveButton.PlayFlash = () => MyPlayer.HoldingAnyDeadBody && canReviveHere() && !reviveButton.EffectActive;
                reviveButton.CoolDownTimer = Bind(new Timer(ReviveCoolDownOption).SetAsAbilityCoolDown().Start());
                reviveButton.EffectTimer = Bind(new Timer(ReviveDurationOption));
                reviveButton.SetLabel("revive");
            }
        }

        [OnlyMyPlayer, Local]
        void ReleaseDeadBodyOnNecromancerDead(PlayerDieEvent ev) => draggable?.OnDead(this);


        void RuntimeAssignable.OnInactivated()
        {
            draggable?.OnInactivated(this);
        }

        [Local]
        void SearchResurrectionRoomOnPlayerDead(PlayerDieEvent ev) => resurrectionRoom?.Remove(ev.Player.PlayerId);
    }
}

