using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;
using Virial.Events.Game;

namespace Nebula.Roles.Perks;

internal class Monoway : PerkFunctionalInstance
{
    const float MonowayCooldown = 30f;
    const float RotateCooldown = 70f;
    const float InitialCooldown = 10f;
    const float MonowayDuration = 10f;
    const float RotateDuration = 25f;

    private float duration;

    static PerkFunctionalDefinition DefX = new("monowayX", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("monowayX", 3, 50, new(106, 111, 173), Virial.Color.ImpostorColor).CooldownText("%CD%", ()=>MonowayCooldown).DurationText("%D%", () => MonowayDuration), (def, instance) => new Monoway(def, instance, () => new(1f, 0f, 0f, 0f), IconType.YInvalid, MonowayCooldown, MonowayDuration));
    static PerkFunctionalDefinition DefY = new("monowayY", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("monowayY", 3, 49, new(173, 108, 108), Virial.Color.ImpostorColor).CooldownText("%CD%", () => MonowayCooldown).DurationText("%D%", () => MonowayDuration), (def, instance) => new Monoway(def, instance, () => new(0f, 0f, 0f, 1f), IconType.XInvalid, MonowayCooldown, MonowayDuration));
    static PerkFunctionalDefinition DefRotate = new("monowayR", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("monowayRotate", 3, 54, new(72,114,85), Virial.Color.ImpostorColor).CooldownText("%CD%", () => RotateCooldown).DurationText("%D%", () => RotateDuration), (def, instance) => new Monoway(def, instance, () => Helpers.Prob(0.5f) ? new(0f, 1f, -1f, 0f) : new(0f, -1f, 1f, 0f), IconType.Rotate, RotateCooldown, RotateDuration));
    private Func<Vector4> speedVec;
    private IconType iconType;
    private Monoway(PerkDefinition def, PerkInstance instance, Func<Vector4> speedVector, IconType iconType, float cooldown, float duration) : base(def, instance)
    {
        cooldownTimer = NebulaAPI.Modules.Timer(this, cooldown);
        cooldownTimer.Start(InitialCooldown);
        cooldownTimer.SetCondition(() => !MeetingHud.Instance && !ExileController.Instance);
        PerkInstance.BindTimer(cooldownTimer);
        this.speedVec = speedVector;
        this.iconType = iconType;
        this.duration = duration;
    }

    private GameTimer cooldownTimer;

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing || MyPlayer.IsDead) return;

        RpcEffect.Invoke((iconType, speedVec.Invoke(), duration));

        cooldownTimer.Start();
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
    }

    static IDividedSpriteLoader IconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.MovementIcon.png", 100f, 3, 1);
    static Image InvalidSprite = SpriteLoader.FromResource("Nebula.Resources.InvalidIcon.png", 100f);

    private static RemoteProcess<(IconType iconType, Vector4 vec, float duration)> RpcEffect = new("Monoway", (message, calledByMe) => {
        NebulaManager.Instance.StartCoroutine(CoAnimIcon(message.iconType, message.vec.z > 0f ? 90f : -90f).WrapToIl2Cpp());

        if (!calledByMe || (message.iconType is IconType.Rotate ? GeneralConfigurations.MovementRotationPerksAlsoAffectCastersOption : GeneralConfigurations.MovementRestrictionPerksAlsoAffectCastersOption))
        {
            NebulaManager.Instance.StartDelayAction(0.5f, () =>
            {
                if (message.iconType is IconType.Rotate)
                {
                    var speedMod = new SpeedModulator(1f, new Vector4(1f, 0f, 0f, 1f), true, message.duration, false, 0);
                    var modifier = new SpeedModulator.MatrixModifier(speedMod);
                    PlayerModInfo.RpcAttrModulator.Invoke((PlayerControl.LocalPlayer.PlayerId, speedMod, true));

                    IEnumerator CoUpdateAngle()
                    {
                        float p = 0f;
                        while (p < 1f)
                        {
                            float theta = p * Mathf.PI * 0.5f;
                            float cos = Mathf.Cos(theta);
                            float sin = Mathf.Sin(theta);
                            modifier.SetDirection(new(cos, message.vec.y * sin, message.vec.z * sin, cos));
                            p += Time.deltaTime / 0.5f;
                            yield return null;
                        }
                        modifier.SetDirection(message.vec);
                    }
                    NebulaManager.Instance.StartCoroutine(CoUpdateAngle().WrapToIl2Cpp());
                }
                else
                {
                    PlayerModInfo.RpcAttrModulator.Invoke((PlayerControl.LocalPlayer.PlayerId, new SpeedModulator(1f, message.vec, true, message.duration, false, 0), true));
                }
            });
        }
    });
    private enum IconType
    {
        Rotate,
        XInvalid,
        YInvalid
    }
    private static IEnumerator CoAnimIcon(IconType iconType, float rotateAngle = 0f)
    {
        var holder = UnityHelper.CreateObject("AnimHolder", HudManager.Instance.transform, new(0f, -1.85f, -10f));

        var display = VanillaAsset.GetPlayerDisplay();
        display.transform.SetParent(holder.transform);
        display.transform.localPosition = new(0.4f, -0.2f, -1f);
        display.transform.localScale = new(0.45f, 0.45f, 1f);
        display.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetUILayer());
        display.Cosmetics.SetColor(NebulaPlayerTab.CamouflageColorId);
        display.Animations.PlayRunAnimation();

        var currentBody = display.Cosmetics.currentBodySprite.BodySprite;

        var arrow = UnityHelper.CreateObject<SpriteRenderer>("Arrow", holder.transform, new(0f, 0f, -0.5f));
        arrow.sprite = IconSprite.GetSprite(iconType switch { IconType.Rotate => 0, IconType.XInvalid => 1, IconType.YInvalid => 2 });

        UnityEngine.Color arrowNormalColor = new(0.25f, 0.4f, 0.7f);
        UnityEngine.Color arrowToColor = new(0.85f, 0.3f, 0.3f);
        UnityEngine.Color currentArrowColor = arrowNormalColor;

        float p = 0f;
        while(p < 1f)
        {
            currentBody.color = Color.white.AlphaMultiplied(p);
            arrow.color = arrowNormalColor.AlphaMultiplied(p);
            p += Time.deltaTime / 0.35f;
            yield return null;
        }

        yield return Effects.Wait(0.12f);

        SpriteRenderer? additionalRenderer = null;
        if (iconType == IconType.Rotate)
        {
            p = 0f;
            while (p < 1f)
            {
                arrow.transform.localScale = Vector3.one * (1f + Helpers.MountainCurve(p, 0.45f));
                arrow.transform.localEulerAngles = new(0f, 0f, p * rotateAngle);
                currentArrowColor = Color.Lerp(arrowNormalColor, arrowToColor, p);
                arrow.color = currentArrowColor;
                p += Time.deltaTime / 0.45f;
                yield return null;
            }

            arrow.transform.localScale = Vector3.one;
            arrow.transform.localEulerAngles = new(0f, 0f, rotateAngle);
        }
        else
        {
            var invalidIcon = UnityHelper.CreateObject<SpriteRenderer>("InvalidIcon", holder.transform, new(0f, 0f, -1f));
            additionalRenderer = invalidIcon;
            invalidIcon.sprite = InvalidSprite.GetSprite();
            yield return Effects.Wait(0.08f);
            invalidIcon.enabled = false;
            yield return Effects.Wait(0.08f);
            invalidIcon.enabled = true;
        }

        yield return Effects.Wait(1.8f);

        p = 0f;
        while (p < 1f)
        {
            currentBody.color = Color.white.AlphaMultiplied(1f - p);
            arrow.color = currentArrowColor.AlphaMultiplied(1f - p);
            if(additionalRenderer != null) additionalRenderer.color = Color.white.AlphaMultiplied(1f - p);
            p += Time.deltaTime / 0.8f;
            yield return null;
        }

        GameObject.Destroy(holder.gameObject);
    }
}
