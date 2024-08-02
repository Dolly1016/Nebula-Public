using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Helpers;

namespace Nebula.Roles.Ghost.Crewmate;

[NebulaRPCHolder]
public class Poltergeist : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Poltergeist() : base("poltergeist", new(210, 220, 234), RoleCategory.CrewmateRole, [PoltergeistCoolDownOption]) { }

    string ICodeName.CodeName => "PLT";

    static private FloatConfiguration PoltergeistCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.poltergeist.poltergeistCoolDown", (5f, 30f, 2.5f), 20f, FloatConfigurationDecorator.Second);

    static public Poltergeist MyRole = new Poltergeist();

    

    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);


    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.PoltergeistButton.png", 115f);

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var deadBodyTracker = Bind(ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => d.RelatedDeadBody?.GetHolder() == null));

                var poltergeistButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                poltergeistButton.SetSprite(buttonSprite.GetSprite());
                poltergeistButton.Availability = (button) => MyPlayer.CanMove && deadBodyTracker.CurrentTarget != null;
                poltergeistButton.Visibility = (button) => MyPlayer.IsDead;
                poltergeistButton.OnClick = (button) =>
                {
                    RpcPoltergeist.Invoke((deadBodyTracker.CurrentTarget!.PlayerId, MyPlayer.VanillaPlayer.GetTruePosition()));
                    new StaticAchievementToken("poltergeist.common1");
                    poltergeistButton.StartCoolDown();
                };
                poltergeistButton.CoolDownTimer = Bind(new Timer(0f, PoltergeistCoolDownOption).SetAsAbilityCoolDown().Start());
                poltergeistButton.SetLabel("poltergeist");
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

