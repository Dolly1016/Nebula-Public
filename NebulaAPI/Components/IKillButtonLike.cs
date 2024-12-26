using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Components;

/// <summary>
/// キルボタンに類するボタンを表します。
/// シェリフやジャッカル等のキルボタンが含まれます。
/// </summary>
public interface IKillButtonLike : ILifespan
{
    float Cooldown { get; }
    void SetCooldown(float cooldown);
    void StartCooldown();
}
