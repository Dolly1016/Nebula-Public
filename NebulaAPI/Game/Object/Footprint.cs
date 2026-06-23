using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;

namespace Virial.Game.Object;

public interface FootprintState : IGenericModule<Game>
{
    /// <summary>
    /// 不可視な足跡のタグを追加します。
    /// </summary>
    /// <param name="attributeTag">対象のアトリビュートタグ</param>
    /// <param name="lifespan">不可視にする期間</param>
    void SetAsInvisible(string attributeTag, ILifespan lifespan);

    /// <summary>
    /// 可視性を取得します。
    /// </summary>
    /// <param name="attributeTag">対象のアトリビュートタグ</param>
    /// <returns>可視な場合、true。</returns>
    internal bool CheckVisibility(string attributeTag);
}
