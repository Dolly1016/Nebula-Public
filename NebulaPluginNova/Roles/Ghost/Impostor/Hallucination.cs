using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial;
using Nebula.Behaviour;
using Virial.Events.Game;
using AmongUs.Data;
using Virial.Game;
using Virial.Events.Player;
using static Rewired.ComponentControls.Effects.RotateAroundAxis;
using Virial.Events.Game.Meeting;
using Nebula.Roles.Ghost.Neutral;
using static Nebula.Roles.Ghost.Neutral.Grudge;

namespace Nebula.Roles.Ghost.Impostor;

public class Hallucination : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public class HallucinationPlayer : IGameOperator, ILifespan
    {
        private PlayerDisplay display;
        GamePlayer originalPlayer;
        GamePlayer targetGhost;
        bool isValid = true;
        public bool isDeadObject = false;
        float speed = 2.5f;
        float alpha = 0f;
        SpriteRenderer[] allRenderers;
        public bool IsDeadObject => isDeadObject;

        void IGameOperator.OnReleased()
        {
            if (display) GameObject.Destroy(display.gameObject);
        }

        public void Disappear()
        {
            isValid = false;
        }

        public HallucinationPlayer(Vector2 pos, GamePlayer originalPlayer, GamePlayer targetGhost)
        {
            this.originalPlayer = originalPlayer;
            this.targetGhost = targetGhost;

            //スピードを計算
            speed = GameManager.Instance.LogicOptions.GetPlayerSpeedMod(originalPlayer.VanillaPlayer) * 2.5f;

            display = VanillaAsset.GetPlayerDisplay();
            UnityHelper.DoForAllChildren(display.gameObject, obj => obj.layer = LayerExpansion.GetPlayersLayer());
            display.transform.position = pos;
            
            var scale = display.Cosmetics.transform.localScale;
            scale.z = 0.001f;
            display.Cosmetics.transform.localScale = scale;

            UpdateOutfit(originalPlayer.CurrentOutfit);

            allRenderers = display.gameObject.GetComponentsInChildren<SpriteRenderer>();
            allRenderers.Do(r => r.color = Color.clear);
        }

        private void UpdateOutfit(Outfit outfit)
        {
            display.UpdateFromPlayerOutfit(outfit.outfit, false, true);
            display.Cosmetics.nameText.gameObject.SetActive(true);
            display.Cosmetics.nameText.transform.parent.localPosition = new(0f, 1f, -0.5f);
            display.Cosmetics.nameText.text = outfit.outfit.PlayerName;
        }

        void OnOutfitChanged(PlayerOutfitChangeEvent ev)
        {
            if (ev.Player.PlayerId == originalPlayer.PlayerId)
            {
                UpdateOutfit(ev.Outfit);
            }
        }

        bool acCommon3 = false;

        void OnUpdate(GameHudUpdateEvent ev)
        {
            UpdatePos();

            //ハルシネーションが生き返ったら幻影を消す
            if (!targetGhost.IsDead) isValid = false;

            if (isValid && alpha < 1f)
            {
                alpha += Time.deltaTime * 1.5f;
                if(!(alpha < 1f)) alpha = 1f;
            }
            if(!isValid && alpha > 0f)
            {
                alpha -= Time.deltaTime * 0.6f;
                if(!(alpha > 0f))
                {
                    isDeadObject = true;
                }
            }

            Color color = new(1f, 1f, 1f, alpha);
            allRenderers.Do(r => r.color = color);

            if (targetGhost.AmOwner)
            {
                //ハルシネーション本人のみ

                if(!acCommon3 && !originalPlayer.IsDead && originalPlayer.Position.ToUnityVector().Distance(display.transform.position) < 2f)
                {
                    new StaticAchievementToken("hallucination.common3");
                    acCommon3 = true;
                }
            }
        }

        void UpdatePos()
        {
            display.Cosmetics.colorBlindText.gameObject.SetActive(DataManager.Settings.Accessibility.ColorBlindMode);

            Vector2 currentPos = display.transform.position;
            Vector2 targetPos = targetGhost.VanillaPlayer.transform.position;
            var diff = targetPos - currentPos;
            var movement = diff.normalized * speed * Time.deltaTime;

            if (diff.magnitude < movement.magnitude)
            {
                display.Animations.PlayIdleAnimation();
                display.Cosmetics.AnimateSkinIdle();
                display.transform.position = targetGhost.VanillaPlayer.transform.position;
            }
            else
            {
                var lastFlipX = display.Cosmetics.FlipX;

                if (movement.magnitude > 0f) display.Cosmetics.SetFlipX(movement.x < 0f);
                var gotoPos = display.transform.position + (Vector3)movement;
                gotoPos.z = gotoPos.y / 1000f;
                display.transform.position = gotoPos;

                if (movement.magnitude > 0f)
                {
                    if (!display.Animations.IsPlayingRunAnimation() || lastFlipX != display.Cosmetics.FlipX)
                    {
                        display.Animations.PlayRunAnimation();
                        display.Cosmetics.AnimateSkinRun();
                    }
                }
                else
                {
                    if (display.Animations.IsPlayingRunAnimation() || !display.Animations.IsPlayingSomeAnimation())
                    {
                        display.Animations.PlayIdleAnimation();
                        display.Cosmetics.AnimateSkinIdle();
                    }
                }
            }
        }

        void onMeetingStart(MeetingStartEvent ev)
        {
            isValid = false;
        }
    }

    public Hallucination() : base("hallucination", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, [HallucinationCooldownOption, HallucinationDurationOption])
    {
    }

    string ICodeName.CodeName => "HLC";

    static private FloatConfiguration HallucinationCooldownOption = NebulaAPI.Configurations.Configuration("options.role.hallucination.hallucinationCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration HallucinationDurationOption = NebulaAPI.Configurations.Configuration("options.role.hallucination.hallucinationDuration", (10f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    

    static public Hallucination MyRole = new Hallucination();
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player) { }

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.HallucinationButton.png", 115f);

        private HallucinationPlayer? currentHallucination = null;
        private AchievementToken<(uint mask, bool cleared)>? acTokenAnother1 = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenAnother1 = new("hallucination.another1", (0u, false), (val, _) => val.cleared);

                var hallucinationButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                hallucinationButton.SetSprite(buttonSprite.GetSprite());
                hallucinationButton.Availability = (button) => MyPlayer.CanMove;
                hallucinationButton.Visibility = (button) => MyPlayer.IsDead;
                hallucinationButton.OnClick = (button) =>
                {
                    button.ToggleEffect();
                };
                hallucinationButton.OnEffectStart = (button) =>
                {
                    var cand = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => p.PlayerId != MyPlayer.PlayerId);
                    if (cand.Any(p => !p.IsDead)) cand = cand.Where(p => !p.IsDead);
                    if (cand.IsEmpty()) cand = [MyPlayer]; //フリープレイ対策

                    var random = cand.ToArray().Random();
                    RpcShowHallucination.Invoke((MyPlayer, random, MyPlayer.VanillaPlayer.transform.position));

                    new StaticAchievementToken("hallucination.common1");
                    if (NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && ((Vector2)MyPlayer.VanillaPlayer.transform.position).Distance(p.Position) < 1f))
                        new StaticAchievementToken("hallucination.common2");

                    acTokenAnother1.Value.mask |= 1u << random.PlayerId;
                };
                hallucinationButton.OnEffectEnd = (button) =>
                {
                    RpcDisappearHallucination.Invoke(MyPlayer);
                    button.StartCoolDown();
                };
                hallucinationButton.CoolDownTimer = Bind(new Timer(0f, HallucinationCooldownOption).SetAsAbilityCoolDown().Start());
                hallucinationButton.EffectTimer = Bind(new Timer(HallucinationDurationOption));
                hallucinationButton.SetLabel("hallucination");
            }
        }

        [Local]
        void OnDead(PlayerDieEvent ev)
        {
            if (((acTokenAnother1?.Value.mask ?? 0) & (1u << ev.Player.PlayerId)) != 0u && (ev.Player.PlayerState == PlayerState.Guessed || ev.Player.PlayerState == PlayerState.Exiled)) acTokenAnother1!.Value.cleared = true;
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenAnother1 != null) acTokenAnother1.Value.mask = 0u;
        }


        RemoteProcess<(GamePlayer hallucination, GamePlayer target, Vector2 pos)> RpcShowHallucination = new("ShowHallucination",
            (message, _) =>
            {
                var hallucination = message.hallucination.Unbox().GhostRole as Hallucination.Instance;
                if (hallucination != null)
                {
                    if (hallucination.currentHallucination != null) hallucination.currentHallucination.Disappear();
                    hallucination.currentHallucination = new HallucinationPlayer(message.pos, message.target, message.hallucination).Register();
                }
            });

        RemoteProcess<GamePlayer> RpcDisappearHallucination = new("DisappearHallucination",
            (message, _) =>
            {
                var hallucination = message.Unbox().GhostRole as Hallucination.Instance;
                if (hallucination?.currentHallucination != null)
                {
                    hallucination.currentHallucination.Disappear();
                    hallucination.currentHallucination = null;
                }
            });
    }
}
