using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;

namespace Nebula.Roles.Perks;

internal class Acceleration : PerkFunctionalInstance
{
    static private float Cooldown => CooldownOption;
    const float InitialCooldown = 10f;
    static private float Duration => DurationOption;
    static private float Rate => RateOption;

    static FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration("perk.acceleration.cooldown", (0f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration("perk.acceleration.duration", (float[])[2f,3f,4f,5f,6f,7f,8f,9f,10f,12.5f,15f,20f,25f,30f], 5f, FloatConfigurationDecorator.Second);
    static FloatConfiguration RateOption = NebulaAPI.Configurations.Configuration("perk.acceleration.rate", (1.125f, 2.5f, 0.125f), 1.5f, FloatConfigurationDecorator.Ratio);
    static PerkFunctionalDefinition Def = new("acceleration", PerkFunctionalDefinition.Category.Standard,
            new PerkDefinition("acceleration", 3, 0, Virial.Color.CrewmateColor).CooldownText("%CD%", () => Cooldown).DurationText("%D%", () => Duration).RateText("%R%", () => Rate),
            (def, instance) => new Acceleration(def, instance),
            [CooldownOption, DurationOption, RateOption]);

    public Acceleration(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        cooldownTimer = NebulaAPI.Modules.Timer(this, Cooldown);
        cooldownTimer.Start(InitialCooldown);
        PerkInstance.BindTimer(cooldownTimer);
    }

    private GameTimer cooldownTimer;

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing) return;

        MyPlayer.GainSpeedAttribute(Rate, Duration, false, 0, "perkAccel");
        cooldownTimer.Start();
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        cooldownTimer.Start(InitialCooldown);
    }
}