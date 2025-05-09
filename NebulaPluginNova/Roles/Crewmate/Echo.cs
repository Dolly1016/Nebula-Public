﻿using Nebula.Behaviour;
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

public class Echo : DefinedRoleTemplate, DefinedRole
{
    private Echo() : base("echo", new(117, 154, 102), RoleCategory.CrewmateRole, Crewmate.MyTeam, [EchoCooldownOption, EchoRangeOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    static private FloatConfiguration EchoCooldownOption = NebulaAPI.Configurations.Configuration("options.role.echo.echoCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EchoRangeOption = NebulaAPI.Configurations.Configuration("options.role.echo.echoRange", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static public Echo MyRole = new Echo();
    static private GameStatsEntry StatsPlayers = NebulaAPI.CreateStatsEntry("stats.echo.players", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsActions = NebulaAPI.CreateStatsEntry("stats.echo.actions", GameStatsCategory.Roles, MyRole);
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EchoButton.png", 115f);
        static private Image iconSprite = SpriteLoader.FromResource("Nebula.Resources.EchoIcon.png", 160f);

        public Instance(GamePlayer player, int[] argument) : base(player)
        {
        }

        IEnumerator CoEcho(Vector2 position)
        {
            EditableBitMask<GamePlayer> pMask = BitMasks.AsPlayer();
            float radious = 0f;
            var circle = EffectCircle.SpawnEffectCircle(null, MyPlayer.Position.ToUnityVector(), MyRole.UnityColor, 0f, null, true);
            Bind(new GameObjectBinding(circle.gameObject));
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

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var searchButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                searchButton.SetSprite(buttonSprite.GetSprite());
                searchButton.Availability = (button) => MyPlayer.CanMove;
                searchButton.Visibility = (button) => !MyPlayer.IsDead;
                searchButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.StartCoroutine(CoEcho(MyPlayer.Position).WrapToIl2Cpp());
                    button.StartCoolDown();
                };
                searchButton.CoolDownTimer = Bind(new Timer(0f, EchoCooldownOption).SetAsAbilityCoolDown().Start());
                searchButton.SetLabel("echo");
            }
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

