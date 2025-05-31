using Nebula.Behavior;
using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Echo : DefinedSingleAbilityRoleTemplate<Echo.Ability>, DefinedRole
{
    private Echo() : base("echo", new(117, 154, 102), RoleCategory.CrewmateRole, Crewmate.MyTeam, [EchoCooldownOption, EchoRangeOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    static private readonly FloatConfiguration EchoCooldownOption = NebulaAPI.Configurations.Configuration("options.role.echo.echoCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EchoRangeOption = NebulaAPI.Configurations.Configuration("options.role.echo.echoRange", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    static public readonly Echo MyRole = new();
    static private readonly GameStatsEntry StatsPlayers = NebulaAPI.CreateStatsEntry("stats.echo.players", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsActions = NebulaAPI.CreateStatsEntry("stats.echo.actions", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EchoButton.png", 115f);
        static private readonly Image iconSprite = SpriteLoader.FromResource("Nebula.Resources.EchoIcon.png", 160f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var searchButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, EchoCooldownOption, "echo", buttonSprite)
                    .SetAsUsurpableButton(this);
                searchButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.StartCoroutine(CoEcho(MyPlayer.Position).WrapToIl2Cpp());
                    button.StartCoolDown();
                };
            }
        }

        IEnumerator CoEcho(Vector2 position)
        {
            EditableBitMask<GamePlayer> pMask = BitMasks.AsPlayer();
            float radious = 0f;
            var circle = EffectCircle.SpawnEffectCircle(null, MyPlayer.Position.ToUnityVector(), MyRole.UnityColor, 0f, null, true);
            this.BindGameObject(circle.gameObject);
            circle.OuterRadius = () => radious;

            MyRole.UnityColor.ToHSV(out var hue, out _, out _);
            bool isFirst = true;
            while (radious < EchoRangeOption)
            {
                if (MeetingHud.Instance) break;

                radious += Time.deltaTime * 5f;
                foreach(var p in NebulaGameManager.Instance?.AllPlayerInfo ?? [])
                {
                    if(!p.AmOwner && !p.IsDead && !pMask.Test(p) && p.Position.Distance(position) < radious)
                    {
                        pMask.Add(p);
                        AmongUsUtil.Ping([p.Position], false, isFirst, postProcess: ping => ping.gameObject.SetHue(360 - hue));
                        StatsPlayers.Progress();
                        isFirst = false;

                        new StaticAchievementToken("echo.common1");
                    }
                }
                yield return null;
            }

            circle.Disappear();
        }

        EditableBitMask<GamePlayer> receiptPlayers = BitMasks.AsPlayer();
        void OnReceiptGameAction(PlayerDoGameActionEvent ev)
        {
            if (!AmOwner) return;
            if (ev.Player.AmOwner) return;

            var arrow = new Arrow(iconSprite.GetSprite(), false, true) { FixedAngle = true, IsAffectedByComms = false }.Register(NebulaAPI.CurrentGame!);

            StatsActions.Progress();
            arrow.FixedAngle = true;
            arrow.TargetPos = ev.Position;
            NebulaManager.Instance.StartDelayAction(5f, () => arrow.MarkAsDisappering());

            receiptPlayers.Add(ev.Player);

            new StaticAchievementToken("echo.common2");
        }

        int num = 0;
        [OnlyMyPlayer]
        void OnPreExiled(PlayerVoteDisclosedLocalEvent ev)
        {
            if (ev.VoteToWillBeExiled && receiptPlayers.Test(ev.VoteFor) && !ev.VoteFor!.IsCrewmate)
            {
                num++;
                if(num == 2) new StaticAchievementToken("echo.challenge");
            }
        }
    }
}

