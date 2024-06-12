using AmongUs.GameOptions;
using Nebula.Behaviour;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Doctor : DefinedRoleTemplate, DefinedRole
{

    private Doctor() : base("doctor", new(128,255,221),RoleCategory.CrewmateRole, Crewmate.MyTeam, [PortableVitalsChargeOption, MaxPortableVitalsChargeOption, ChargesPerTasksOption]) { }
    

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration PortableVitalsChargeOption = NebulaAPI.Configurations.Configuration("options.role.doctor.portableVitalsCharge", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MaxPortableVitalsChargeOption = NebulaAPI.Configurations.Configuration("options.role.doctor.maxPortableVitalsCharge", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration ChargesPerTasksOption = NebulaAPI.Configurations.Configuration("options.role.doctor.chargesPerTasks", (0.5f, 10f, 0.5f), 1f, FloatConfigurationDecorator.Second);

    static public Doctor MyRole = new Doctor();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? vitalButton = null;
        private float vitalTimer = PortableVitalsChargeOption;

        public Instance(GamePlayer player) : base(player)
        {
        }

        void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            vitalTimer = Mathf.Min(MaxPortableVitalsChargeOption, vitalTimer + ChargesPerTasksOption);
        }

        void RuntimeAssignable.OnActivated()
        {
            StaticAchievementToken? acTokenCommon = null;
            StaticAchievementToken? acTokenChallenge = null;

            if (AmOwner)
            {
                vitalButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                vitalButton.SetSprite(HudManager.Instance.UseButton.fastUseSettings[ImageNames.VitalsButton].Image);
                vitalButton.Availability = (button) => MyPlayer.CanMove && vitalTimer > 0f;
                vitalButton.Visibility = (button) => !MyPlayer.IsDead;
                vitalButton.OnClick = (button) =>
                {
                    VitalsMinigame? vitalsMinigame = null;
                    foreach (RoleBehaviour role in RoleManager.Instance.AllRoles)
                    {
                        if (role.Role == RoleTypes.Scientist)
                        {
                            vitalsMinigame = UnityEngine.Object.Instantiate(role.gameObject.GetComponent<ScientistRole>().VitalsPrefab, Camera.main.transform, false);
                            break;
                        }
                    }
                    if (vitalsMinigame == null) return;
                    vitalsMinigame.transform.SetParent(Camera.main.transform, false);
                    vitalsMinigame.transform.localPosition = new Vector3(0.0f, 0.0f, -50f);
                    vitalsMinigame.Begin(null);

                    ConsoleTimer.MarkAsNonConsoleMinigame();

                    vitalsMinigame.BatteryText.gameObject.SetActive(true);
                    vitalsMinigame.BatteryText.transform.localPosition = new Vector3(2.2f, -2.45f, 0f);
                    foreach (var sprite in vitalsMinigame.BatteryText.gameObject.GetComponentsInChildren<SpriteRenderer>()) sprite.transform.localPosition = new Vector3(-0.45f, 0f);

                    IEnumerator CoUpdate()
                    {
                        acTokenCommon ??= new("doctor.common1");

                        int lastAliveCount = NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead);

                        while (vitalsMinigame.amClosing != Minigame.CloseState.Closing)
                        {
                            vitalTimer -= Time.deltaTime;
                            if (vitalTimer < 0f)
                            {
                                vitalsMinigame.BatteryText.gameObject.SetActive(false);
                                break;
                            }

                            vitalsMinigame.BatteryText.text = Language.Translate("role.doctor.gadgetLeft").Replace("%SECOND%", string.Format("{0:f1}", vitalTimer));

                            yield return null;
                        }

                        //生存者の数に変化がある
                        if(lastAliveCount != NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead))
                            acTokenChallenge = new("doctor.challenge");
                        

                        if (vitalsMinigame.amClosing != Minigame.CloseState.Closing) vitalsMinigame.Close();
                    }

                    vitalsMinigame.StartCoroutine(CoUpdate().WrapToIl2Cpp());
                };
                vitalButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Utility);
                vitalButton.SetLabel("vital");
            }
        }
    }
}
