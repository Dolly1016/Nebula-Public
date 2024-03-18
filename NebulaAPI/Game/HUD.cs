using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Virial.Game;

/// <summary>
/// ヘッドアップディスプレイ。
/// おもに表示されているGUIコンテンツを管理します。
/// </summary>
public interface HUD
{
    /// <summary>
    /// 画面上部に表示するコンテキストを登録します。
    /// </summary>
    /// <param name="bindedLifespan"></param>
    /// <param name="widget"></param>
    //void RegisterUpperWidget(ILifespan bindedLifespan, GUIWidget widget);
}

