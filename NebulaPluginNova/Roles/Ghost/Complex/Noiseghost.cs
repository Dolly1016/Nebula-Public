using Nebula.Roles.Ghost.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial;
using Virial.Game;

namespace Nebula.Roles.Ghost.Complex;

[NebulaRPCHolder]
public class Noiseghost : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Noiseghost() : base("noiseghost", new(216, 199, 232), RoleCategory.CrewmateRole | RoleCategory.ImpostorRole | RoleCategory.NeutralRole, [Nebula.Roles.Crewmate.Noisemaker.NoiseDurationOption, NumOfNoiseOption, NoiseCooldownOption]) { }

    string ICodeName.CodeName => "NGH";

    static private readonly IntegerConfiguration NumOfNoiseOption = NebulaAPI.Configurations.Configuration("options.role.noiseghost.numOfNoise", (1, 10), 1);
    static private readonly FloatConfiguration NoiseCooldownOption = NebulaAPI.Configurations.Configuration("options.role.noiseghost.noiseCooldown", (5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second, () => NumOfNoiseOption >= 2);

    static public readonly Noiseghost MyRole = new();
    static internal readonly GameStatsEntry StatsAlert = NebulaAPI.CreateStatsEntry("stats.noiseghost.alert", GameStatsCategory.Roles, MyRole);
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player) { }

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.GhostNoiseButton.png", 115f);

        string RuntimeAssignable.DisplayColoredName => (MyRole as DefinedAssignable).DisplayName.Color(MyPlayer.IsImpostor ? NebulaTeams.ImpostorTeam.UnityColor : MyRole.UnityColor);
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                int left = NumOfNoiseOption;
                var noiseButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, NoiseCooldownOption,
                    "noise", buttonSprite, null, _ => left >= 0, true);
                
                noiseButton.ShowUsesIcon(3, left.ToString());
                noiseButton.OnClick = (button) =>
                {
                    new StaticAchievementToken("noiseghost.common1");
                    new StaticAchievementToken("noiseghost.common2");
                    StatsAlert.Progress();
                    RpcGhostNoise.Invoke((MyPlayer, MyPlayer.VanillaPlayer.transform.position));
                    left--;

                    if (left > 0) noiseButton.UpdateUsesIcon(left.ToString());

                    noiseButton.StartCoolDown();
                    
                };
            }
        }
    }

    static public RemoteProcess<(GamePlayer, Vector2)> RpcGhostNoise = new(
        "GhostNoise", (message, _) => {
            if (!GamePlayer.LocalPlayer.Role.IgnoreNoisemakerNotification)
            {
                var arrow = AmongUsUtil.InstantiateNoisemakerArrow(message.Item2, true);
                arrow.arrow.SetDuration(Nebula.Roles.Crewmate.Noisemaker.NoiseDurationOption);
                if (message.Item1.AmOwner) arrow.arrow.alwaysMaxSize = true;
            }
        });
}
