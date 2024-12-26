using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;

namespace Nebula.Modules.ScriptComponents;

internal class VanillaKillButtonHandler : IKillButtonLike
{
    private KillButton killButton;

    public VanillaKillButtonHandler(KillButton killButton)
    {
        this.killButton = killButton;
    }

    float IKillButtonLike.Cooldown => PlayerControl.LocalPlayer.killTimer;

    bool ILifespan.IsDeadObject => false;

    void IKillButtonLike.SetCooldown(float cooldown)
    {
        PlayerControl.LocalPlayer.killTimer = cooldown;
    }

    void IKillButtonLike.StartCooldown()
    {
        PlayerControl.LocalPlayer.SetKillTimer(AmongUsUtil.VanillaKillCoolDown);
    }
}

internal class ModKillButtonHandler : IKillButtonLike
{
    private Virial.Components.ModAbilityButton button;
    public ModKillButtonHandler(Virial.Components.ModAbilityButton button)
    {
        this.button = button;
    }

    float IKillButtonLike.Cooldown => (button.GetCoolDownTimer() as GameTimer)?.CurrentTime ?? 0f;

    bool ILifespan.IsDeadObject => button.IsDeadObject;

    void IKillButtonLike.SetCooldown(float cooldown)
    {
        button.GetCoolDownTimer()?.Start(cooldown);
    }

    void IKillButtonLike.StartCooldown()
    {
        button.StartCoolDown();
    }
}