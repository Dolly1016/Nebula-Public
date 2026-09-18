using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

public class MeetingFixExiledHostEvent : Virial.Events.Event
{
    private byte exiled;
    private byte[] exiledAll;
    internal byte RawExiled => exiled;
    internal byte[] RawExiledAll => exiledAll;
    internal bool CanBeTie { get; private set; } = true;

    internal MeetingFixExiledHostEvent(byte exiled, byte[] exiledAll)
    {
        this.exiled = exiled;
        this.exiledAll = exiledAll;
    }

    /// <summary>
    /// 現在、追放されることになっている場合trueを返します。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool WillBeExiled(Virial.Game.Player? player) => player == null ? false : exiledAll.Contains(player.PlayerId);

    /// <summary>
    /// 追放されるプレイヤーを返します。
    /// 誰も追放されない場合、空の配列が返ります。
    /// </summary>
    public Virial.Game.Player[] Exiled => this.exiledAll.Select(id => Virial.Game.Player.GetPlayer(id)!).ToArray();

    /// <summary>
    /// 追放されるプレイヤーを変更します。
    /// </summary>
    /// <param name="exiled"></param>
    public void SetExiledPlayers(Virial.Game.Player?[]? exiled)
    {
        if (exiled == null || exiled.Length == 0)
        {
            this.exiled = byte.MaxValue;
            this.exiledAll = [];
            return;
        }

        var list = exiled.Where(p => p != null).ToArray();
        if(list.Length == 0)
        {
            this.exiled = byte.MaxValue;
            this.exiledAll = [];
            return;
        }

        this.exiled = list.First()!.PlayerId;
        this.exiledAll = list.Select(p => p.PlayerId).Distinct().ToArray();
        this.CanBeTie = false;
    }
}
