using Nebula.Roles.Ghost.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial;
using Nebula.Roles.Crewmate;
using Nebula.Roles.Abilities;
using Virial.Events.Player;
using Virial.Events.Game;

namespace Nebula.Roles.Ghost.Impostor;

public class Clog : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Clog() : base("clog", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, [GhostDurationOption, NumOfGhostsOption, GhostSizeOption]) {
        MetaAbility.RegisterCircle(new("role.clog.ghostSize", () => GhostSizeOption, () => null, UnityColor));
    }

    string ICodeName.CodeName => "CLG";

    static private FloatConfiguration GhostDurationOption = NebulaAPI.Configurations.Configuration("options.role.clog.ghostDuration", (10f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration NumOfGhostsOption = NebulaAPI.Configurations.Configuration("options.role.clog.numOfGhost", (1, 20), 3);
    static private FloatConfiguration GhostSizeOption = NebulaAPI.Configurations.Configuration("options.role.clog.ghostSize", (0.125f, 1.5f, 0.125f), 0.5f, FloatConfigurationDecorator.Ratio);

    static public Clog MyRole = new Clog();
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player) { }

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.GhostButton.png", 115f);

        private AchievementToken<bool>? acTokenChallenge = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("clog.common2", false, (val, _) => val && NebulaGameManager.Instance?.EndState?.EndReason == Virial.Game.GameEndReason.Sabotage);
                GameOperatorManager.Instance?.Register<PlayerTaskRemoveLocalEvent>(ev => {
                    try
                    {
                        if (ev.Task.TryCast<SabotageTask>()) acTokenChallenge.Value = false;
                    }catch (Exception ex) { }
                }, this);

                int left = NumOfGhostsOption.GetValue();
                var ghostButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                ghostButton.SetSprite(buttonSprite.GetSprite());
                ghostButton.Availability = (button) => MyPlayer.CanMove;
                ghostButton.Visibility = (button) => MyPlayer.IsDead;
                ghostButton.OnClick = (button) =>
                {
                    RpcSpawnGhost.Invoke(MyPlayer.VanillaPlayer.transform.position);

                    new StaticAchievementToken("clog.common1");
                    if (AmongUsUtil.InAnySab) acTokenChallenge.Value = true;

                    left--;
                    if(left > 0)
                        ghostButton.ShowUsesIcon(0).text = left.ToString();
                    else
                        ghostButton.ReleaseIt();
                };
                ghostButton.ShowUsesIcon(0).text = left.ToString();
                ghostButton.CoolDownTimer = Bind(new Timer(0f, 10f).SetAsAbilityCoolDown().Start());
                ghostButton.SetLabel("ghost");
            }
        }
    }

    static public RemoteProcess<Vector2> RpcSpawnGhost = new(
        "GhostGhost", (pos, calledBeMe) =>
        {
            var ghost = new Nebula.Roles.Crewmate.Ghost(pos, GhostDurationOption, null, Seer.CanSeeGhostsInShadowOption, GhostSizeOption);
            if (calledBeMe)
            {
                bool achieved = false;
                GameOperatorManager.Instance?.Register<GameUpdateEvent>(ev => { 
                    if(!achieved && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && !p.AmOwner && p.IsImpostor && pos.Distance(p.Position) < 0.75f))
                    {
                        new StaticAchievementToken("clog.another1");
                        achieved = true;
                    }
                }, ghost);
            }
        });
}
