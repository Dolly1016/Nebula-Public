using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Behavior;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Doctor : DefinedUsurpableAdvancedRoleTemplate<Doctor.Ability, Doctor.UsurpedAbility>, DefinedRole
{

    private Doctor() : base("doctor", new(128,255,221),RoleCategory.CrewmateRole, Crewmate.MyTeam, [PortableVitalsChargeOption, MaxPortableVitalsChargeOption, ChargesPerTasksOption]) { }

    public override Ability CreateAbility(Virial.Game.Player player, int[] arguments) => new(player, arguments.GetAsBool(0), (float)arguments.Get(1, (int)(PortableVitalsChargeOption * 10)) / 10f);
    public override UsurpedAbility CreateUsurpedAbility(Virial.Game.Player player, int[] arguments) => new(player, arguments.GetAsBool(0));

    static private readonly FloatConfiguration PortableVitalsChargeOption = NebulaAPI.Configurations.Configuration("options.role.doctor.portableVitalsCharge", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration MaxPortableVitalsChargeOption = NebulaAPI.Configurations.Configuration("options.role.doctor.maxPortableVitalsCharge", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration ChargesPerTasksOption = NebulaAPI.Configurations.Configuration("options.role.doctor.chargesPerTasks", (0.5f, 10f, 0.5f), 1f, FloatConfigurationDecorator.Second);

    static public readonly Doctor MyRole = new();

    internal static VitalsMinigame OpenSpecialVitalsMinigame()
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
        if (vitalsMinigame == null) return null!;
        vitalsMinigame.transform.SetParent(Camera.main.transform, false);
        vitalsMinigame.transform.localPosition = new Vector3(0.0f, 0.0f, -50f);
        vitalsMinigame.Begin(null);

        ConsoleTimer.MarkAsNonConsoleMinigame();

        return vitalsMinigame;
    }

    public class UsurpedAbility : AbstractPlayerUsurpableAbility, IGameOperator, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public UsurpedAbility(GamePlayer player, bool isUsurped) : base(player, isUsurped) {
            if (AmOwner)
            {
                var sprite = HudManager.Instance.UseButton.fastUseSettings[ImageNames.VitalsButton].Image;
                var vitalButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, 0f, "vital", new WrapSpriteLoader(() => sprite));
                vitalButton.OnClick = (button) =>
                {
                    VitalsMinigame? vitalsMinigame = OpenSpecialVitalsMinigame();

                    ConsoleTimer.MarkAsNonConsoleMinigame();

                    IEnumerator CoUpdateState(VitalsPanel panel, GamePlayer player)
                    {
                        TMPro.TextMeshPro text = UnityEngine.Object.Instantiate(vitalsMinigame.SabText, panel.transform);
                        UnityEngine.Object.DestroyImmediate(text.GetComponent<AlphaBlink>());
                        text.gameObject.SetActive(false);
                        text.transform.localScale = Vector3.one * 0.5f;
                        text.transform.localPosition = new Vector3(-0.75f, -0.23f, 0f);
                        text.color = new Color(0.8f, 0.8f, 0.8f);

                        while (true)
                        {

                            if (panel.IsDiscon)
                            {
                                text.gameObject.SetActive(true);
                                text.text = player.PlayerState.Text;
                            }
                            else
                            {
                                text.gameObject.SetActive(false);
                            }
                            yield return null;
                        }
                    }
                    vitalsMinigame.vitals.Do(panel =>
                    {
                        panel.StartCoroutine(CoUpdateState(panel, NebulaGameManager.Instance!.GetPlayer(panel.PlayerInfo.PlayerId)!));
                    });
                };
                vitalButton.SetLabelType(ModAbilityButton.LabelType.Utility);
                vitalButton.SetAsUsurpableButton(this);
            }
        }
    }

    public class Ability : AbstractPlayerUsurpableAbility, IGameOperator, IUsurpableAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), (int)(vitalTimer * 10f)];
        private float vitalTimer;

        public Ability(GamePlayer player, bool isUsurped, float vitalTimer) : base(player, isUsurped)
        {
            this.vitalTimer = vitalTimer;

            StaticAchievementToken? acTokenCommon = null;
            StaticAchievementToken? acTokenChallenge = null;

            if (AmOwner)
            {
                var sprite = HudManager.Instance.UseButton.fastUseSettings[ImageNames.VitalsButton].Image;
                var vitalButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, 0f, "vital", new WrapSpriteLoader(() => sprite), _ => vitalTimer > 0f, null);
                vitalButton.OnClick = (button) =>
                {
                    VitalsMinigame? vitalsMinigame = OpenSpecialVitalsMinigame();

                    ConsoleTimer.MarkAsNonConsoleMinigame();

                    IEnumerator CoUpdateState(VitalsPanel panel, GamePlayer player)
                    {
                        TMPro.TextMeshPro text = UnityEngine.Object.Instantiate(vitalsMinigame.SabText, panel.transform);
                        UnityEngine.Object.DestroyImmediate(text.GetComponent<AlphaBlink>());
                        text.gameObject.SetActive(false);
                        text.transform.localScale = Vector3.one * 0.5f;
                        text.transform.localPosition = new Vector3(-0.75f, -0.23f, 0f);
                        text.color = new Color(0.8f, 0.8f, 0.8f);

                        while (true)
                        {

                            if (panel.IsDiscon)
                            {
                                text.gameObject.SetActive(true);
                                text.text = player.PlayerState.Text;
                            }
                            else
                            {
                                text.gameObject.SetActive(false);
                            }
                            yield return null;
                        }
                    }
                    vitalsMinigame.vitals.Do(panel =>
                    {
                        panel.StartCoroutine(CoUpdateState(panel, NebulaGameManager.Instance!.GetPlayer(panel.PlayerInfo.PlayerId)!));
                    });

                    vitalsMinigame.BatteryText.gameObject.SetActive(true);
                    vitalsMinigame.BatteryText.transform.localPosition = new Vector3(2.2f, -2.45f, 0f);
                    foreach (var sprite in vitalsMinigame.BatteryText.gameObject.GetComponentsInChildren<SpriteRenderer>()) sprite.transform.localPosition = new Vector3(-0.45f, 0f);

                    IEnumerator CoUpdate()
                    {
                        acTokenCommon ??= new("doctor.common1");

                        int lastAliveCount = NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead);

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
                        if (lastAliveCount != NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead))
                            acTokenChallenge = new("doctor.challenge");


                        if (vitalsMinigame.amClosing != Minigame.CloseState.Closing) vitalsMinigame.Close();
                    }

                    vitalsMinigame.StartCoroutine(CoUpdate().WrapToIl2Cpp());
                };
                vitalButton.SetLabelType(ModAbilityButton.LabelType.Utility);
                vitalButton.SetAsUsurpableButton(this);
            }
        }

        void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            vitalTimer = Mathf.Min(MaxPortableVitalsChargeOption, vitalTimer + ChargesPerTasksOption);
        }
    }
}
