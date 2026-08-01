using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerInvokeSabotageHostEvent : AbstractPlayerEvent
{
    public enum SabotageType
    {
        /// <summary>
        /// 停電
        /// </summary>
        BlackOut,
        /// <summary>
        /// 通信障害
        /// </summary>
        Communication,
        /// <summary>
        /// リアクター、耐震装置、ヘリ衝突
        /// </summary>
        Reactorlike,
        /// <summary>
        /// O2サボタージュ
        /// </summary>
        O2,

        /// <summary>
        /// 非対応サボタージュ
        /// </summary>
        Others = -1,
    }

    /// <summary>
    /// 発動したサボタージュの種類
    /// </summary>
    public SabotageType Sabotage { get; }

    /// <summary>
    /// サボタージュの発動者
    /// </summary>
    public Virial.Game.Player Invoker => this.Player;

    internal PlayerInvokeSabotageHostEvent(SabotageType sabotage, Virial.Game.Player invoker) : base(invoker)
    {
        Sabotage = sabotage;
    }
}
