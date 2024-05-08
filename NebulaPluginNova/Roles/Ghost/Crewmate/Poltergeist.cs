using Virial.Assignable;

namespace Nebula.Roles.Ghost.Crewmate;

[NebulaRPCHolder]
public class Poltergeist : ConfigurableStandardGhostRole
{
    static public Poltergeist MyRole = new Poltergeist();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "poltergeist";
    public override string CodeName => "PLT";
    public override Color RoleColor => new Color(210f / 255f, 220f / 255f, 234f / 255f);

    private NebulaConfiguration PoltergeistCoolDownOption = null!;

    public override GhostRoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    protected override void LoadOptions()
    {
        base.LoadOptions();
        PoltergeistCoolDownOption = new(RoleConfig, "poltergeistCoolDown", null, 5f, 30f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public class Instance : GhostRoleInstance
    {
        public override AbstractGhostRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.PoltergeistButton.png", 115f);

        public override void OnActivated()
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
                poltergeistButton.CoolDownTimer = Bind(new Timer(0f, MyRole.PoltergeistCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                poltergeistButton.SetLabel("poltergeist");
            }
        }
    }

    static private IEnumerator CoMoveDeadBody(DeadBody? deadBody, Vector2 pos)
    {
        if (deadBody == null) yield break;

        float p = 0f;
        Vector2 beginPos = deadBody.transform.position;

        while (true)
        {
            p += Time.deltaTime * 0.85f;
            if (!(p < 1f)) break;

            float pp = p * p;
            Vector3 currentPos = beginPos * (1 - pp) + pos * pp;
            currentPos.z = currentPos.y / 1000f;
            deadBody.transform.position = currentPos;

            yield return null;
        }

        deadBody.transform.position = pos;

        yield break;
    }

    static public RemoteProcess<(byte playerId,Vector2 pos)> RpcPoltergeist = new(
        "Poltergeist", (message, _) => NebulaManager.Instance.StartCoroutine(CoMoveDeadBody(Helpers.GetDeadBody(message.playerId), message.pos).WrapToIl2Cpp()));
}

