using AmongUs.GameOptions;
using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using Nebula.Utilities;
using Nebula.Behavior;
using PowerTools;
using Virial.Events.Game;
using Nebula.Roles.Abilities;
using UnityEngine;
using TMPro;
using Virial.DI;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MS.Internal.Xml.XPath;
using Virial.Components;

namespace Nebula.Roles.Impostor;

public class Bubblegun : DefinedSingleAbilityRoleTemplate<Bubblegun.Ability>, DefinedRole
{
    private Bubblegun() : base("bubblegun", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [BubbleCoolDownOption, bubbleSizeOption, bubbleSpeedOption, bubbleDurationOption, maxBubblesOption, eraseBubblesOnMeeting, canKillImpostorOption, bubblePopWhenHitWallOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Bubblegun.png");
    }

    static private readonly IRelativeCoolDownConfiguration BubbleCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.bubblegun.bubbleCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 30f, (-30f, 30f, 2.5f), 0f, (0.5f, 5f, 0.125f), 1.5f);
    static private readonly FloatConfiguration bubbleSizeOption = NebulaAPI.Configurations.Configuration("options.role.bubblegun.bubbleSize", (0.5f, 2f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration bubbleSpeedOption = NebulaAPI.Configurations.Configuration("options.role.bubblegun.bubbleSpeed", (0.5f, 2f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration bubbleDurationOption = NebulaAPI.Configurations.Configuration("options.role.bubblegun.bubbleDuration", (2.5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration maxBubblesOption = NebulaAPI.Configurations.Configuration("options.role.bubblegun.maxBubbles", (int[])[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 50, 99], 2);
    static private readonly BoolConfiguration eraseBubblesOnMeeting = NebulaAPI.Configurations.Configuration("options.role.bubblegun.eraseBubblesOnMeeting", true);
    static private readonly BoolConfiguration canKillImpostorOption = NebulaAPI.Configurations.Configuration("options.role.bubblegun.canKillImpostor", false);
    static private readonly BoolConfiguration bubblePopWhenHitWallOption = NebulaAPI.Configurations.Configuration("options.role.bubblegun.bubblePopWhenHitWall", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, maxBubblesOption));
    bool DefinedRole.IsJackalizable => true;

    static public readonly Bubblegun MyRole = new();
    static private readonly GameStatsEntry StatsBubble = NebulaAPI.CreateStatsEntry("stats.bubblegun.bubble", GameStatsCategory.Roles, MyRole);
    static float BubbleDistance => 1.2f;

    public class BubblegunBubble : SimpleLifespan, IGameOperator
    {
        private SpriteRenderer renderer;
        private float degreeAngle;
        private int index;
        private GamePlayer myPlayer;
        
        public BubblegunBubble(GamePlayer player, Vector2 pos, float angle, int index)
        {
            this.index = index;
            myPlayer = player;
            renderer = UnityHelper.CreateObject<SpriteRenderer>("Bubble", null, pos, LayerExpansion.GetObjectsLayer());
            renderer.transform.SetWorldZ(-1f);
            renderer.sprite = bubbleSprite.GetSprite(8);
            renderer.transform.localScale = new(bubbleSizeOption / 2f, bubbleSizeOption / 2f, 1f);
            degreeAngle = angle * 180f / Mathf.PI;
        }

        const float BubbleGraphicInterval = 0.1f;
        float updateInterval = BubbleGraphicInterval;
        int spriteIndex = 0;
        bool used = false;
        
        bool intro = true;
        bool moving = false;

        float time = 0;
        void OnUpdate(GameUpdateEvent ev)
        {
            updateInterval -= Time.deltaTime;

            if(updateInterval < 0f)
            {
                //コマ送りのための動き
                if (!moving) renderer.gameObject.transform.position += new Vector2(0.4f * bubbleSizeOption, 0f).Rotate(degreeAngle).AsVector3(0f);

                if (intro)
                {
                    intro = false;
                    renderer.sprite = bubbleSprite.GetSprite(9);
                    spriteIndex = 3;
                }
                else
                {
                    moving = true;
                    spriteIndex = (spriteIndex + 1) % 4;
                    renderer.sprite = bubbleSprite.GetSprite(spriteIndex);
                    updateInterval = BubbleGraphicInterval;
                }
            }

            if (moving) renderer.gameObject.transform.position += (new Vector2(1f, 0f).Rotate(degreeAngle) * 0.8f * bubbleSpeedOption * Time.deltaTime).AsVector3(0f);


            if (!MeetingHud.Instance && moving)
            {
                var localPlayer = GamePlayer.LocalPlayer;
                if (!used && !myPlayer.AmOwner && ObjectTrackers.StandardPredicateIgnoreOwner.Invoke(localPlayer) && (canKillImpostorOption || myPlayer.CanKill(localPlayer)))
                {
                    if (localPlayer.Position.ToUnityVector().Distance(renderer.transform.position) < (1.7f / 2f * bubbleSizeOption))
                    {
                        RpcBubbleKill.Invoke((myPlayer, localPlayer, localPlayer.Position, index));
                        used = true;
                    }
                }
            }

            time += Time.deltaTime;
            if (time > bubbleDurationOption) this.Release();

            if (bubblePopWhenHitWallOption && time > 0.8f && 
                Helpers.CircleContainsAnyNonTriggers(renderer.transform.position, 0.3f * bubbleSizeOption, Constants.ShipAndAllObjectsMask) &&
                !Helpers.CircleContainsAnyNonTriggers(renderer.transform.position, 0.3f * bubbleSizeOption, 1 << LayerExpansion.GetRaiderColliderLayer())) this.Release();
        }

        void IGameOperator.OnReleased()
        {
            IEnumerator CoDisappear()
            {
                for (int i = 0; i < 4; i++)
                {
                    renderer.sprite = bubbleSprite.GetSprite(4 + i);
                    yield return Effects.Wait(BubbleGraphicInterval);
                }
                GameObject.Destroy(renderer.gameObject);
            }
            NebulaManager.Instance.StartCoroutine(CoDisappear().WrapToIl2Cpp());
        }

        int killed = 0;
        public void OnKill(GamePlayer dead)
        {
            killed++;

            if (myPlayer.AmOwner)
            {
                if (20f <= degreeAngle && degreeAngle <= 70f && killed == 3) new StaticAchievementToken("bubblegun.challenge");
                if (killed == 1) new StaticAchievementToken("bubblegun.common1");
            }
        }
    }

    public class BubblegunGun : EquipableAbility
    {
        static private IDividedSpriteLoader bubblegunSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Bubblegun.png", 115f, 4, 2).SetPivot(new(0.8f, 0.38f));
        protected override float Size => 0.68f;
        protected override float Distance => 0.7f;
        public BubblegunGun(GamePlayer owner) : base(owner, false, "Bubblegun")
        {
            Renderer.sprite = bubblegunSprite.GetSprite(0);
        }

        float? fireAngle = null;
        IEnumerator CoDisappear()
        {
            for(int i = 1; i < bubblegunSprite.Length; i++)
            {
                Renderer.sprite = bubblegunSprite.GetSprite(i);
                yield return Effects.Wait(0.15f);
            }
            this.Release();
        }

        public void OnFire(float angle)
        {
            if (fireAngle.HasValue) return;

            fireAngle = angle;
            NebulaManager.Instance.StartCoroutine(CoDisappear().WrapToIl2Cpp());
        }
    }

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BubblegunButton.png", 115f);
        static private Image killButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BubblegunKillButton.png", 115f);

        public BubblegunGun? MyGun { get; private set; } = null;
        public int LeftUses { get; private set; }

        public List<BubblegunBubble> Bubbles { get; private set; } = [];
        public Ability(GamePlayer player, bool isUsurped, int leftUses) : base(player, isUsurped)
        {
            LeftUses = leftUses;
            if (AmOwner)
            {
                var acTokenAnother1 = AchievementTokens.FirstFailedAchievementToken("bubblegun.another1", MyPlayer, this);

                new GuideLineAbility(MyPlayer, () => MyGun != null).Register(this);

                ModAbilityButton equipButton = null!;
                var killButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, true, false, Virial.Compat.VirtualKeyInput.Kill, "bubblegun.kill",
                    0f, "bubble", killButtonSprite, null, _ => MyGun != null)
                    .SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor)
                    .SetAsMouseClickButton().SetAsUsurpableButton(this);
                killButton.OnClick = (button) => {
                    Vector2 pos = (Vector2)MyPlayer.Position + new Vector2(1f, 0f).Rotate(MyPlayer.Unbox().MouseAngle * 180f / Mathf.PI) * (0.7f + 0.1f * bubbleSizeOption);
                    RpcFire.Invoke((MyPlayer.PlayerId, pos, MyPlayer.Unbox().MouseAngle, Bubbles.Count));
                    equipButton.SetLabel("equip");
                    NebulaAsset.PlaySE(Helpers.Prob(50) ? NebulaAudioClip.Bubble1 : NebulaAudioClip.Bubble2, oneshot: true, volume: 1f);

                    LeftUses--;
                    equipButton.UpdateUsesIcon(LeftUses.ToString());

                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();

                    acTokenAnother1.Value.triggered = true;
                    StatsBubble.Progress();
                };

                equipButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "bubblegun.equip",
                    BubbleCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "equip", buttonSprite,
                    null, _ => LeftUses > 0).ShowUsesIcon(0, LeftUses.ToString()).SetAsUsurpableButton(this);
                equipButton.OnClick = (button) => {
                    button.SetLabel(MyGun == null ? "unequip" : "equip");
                    RpcEquip.Invoke((MyPlayer.PlayerId, MyGun == null));
                };
                equipButton.OnBroken = (button) =>
                {
                    if (MyGun != null)
                    {
                        button.SetLabel("equip");
                        RpcEquip.Invoke((MyPlayer.PlayerId, false));
                    }
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(equipButton.GetKillButtonLike());

                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                {
                    equipButton.SetLabel("equip");
                    if (MyGun != null) RpcEquip.Invoke((MyPlayer.PlayerId, false));
                    equipButton.StartCoolDown();
                }, equipButton);
                GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
                {
                    if (ev.Murderer.AmOwner) equipButton.StartCoolDown();
                }, this);
            }
        }

        static readonly RemoteProcess<(byte playerId, bool equip)> RpcEquip = new(
        "EquipBubblegun",
        (message, _) =>
        {
            var role = NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Role;
            var ability = role?.GetAbility<Bubblegun.Ability>();
            if (ability != null)
            {
                if (message.equip)
                    ability.EquipBubblegun();
                else
                    ability.UnequipBubblegun();
            }
        }
        );

        static readonly RemoteProcess<(byte playerId, Vector2 pos, float angle, int index)> RpcFire = new(
            "FireBubblegun",
            (message, _) =>
            {
                var role = NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Role;
                var ability = role?.GetAbility<Bubblegun.Ability>();
                ability?.Bubbles.Add(ability.FireBubblegun(message.pos, message.angle, message.index));
            }
            );

        void EquipBubblegun()
        {
            MyGun = new BubblegunGun(MyPlayer).Register(this);
        }

        void UnequipBubblegun()
        {
            MyGun?.Release();
            MyGun = null;
        }

        BubblegunBubble FireBubblegun(Vector2 pos, float angle, int index)
        {
            MyGun?.OnFire(angle);
            MyGun = null;

            var bubble = new BubblegunBubble(MyPlayer, pos, angle, index);
            bubble.Register(bubble);
            return bubble;
        }

        [Local]
        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (MyGun != null) RpcEquip.Invoke((MyPlayer.PlayerId, false));
        }

        public void OnBubbleKill(int index, GamePlayer dead)
        {
            Bubbles[index].OnKill(dead);
        }

        bool IPlayerAbility.HideKillButton => MyGun != null;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), LeftUses];
    }

    //泡キル

    static private readonly IDividedSpriteLoader bubbleSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Bubble.png", 100f, 4, 3);

    static readonly RemoteProcess<(GamePlayer killer, GamePlayer dead, Vector2 pos, int index)> RpcBubbleKill = new(
           "BubbleKill",
           (message, _) =>
           {
               SpawnDeadBodyBubble(message.killer, message.dead, message.pos);

               var ability = message.killer?.Role.GetAbility<Bubblegun.Ability>();
               ability?.OnBubbleKill(message.index, message.dead);
           }
           );

    static List<GameObject> allBubbles = [];

    [NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
    private class BubbleManager : AbstractModule<Virial.Game.Game>, IGameOperator{
        static BubbleManager() => DIManager.Instance.RegisterModule(() => new BubbleManager());

        void Update(GameHudUpdateEvent ev) => allBubbles.RemoveAll(b => !b);
    }


    static void SpawnDeadBodyBubble(GamePlayer killer, GamePlayer player, Vector2 position)
    {
        AmongUsUtil.PlayCinematicKill(killer, player, 0.7f, 0.55f, PlayerState.Bubbled, EventDetail.Bubbled, () => {
            var bubbleHolder = UnityHelper.CreateObject("Bubble", null, position.AsVector3(-1f), LayerExpansion.GetDefaultLayer());
            allBubbles.Add(bubbleHolder);

            bubbleHolder.transform.localScale = new Vector3(1f, 1f, 0.1f);
            var bubble = UnityHelper.CreateObject("Sin", bubbleHolder.transform, Vector3.zero);
            var bubbleRenderer = UnityHelper.CreateObject<SpriteRenderer>("Sprite", bubble.transform, Vector3.zero);
            bubbleRenderer.transform.localScale = new(0.43f, 0.43f, 1f);
            bubbleRenderer.sprite = bubbleSprite.GetSprite(0);
            bubbleRenderer.material = new Material(NebulaAsset.HSVShader);

            if (eraseBubblesOnMeeting) GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(_ => { if (bubbleHolder) GameObject.Destroy(bubbleHolder); }, new GameObjectLifespan(bubbleHolder));

            var bubbleInner = UnityHelper.CreateObject("Inner", bubble.transform, Vector3.zero);

            var deadBody = GameObject.Instantiate(HudManager.Instance.KillOverlay.KillAnims[0].victimParts, bubbleInner.transform);
            deadBody.transform.localPosition = new Vector3(0.2f, 0.12f, 1f);
            deadBody.transform.localScale = new(0.36f, 0.36f, 0.5f);
            deadBody.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetDefaultLayer());
            deadBody.UpdateFromPlayerOutfit(player.DefaultOutfit.outfit, PlayerMaterial.MaskType.None, false, false, (System.Action)(() =>
            {
                var skinView = deadBody.GetSkinView();
                var skinAnim = deadBody.GetSkinSpriteAnim();
                if (skinView != null) skinAnim?.Play(skinView.KillStabVictim);

                deadBody.StartCoroutine(CoAnimDeadBody().WrapToIl2Cpp());
            }));
            deadBody.ToggleName(false);
            deadBody.StartCoroutine(CoRotateAndMove().WrapToIl2Cpp());
            deadBody.StartCoroutine(CoUpdateImage().WrapToIl2Cpp());
            deadBody.StartCoroutine(CoUpdateSat().WrapToIl2Cpp());
            if (player.AmOwner) NebulaAsset.PlaySE(NebulaAudioClip.BubbleLong, volume: 1f);
            else if (killer.AmOwner) NebulaAsset.PlaySE(Helpers.Prob(50) ? NebulaAudioClip.Bubble1 : NebulaAudioClip.Bubble2, oneshot: true, volume: 1f);

            IEnumerator CoAnimDeadBody()
            {
                var anims = deadBody.GetComponentsInChildren<SpriteAnim>();
                foreach (var anim in anims) anim.Speed = 1f;
                for (int i = 0; i < 5; i++)
                {
                    foreach (var anim in anims) anim.Time = 0.45f;
                    yield return Effects.Wait(0.44f);
                }
                foreach (var anim in anims)
                {
                    anim.Time = 0.45f;
                    anim.Speed = 0f;
                }
                yield return Effects.Wait(5f);
                while (true)
                {
                    foreach (var anim in anims)
                    {
                        anim.Time = 0.45f;
                        anim.Speed = 0.5f;
                    }
                    yield return Effects.Wait(1.5f);
                    foreach (var anim in anims)
                    {
                        anim.Speed = 0f;
                    }
                    yield return Effects.Wait(7f);
                }
            }

            IEnumerator CoRotateAndMove()
            {
                float angle = System.Random.Shared.NextSingle() * 180f;
                float sin = 0f;
                while (true)
                {
                    angle += Time.deltaTime * 15f;
                    sin += Time.deltaTime * 1f;
                    bubbleInner.transform.localEulerAngles = new(0f, 0f, -angle);
                    bubble.transform.localPosition = new(0f, Mathf.Sin(sin) * 0.12f, 0f);
                    if (angle > 360f) angle -= 360f;

                    allBubbles.Do(b =>
                    {
                        if (!b) return;
                        if (b == bubbleHolder) return;
                        var distance = b.transform.position.Distance(bubbleHolder.transform.position);
                        if (distance < 1.1f)
                        {
                            var vec = (bubbleHolder.transform.position - b.transform.position).normalized;
                            var z = bubbleHolder.transform.localPosition.z;
                            bubbleHolder.transform.position += vec * (1.1f - distance) * Time.deltaTime;
                            bubbleHolder.transform.SetLocalZ(z);

                        }
                    });
                    yield return null;
                }
            }

            IEnumerator CoUpdateImage()
            {
                int index = 0;
                while (true)
                {
                    yield return Effects.Wait(0.25f);
                    index = (index + 1) % 4;
                    bubbleRenderer.sprite = bubbleSprite.GetSprite(index);
                }
            }

            IEnumerator CoUpdateSat()
            {
                yield return Effects.Wait(1.5f);
                float sat = 1f;
                while (sat > 0.2f)
                {
                    sat -= Time.deltaTime * 0.4f;
                    bubbleRenderer.material.SetFloat("_Sat", sat);
                    yield return null;
                }
                bubbleRenderer.material.SetFloat("_Sat", 0.2f);
                yield break;
            }

            return (position.AsVector3(-1f), bubbleHolder);
        });

    }
}