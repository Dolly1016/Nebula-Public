using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;
using Virial.Game;
using Virial;

namespace Nebula.Roles.Abilities;

internal class DebugAbility : DependentLifespan, IGameOperator
{
    public DebugAbility()
    {
        DebugScreen.Push(new FunctionalDebugTextContent(() =>
        {
            if (!NebulaInput.GetKey(KeyCode.RightShift)) return null;

            var pos = GamePlayer.LocalPlayer.TruePosition;
            return 
            "Room(Simple): " + AmongUsUtil.GetRoomName(pos, false) + "<br>" +
            "Room(Detail): " + AmongUsUtil.GetRoomName(pos, true) + "<br>" +
            "Room(Simple Short): " + AmongUsUtil.GetRoomName(pos, false, true) + "<br>" +
            "Room(Detail Short): " + AmongUsUtil.GetRoomName(pos, true, true);
        }, this));
    }

    Vector2? pos1 = null, pos2 = null;
    void LocalUpdate(GameHudUpdateEvent ev)
    {
        if (NebulaInput.GetKeyDown(KeyCode.Alpha3))
        {
            pos1 = GamePlayer.LocalPlayer!.Position;
            DebugScreen.Push(new FunctionalDebugTextContent(() => "続けてもう一方を選択。", FunctionalLifespan.GetTimeLifespan(1.2f)));
        }

        if (NebulaInput.GetKeyDown(KeyCode.Alpha4))
        {
            if (pos1 == null)
            {
                pos1 = GamePlayer.LocalPlayer!.Position;
                DebugScreen.Push(new FunctionalDebugTextContent(() => "続けてもう一方を選択。", FunctionalLifespan.GetTimeLifespan(1.2f)));
            }
            else
            {
                pos2 = GamePlayer.LocalPlayer!.Position;

                Vector2 center = (pos1.Value + pos2.Value) * 0.5f;
                Vector2 diff = (pos1.Value - pos2.Value) * 0.5f;

                ClipboardHelper.PutClipboardString("new(" + center.x.ToString("F2") + "f, " + center.y.ToString("F2") + "f, " + Mathf.Abs(diff.x).ToString("F2") + "f, " + Mathf.Abs(diff.y).ToString("F2") + "f)");
                DebugScreen.Push(new FunctionalDebugTextContent(() => "範囲オブジェクトの生成式をコピーしました。", FunctionalLifespan.GetTimeLifespan(2f)));

                pos1 = null;
                pos2 = null;
            }
        }
    }
}
