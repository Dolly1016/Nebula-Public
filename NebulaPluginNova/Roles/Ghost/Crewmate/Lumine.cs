using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Ghost.Crewmate;

[NebulaRPCHolder]
public class Lumine : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Lumine(): base("lumine", new(241, 237, 184),RoleCategory.CrewmateRole, [LightSizeOption, LightDurationOption]) {}

    string ICodeName.CodeName => "LMN";

    static private FloatConfiguration LightSizeOption = NebulaAPI.Configurations.Configuration("options.role.lumine.lightSize", (1f, 10f, 0.25f), 2f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration LightDurationOption = NebulaAPI.Configurations.Configuration("options.role.lumine.lightDuration", (5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    static public Lumine MyRole = new Lumine();
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player) {}

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LumineButton.png", 115f);

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var lumineButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                lumineButton.SetSprite(buttonSprite.GetSprite());
                lumineButton.Availability = (button) => MyPlayer.CanMove;
                lumineButton.Visibility = (button) => MyPlayer.IsDead;
                lumineButton.OnClick = (button) =>
                {
                    RpcLumineLight.Invoke(MyPlayer.VanillaPlayer.transform.position);

                    var near = Helpers.AllDeadBodies().Where(db => db.transform.position.Distance(MyPlayer.VanillaPlayer.transform.position) < 2f).ToArray();
                    if (near.Length > 0)
                        new StaticAchievementToken("lumine.common1");
                    if (near.Any(db => db.ParentId == MyPlayer.PlayerId))
                        new StaticAchievementToken("lumine.another1");

                    lumineButton.ReleaseIt();
                };
                lumineButton.ShowUsesIcon(3).text = "1";
                lumineButton.CoolDownTimer = Bind(new Timer(0f, 10f).SetAsAbilityCoolDown().Start());
                lumineButton.SetLabel("lumine");
            }
        }
    }

    static private IEnumerator CoLight(Vector2 pos)
    {
        SpriteRenderer lightRenderer = AmongUsUtil.GenerateCustomLight(pos);
        lightRenderer.transform.localScale *= LightSizeOption;

        float p = 0f;
        while (p < 1f)
        {
            p += Time.deltaTime * 0.85f;
            lightRenderer.material.color = new Color(1, 1, 1, p);
            yield return null;
        }

        lightRenderer.material.color = Color.white;
        yield return Effects.Wait(LightDurationOption);
        while (p > 0f)
        {
            p -= Time.deltaTime * 0.75f;
            lightRenderer.material.color = new Color(1, 1, 1, p);
            yield return null;
        }

        GameObject.Destroy(lightRenderer.gameObject);

        yield break;
    }

    static public RemoteProcess<Vector2> RpcLumineLight = new(
        "LumineLight", (pos, _) => NebulaManager.Instance.StartCoroutine(CoLight(pos).WrapToIl2Cpp()));
}
