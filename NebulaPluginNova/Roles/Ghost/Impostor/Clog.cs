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
using Virial.Game;

namespace Nebula.Roles.Ghost.Impostor;

public class Clog : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Clog() : base("clog", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, [GhostDurationOption, NumOfGhostsOption, GhostSizeOption]) {
        MetaAbility.RegisterCircle(new("role.clog.ghostSize", () => GhostSizeOption, () => null, UnityColor));

        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Clog.png");

        GameActionTypes.ClogInvokingGhostAction = new("clog.ghost", this, isPlacementAction: true);
    }

    string ICodeName.CodeName => "CLG";

    static private readonly FloatConfiguration GhostDurationOption = NebulaAPI.Configurations.Configuration("options.role.clog.ghostDuration", (10f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfGhostsOption = NebulaAPI.Configurations.Configuration("options.role.clog.numOfGhost", (1, 20), 3);
    static private readonly FloatConfiguration GhostSizeOption = NebulaAPI.Configurations.Configuration("options.role.clog.ghostSize", (0.125f, 1.5f, 0.125f), 0.5f, FloatConfigurationDecorator.Ratio);

    static public readonly Clog MyRole = new();
    static internal readonly GameStatsEntry StatsGhosts = NebulaAPI.CreateStatsEntry("stats.clog.ghosts", GameStatsCategory.Roles, MyRole);
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player) { }

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.GhostButton.png", 115f);

        private AchievementToken<bool>? acTokenChallenge = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("clog.common2", false, (val, _) => val && NebulaGameManager.Instance?.EndState?.EndReason == Virial.Game.GameEndReason.Sabotage);
                GameOperatorManager.Instance?.Subscribe<PlayerTaskRemoveLocalEvent>(ev => {
                    try
                    {
                        if (ev.Task.TryCast<SabotageTask>() != null) acTokenChallenge.Value = false;
                    }catch (Exception ex) { }
                }, this);

                int left = NumOfGhostsOption.GetValue();
                var ghostButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    10f, "ghost", buttonSprite, null, _ => left > 0, true);
                ghostButton.OnClick = (button) =>
                {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.ClogInvokingGhostAction);

                    RpcSpawnGhost.Invoke(MyPlayer.VanillaPlayer.transform.position);
                    StatsGhosts.Progress();

                    new StaticAchievementToken("clog.common1");
                    if (AmongUsUtil.InAnySab) acTokenChallenge.Value = true;

                    left--;
                    if(left > 0) ghostButton.UpdateUsesIcon(left.ToString());
                };
                ghostButton.ShowUsesIcon(0, left.ToString());
            }
        }
    }

    static public readonly RemoteProcess<Vector2> RpcSpawnGhost = new(
        "GhostGhost", (pos, calledBeMe) =>
        {
            var ghost = new Nebula.Roles.Crewmate.Ghost(pos, GhostDurationOption, null, Seer.CanSeeGhostsInShadowOption, GhostSizeOption);
            if (calledBeMe)
            {
                bool achieved = false;
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => { 
                    if(!achieved && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && !p.AmOwner && p.IsImpostor && pos.Distance(p.Position) < 0.75f))
                    {
                        new StaticAchievementToken("clog.another1");
                        achieved = true;
                    }
                }, ghost);
            }
        });
}
