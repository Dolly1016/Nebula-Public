using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Ghost.Crewmate;

[NebulaRPCHolder]
public class Poltergeist : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Poltergeist() : base("poltergeist", new(210, 220, 234), RoleCategory.CrewmateRole, [PoltergeistCoolDownOption]) { }

    string ICodeName.CodeName => "PLT";

    static private readonly FloatConfiguration PoltergeistCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.poltergeist.poltergeistCoolDown", (5f, 30f, 2.5f), 20f, FloatConfigurationDecorator.Second);

    static public readonly Poltergeist MyRole = new();
    static internal readonly GameStatsEntry StatsPoltergeist = NebulaAPI.CreateStatsEntry("stats.poltergeist.poltergeist", GameStatsCategory.Roles, MyRole);


    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);


    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.PoltergeistButton.png", 115f);

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var deadBodyTracker = ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => d.RelatedDeadBody?.GetHolder() == null).Register(this);

                var poltergeistButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    PoltergeistCoolDownOption, "poltergeist", buttonSprite, _ => deadBodyTracker.CurrentTarget != null, null, true);
                poltergeistButton.OnClick = (button) =>
                {
                    RpcPoltergeist.Invoke((deadBodyTracker.CurrentTarget!.PlayerId, MyPlayer.VanillaPlayer.GetTruePosition()));
                    new StaticAchievementToken("poltergeist.common1");
                    StatsPoltergeist.Progress();
                    poltergeistButton.StartCoolDown();
                };
            }
        }
    }

    static private IEnumerator CoMoveDeadBody(DeadBody? deadBody, Vector2 pos)
    {
        if (deadBody == null) yield break;

        float p = 0f;
        Vector2 beginPos = deadBody.transform.position;

        while (deadBody)
        {
            p += Time.deltaTime * 0.85f;
            if (!(p < 1f)) break;

            float pp = p * p;
            Vector3 currentPos = beginPos * (1 - pp) + pos * pp;
            currentPos.z = currentPos.y / 1000f;
            deadBody.transform.position = currentPos;

            yield return null;
        }

        if(deadBody) deadBody.transform.position = pos.AsVector3(pos.y / 1000f);

        yield break;
    }

    static public RemoteProcess<(byte playerId,Vector2 pos)> RpcPoltergeist = new(
        "Poltergeist", (message, _) => NebulaManager.Instance.StartCoroutine(CoMoveDeadBody(Helpers.GetDeadBody(message.playerId), message.pos).WrapToIl2Cpp()));
}

