using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using System.Runtime.CompilerServices;
using TMPro;
using Virial;
using Virial.Assignable;
using Virial.Game;
using Virial.Media;
using static Nebula.Modules.MetaWidgetOld;

namespace Nebula.VoiceChat;
public interface IVoiceComponent
{
    //可聴範囲 (リスナーとして)
    public float Radious { get; }

    //音声に掛ける倍率 (スピーカーとして)
    public float Volume { get; }

    public Vector2 Position { get; }

    /// <summary>
    /// 与えられたマイクからの音声を再生するかどうか返します。
    /// </summary>
    /// <param name="mic">音声を再生する場合、true</param>
    /// <returns></returns>
    public bool CanPlaySoundFrom(IVoiceComponent mic);

    /// <summary>
    /// 音声に係数を乗じたうえで聴き取ります。
    /// </summary>
    /// <param name="speaker"></param>
    /// <returns></returns>
    public float CanCatch(GamePlayer player, Vector2 position)
    {
        float dis = position.Distance(Position);
        if (dis < Radious) return 1f - dis / Radious;
        return 0f;
    }
}

