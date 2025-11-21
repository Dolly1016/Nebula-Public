using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;


internal class Zeal : DefinedSingleAbilityRoleTemplate<Zeal.Ability>, DefinedRole
{
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.zeal.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 40f, (-40f, 40f, 2.5f), 10f, (0.125f, 2f, 0.125f), 1.25f);
    static private FloatConfiguration AbsorbCooldownOption = NebulaAPI.Configurations.Configuration("options.role.zeal.absorbCooldown", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration AbsorbDurationOption = NebulaAPI.Configurations.Configuration("options.role.zeal.absorbDuration", (1f, 10f, 1f), 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration AbsorbAdditionalDurationOption = NebulaAPI.Configurations.Configuration("options.role.zeal.absorbAdditionalDuration", (float[])[0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f], 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EnthusiasmCooldownOption = NebulaAPI.Configurations.Configuration("options.role.zeal.enthusiasmCooldown", (10f, 120f, 10f), 60f, FloatConfigurationDecorator.Second);
    private Zeal() : base("zeal", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [KillCoolDownOption, AbsorbCooldownOption, AbsorbDurationOption, AbsorbAdditionalDurationOption, EnthusiasmCooldownOption])
    {
        
    }

    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    static public readonly Zeal MyRole = new();

    static private Image douseButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ZealButton.png", 115f);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private class PlayerIcon
        {
            public PlayerIconInfo Icon;
            public float Cooldown;
            public GamePlayer Player;
            public PlayerIcon(PlayerIconInfo icon, GamePlayer player)
            {
                Icon = icon;
                Cooldown = EnthusiasmCooldownOption;
                Player = player;
            }
        }
        private List<PlayerIcon> playerIcons = [];
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        ModAbilityButton interactButton = null!;
        bool IPlayerAbility.HideKillButton => !(interactButton?.IsBroken ?? false);
        
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                HudContent emptyContent = HudContent.InstantiateContent("Empty", false, false, true, false);
                List<(IPlayerlike player, float time)> killHistory = [];

                GameTimer cooldownTimer = NebulaAPI.Modules.Timer(this, KillCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown)).SetAsKillCoolTimer().ResetsAtTaskPhase();
                cooldownTimer.Start();

                var iconHolder = new PlayersIconHolder(false).Register(this);
                iconHolder.XInterval = 0.35f;

                void RemoveIcons(Func<PlayerIcon, bool> predicate)
                {
                    playerIcons.RemoveAll(icon =>
                    {
                        var remove = predicate.Invoke(icon);
                        if (remove) iconHolder.Remove(icon.Icon);
                        return remove;
                    });
                }

                void UpdateIcons()
                {
                    bool inMeeting = MeetingHud.Instance || ExileController.Instance;
                    RemoveIcons(icon =>
                    {
                        if (!inMeeting)
                        {
                            icon.Cooldown -= Time.deltaTime;
                            icon.Icon.SetText(Mathn.CeilToInt(icon.Cooldown).ToString());
                            return icon.Cooldown < 0f;
                        }
                        return false;
                    });
                }

                void AddPlayerIcon(GamePlayer player)
                {
                    playerIcons.Add(new(iconHolder.AddPlayer(player), player));
                }

                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => UpdateIcons(), this);

                //死亡しているならクールダウンは関係ない。邪魔なので削除。
                GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev => {
                    RemoveIcons(icon => icon.Player.IsDead);
                }, iconHolder);

                var interactTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeStandardPredicate(p) && playerIcons.All(icon => icon.Player != p));

                interactButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    AbsorbCooldownOption, AbsorbDurationOption, "absorb", douseButtonSprite,
                    _ => interactTracker.CurrentTarget != null);
                interactButton.OnEffectStart = (button) =>
                {
                    interactTracker.KeepAsLongAsPossible = true;
                    var timer = button.EffectTimer as GameTimer;
                    timer!.SetRange(AbsorbDurationOption + AbsorbAdditionalDurationOption * killButtons.Count);
                    timer.Start();
                };
                interactButton.OnEffectEnd = (button) =>
                {
                    interactTracker.KeepAsLongAsPossible = false;
                    if (interactTracker.CurrentTarget == null) return;
                    if (MeetingHud.Instance) return;

                    if (!button.EffectTimer!.IsProgressing)
                    {
                        if (!(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, interactTracker.CurrentTarget, new(RealPlayerOnly: true))).IsCanceled ?? false))
                        {
                            GenerateKillButton();
                            AddPlayerIcon(interactTracker.CurrentTarget.RealPlayer);
                        }
                    }
                    button.StartCoolDown();
                };
                interactButton.OnUpdate = (button) => {
                    if (!button.IsInEffect) return;
                    if (interactTracker.CurrentTarget == null) button.InterruptEffect();
                };
                interactButton.SetAsUsurpableButton(this);
                interactButton.Priority = 1;
                
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => {
                    float currentTime = NebulaGameManager.Instance?.CurrentTime ?? 0f;
                    killHistory.RemoveAll(history => history.time + 2.8f < currentTime);
                }, this);

                ModAbilityButton GenerateKillButton()
                {
                    FlexibleLifespan lifespan = new(this);
                    var killButton = NebulaAPI.Modules.PlayerlikeKillButton(lifespan, MyPlayer, false, Virial.Compat.VirtualKeyInput.None, null,
                        0f, "kill", Virial.Components.ModAbilityButton.LabelType.Impostor, null,
                        (p, button) =>
                        {
                            if (killHistory.Count >= 2) new StaticAchievementToken("zeal.common1");

                            killHistory.Add((p, NebulaGameManager.Instance?.CurrentTime ?? 0f));
                            MyPlayer.MurderPlayer(p, PlayerState.Dead, null, KillParameter.NormalKill);
                            lifespan.Release();
                            UpdateKillButtons();
                            if (killButtons.Count == 0) cooldownTimer.Start(); //キルボタンを持たなくなったらクールダウンをため直す
                        }, p => !killHistory.Any(history => history.player == p), null).SetAsUsurpableButton(this);
                    killButton.CoolDownTimer = cooldownTimer;
                    killButton.ShouldBeInLastLine = true;
                    killButton.SetAsUsurpableButton(this);
                    killButton.OnBroken = _ => {
                        OnBecameClearUsurped();
                    };
                    killButtons.Enqueue((killButton, lifespan));
                    UpdateKillButtons();
                    if (killButtons.Count == 4) new StaticAchievementToken("zeal.common2");
                    return killButton;
                }

                GameOperatorManager.Instance?.SubscribeAchievement<GameEndEvent>("zeal.challenge", ev => killHistory.Count >= 3  && ev.EndState.Winners.Test(MyPlayer) && (NebulaGameManager.Instance?.LastDead?.MyKiller?.AmOwner ?? false), this);
                GameOperatorManager.Instance?.SubscribeAchievement<PlayerDieEvent>("zeal.another1", ev => ev.Player.AmOwner && killButtons.Count >= 2, this);
                

                void UpdateKillButtons()
                {
                    while (killButtons.Count > 0)
                    {
                        var entry = killButtons.Peek();
                        if (entry.lifespan.IsDeadObject)
                        {
                            killButtons.Dequeue();
                            continue;
                        }
                        if (!entry.button.IsKillButtonContent)
                        {
                            NebulaManager.Instance.ScheduleDelayAction(() => {
                                entry.button.IsKillButtonContent = true;
                                entry.button.ShouldBeInLastLine = false;
                            });
                            entry.button.BindKey(Virial.Compat.VirtualKeyInput.Kill);
                        }
                        break;
                    }

                    if (killButtons.Count == 0)
                    {
                        emptyContent.gameObject.SetActive(true);
                    }
                    else
                    {
                        NebulaManager.Instance.ScheduleDelayAction(() => emptyContent.gameObject.SetActive(false));
                    }
                }
            }
        }

        Queue<(ModAbilityButton button, FlexibleLifespan lifespan)> killButtons = [];
        
        bool invoked_onBecameClearUsurped = false;
        void OnBecameClearUsurped()
        {
            if (invoked_onBecameClearUsurped) return;
            invoked_onBecameClearUsurped = true;

            interactButton.Break();
            foreach(var killButton in killButtons) killButton.lifespan.Release();
        }
    }
}
