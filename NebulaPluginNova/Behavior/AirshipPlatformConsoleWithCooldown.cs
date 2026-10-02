using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Behavior;

internal class AirshipPlatformConsoleWithCooldown : PlatformConsole
{
    static AirshipPlatformConsoleWithCooldown()
    {
        ClassInjector.RegisterTypeInIl2Cpp<AirshipPlatformConsoleWithCooldown>(new RegisterTypeOptions()
        {
            Interfaces = new[] { typeof(IUsable), typeof(IUsableCoolDown) }
        });
    }

    internal void SetUp(PlatformConsole originalConsole)
    {
        this.usableDistance = originalConsole.usableDistance;
        this.Image = originalConsole.Image;
        this.Platform = originalConsole.Platform;
        this.MaxCoolDown = GeneralConfigurations.PlatformCooldownOption;
    }

    public override float PercentCool => this.CoolDown / this.MaxCoolDown;

    public float CoolDown { get; set; }

    public float MaxCoolDown { get; private set; }

    public bool IsCoolingDown() => this.CoolDown > 0f;

    private void Update()
    {
        this.CoolDown = Mathn.Max(this.CoolDown - FastMethods.GetDeltaTimeFast(), 0f);
    }
}

