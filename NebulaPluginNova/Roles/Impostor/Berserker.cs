﻿using Nebula.Patches;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Berserker : DefinedSingleAbilityRoleTemplate<Berserker.Ability>, DefinedRole
{
    private Berserker() : base("berserker", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [killCoolDownOption, accelRateOption, canCalmDownOption, berserkCooldownOption, berserkSEStrengthOption, maxBerserkDurationOption, killingForTranceOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Berserker.png");

        GameActionTypes.BerserkerTransformingAction = new("berserker.berserk", this, isPhysicalAction: true);
    }    


    static private IRelativeCoolDownConfiguration killCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.berserker.killCooldown", CoolDownType.Immediate, (1f, 20f, 1f), 5f, (-30f, 10f, 2.5f), -15f, (0.125f, 2f, 0.125f), 0.25f);
    static private FloatConfiguration accelRateOption = NebulaAPI.Configurations.Configuration("options.role.berserker.accelRate", (1f, 3f, 0.25f), 1.5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration berserkSEStrengthOption = NebulaAPI.Configurations.Configuration("options.role.berserker.bersekSEStrength", (1.25f, 20f, 1.25f), 7.5f, FloatConfigurationDecorator.Ratio);
    static private BoolConfiguration canCalmDownOption = NebulaAPI.Configurations.Configuration("options.role.berserker.canCalmDown", true);
    static private IntegerConfiguration killingForTranceOption = NebulaAPI.Configurations.Configuration("options.role.berserker.killingForTrance", (1, 5), 2);
    static private FloatConfiguration berserkCooldownOption = NebulaAPI.Configurations.Configuration("options.role.berserker.berserkCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration maxBerserkDurationOption = NebulaAPI.Configurations.Configuration("options.role.berserker.maxBerserkDuration", (10f, 180f, 5f), 30f, FloatConfigurationDecorator.Second, () => canCalmDownOption);
    
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player);
    bool DefinedRole.IsJackalizable => true;
    static public Berserker MyRole = new Berserker();
    static private GameStatsEntry StatsBerserk = NebulaAPI.CreateStatsEntry("stats.berserker.berserk", GameStatsCategory.Roles, MyRole);
    public class BerserkMode : SimpleReleasable, IGameOperator, IBindPlayer
    {
        Berserker.Ability myBerserker;
        GamePlayer myPlayer => (myBerserker as IBindPlayer).MyPlayer;
        GamePlayer IBindPlayer.MyPlayer => myPlayer;

        public bool IsTrancing { get; private set; } = false;

        public BerserkMode(Berserker.Ability berserker)
        {
            myBerserker = berserker;

            NebulaManager.Instance.ScheduleDelayAction(() =>
            {
                RpcBerserk.Invoke(myPlayer);
                var berserkKillCooldown = killCoolDownOption.GetCoolDown(myPlayer.TeamKillCooldown);
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.SetCooldown(0f);
            });

            myPlayer.GainAttribute(accelRateOption, float.MaxValue, false, 0, "nebula::berserk");
        }

        void IGameOperator.OnReleased()
        {
            if (myBerserker.AmBerserking) RpcCalmDown.Invoke((myPlayer, true));

            myPlayer.GainAttribute(1f, 0f, false, 0, "nebula::berserk");
            NebulaGameManager.Instance?.AllPlayerInfo.Do(p => p.Unbox().RemoveOutfit("nebula::berserk"));
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
        }

        bool calmDownInvoked = false;
        public void CalmDown()
        {
            if (calmDownInvoked) return;
            calmDownInvoked = true;

            NebulaManager.Instance.ScheduleDelayAction(() =>
            {
                RpcCalmDown.Invoke((myPlayer, false));
            });
            NebulaManager.Instance.StartDelayAction(1f, () => this.ReleaseIt());
        }

        int killing = 0;
        public bool KilledAnyone => killing > 0;
        float killingFlash = 0f;
        void OnUpdateCamera(CameraUpdateEvent ev)
        {

            if (killingFlash > 0f && killing <= killingForTranceOption)
            {
                ev.UpdateHue(180f);
            }
            else
            {
                ev.UpdateSaturation(Mathf.Max(0f, 1f - killing * 0.4f), true);
            }

            if (killingFlash > 0f) killingFlash -= Time.deltaTime;
        }

        void OnResetKillCooldown(ResetKillCooldownLocalEvent ev) => ev.SetFixedCooldown(killCoolDownOption.GetCoolDown(ev.Player.TeamKillCooldown));
        

        [OnlyMyPlayer]
        void OnMurderPlayer(PlayerKillPlayerEvent ev)
        {
            killing++;
            killingFlash = killing == killingForTranceOption ? 0.6f : (0.15f + 0.05f * killing);
            if (killing == killingForTranceOption)
            {
                NebulaManager.Instance.StartDelayAction(0.3f, () =>
                {
                    PlayerModInfo.RpcAttrModulator.Invoke((myPlayer.PlayerId, new FloatModulator(PlayerAttributes.Roughening, 10, 0.3f, false, 0, "nebula::berserk", false), true));
                });
                NebulaManager.Instance.StartDelayAction(0.6f, () =>
                {
                    NebulaGameManager.Instance?.AllPlayerInfo.Where(p => !p.AmOwner).Do(p => p.Unbox().AddOutfit(new(NebulaGameManager.Instance.UnknownOutfit, "nebula::berserk", 100, true)));
                    IsTrancing = true;
                });
            }
            
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            this.ReleaseIt();
        }

        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            this.ReleaseIt();
        }
    }

    public class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BerserkButton.png", 115f);
        static private Image buttonCalmSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BerserkCalmButton.png", 115f);

        public bool AmBerserking => MyPlayer.VanillaPlayer.cosmetics.bodyType == PlayerBodyTypes.Seeker;
        public BerserkMode? CurrentBerserkerMode = null;

        public Ability(GamePlayer player) : base(player)
        {
            if (AmOwner)
            {
                var acTokenAnother1 = AchievementTokens.FirstFailedAchievementToken("berserker.another1", MyPlayer, this);

                AchievementToken<(float last1Kill, float last2Kill, bool triggered)> acTokenChallenge = new("berserker.challenge", (-100f, -100f, false), (a, _) => a.triggered && (NebulaGameManager.Instance?.EndState?.Winners.Test(MyPlayer) ?? false));
                GameOperatorManager.Instance?.Register<PlayerKillPlayerEvent>(ev =>
                {
                    if(ev.Player.AmOwner && AmBerserking)
                    {
                        float time = NebulaGameManager.Instance!.CurrentTime - acTokenChallenge.Value.last2Kill;
                        if (time < 20f) acTokenChallenge.Value.triggered = true;
                        acTokenChallenge.Value.last2Kill = acTokenChallenge.Value.last1Kill;
                        acTokenChallenge.Value.last1Kill = NebulaGameManager.Instance!.CurrentTime;
                    }
                }, this);

                ModAbilityButton calmButton = null!;
                var berserkButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                berserkButton.SetSprite(buttonSprite.GetSprite());
                berserkButton.Availability = (button) => MyPlayer.CanMove;
                berserkButton.Visibility = (button) => !MyPlayer.IsDead && !AmBerserking;
                berserkButton.OnClick = (button) =>
                {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.BerserkerTransformingAction);

                    if (CurrentBerserkerMode != null) CurrentBerserkerMode.ReleaseIt();
                    CurrentBerserkerMode = Bind(new BerserkMode(this)).Register();

                    StatsBerserk.Progress();
                    new StaticAchievementToken("berserker.common1");
                    new StaticAchievementToken("berserker.common2");
                    acTokenAnother1.Value.triggered = true;

                    if (canCalmDownOption)
                    {
                        calmButton.ActivateEffect();
                    }
                };
                berserkButton.OnMeeting = button => button.StartCoolDown();
                berserkButton.CoolDownTimer = Bind(new Timer(berserkCooldownOption).SetAsAbilityCoolDown().Start());
                berserkButton.SetLabel("berserk");
                berserkButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);

                calmButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                calmButton.SetSprite(buttonCalmSprite.GetSprite());
                calmButton.Availability = (button) => MyPlayer.CanMove;
                calmButton.Visibility = (button) => !MyPlayer.IsDead && AmBerserking && canCalmDownOption;
                void CalmDown()
                {
                    CurrentBerserkerMode?.CalmDown();
                    CurrentBerserkerMode = null;
                    berserkButton.StartCoolDown();
                }
                calmButton.OnClick = (button) =>
                {
                    if(CurrentBerserkerMode?.KilledAnyone ?? false) new StaticAchievementToken("berserker.common3");
                    calmButton.InactivateEffect();
                };
                calmButton.OnEffectEnd = (button) =>
                {
                    CalmDown();
                };
                calmButton.OnMeeting = button => button.StartCoolDown();
                var spawnAnim = HudManager.Instance.IntroPrefab.HnSSeekerSpawnAnim;
                calmButton.EffectTimer = Bind(new Timer(maxBerserkDurationOption).SetPredicate(()=>MyPlayer.VanillaPlayer.MyPhysics.Animations.Animator.GetCurrentAnimation() != spawnAnim));
                calmButton.SetLabel("calmDown");
            }
        }

        bool IPlayerAbility.KillIgnoreTeam => CurrentBerserkerMode?.IsTrancing ?? false;

        bool killWhileBerserking = false;
        [OnlyMyPlayer, Local]
        void OnKillAnyone(PlayerKillPlayerEvent ev)
        {
            if (AmBerserking)
            {
                if (ev.Dead.IsImpostor) new StaticAchievementToken("berserker.another2");
                killWhileBerserking = true;
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (killWhileBerserking && !MyPlayer.IsDead) new StaticAchievementToken("berserker.common4");
        }
    }

    static private RemoteProcess<GamePlayer> RpcBerserk = new("berserk", (player, _) =>
    {
        player.VanillaPlayer.NetTransform.Halt();
        var lastFlipX = player.VanillaPlayer.MyPhysics.FlipX;
        player.VanillaPlayer.MyPhysics.SetBodyType(PlayerBodyTypes.Seeker);
        player.VanillaPlayer.MyPhysics.FlipX = lastFlipX;
        player.VanillaPlayer.AnimateCustom(HudManager.Instance.IntroPrefab.HnSSeekerSpawnAnim);

        player.VanillaPlayer.MyPhysics.Animations.Animator.SetTime(7.4f);//Coroutineは最初のyieldまで実行したのちに脱出することに留意
        NebulaAsset.PlaySE(VanillaAsset.HnSTransformClip.Clip, player.VanillaPlayer.transform.position, 0.8f, berserkSEStrengthOption, 0.8f);
        player.VanillaPlayer.cosmetics.SetBodyCosmeticsVisible(false);
    });

    static private void ResetBody(GamePlayer player)
    {
        var lastFlipX = player.VanillaPlayer.MyPhysics.FlipX;
        player.VanillaPlayer.MyPhysics.SetBodyType(PlayerBodyTypes.Normal);
        player.VanillaPlayer.MyPhysics.FlipX = lastFlipX;
        player.VanillaPlayer.cosmetics.SetBodyCosmeticsVisible(true);
    }

    static private RemoteProcess<(GamePlayer player, bool immediately)> RpcCalmDown = new("calmDown", (message, _) =>
    {
        if (message.immediately)
        {
            ResetBody(message.player);
        }
        else
        {

            message.player.VanillaPlayer.NetTransform.Halt();
            message.player.GainAttribute(0, 1.5f, false, 0);

            RoleEffectAnimation chargeAnim = GameObject.Instantiate<RoleEffectAnimation>(RoleManager.Instance.vanish_ChargeAnim, message.player.VanillaPlayer.transform);
            //roleEffectAnimation2.SetMaskLayerBasedOnWhoShouldSee(base.AmOwner);
            chargeAnim.SetMaterialColor(message.player.PlayerId);
            AmongUsUtil.GetRolePrefab<PhantomRole>()?.PlayPhantomAppearSound();
            chargeAnim.Play(message.player.VanillaPlayer, (Il2CppSystem.Action)(() =>
            {
                ResetBody(message.player);

                RoleEffectAnimation poofAnim = GameObject.Instantiate<RoleEffectAnimation>(DestroyableSingleton<RoleManager>.Instance.vanish_PoofAnim, message.player.VanillaPlayer.transform);
                poofAnim.SetMaterialColor(message.player.PlayerId);
                poofAnim.Play(message.player.VanillaPlayer, (Il2CppSystem.Action)(() => { }), message.player.VanillaPlayer.cosmetics.FlipX, RoleEffectAnimation.SoundType.Local, 0f, true, 0f);
            }), message.player.VanillaPlayer.cosmetics.FlipX, RoleEffectAnimation.SoundType.Local, 1f, true, -0.05f);
        }
    });
}