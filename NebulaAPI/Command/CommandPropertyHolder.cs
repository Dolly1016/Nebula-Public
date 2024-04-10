using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Command;

public interface ICommandPropertyHolder
{
    /// <summary>
    /// プロパティを取得します。
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    ICommandToken? GetCommandProperty(string propertyName);
}
