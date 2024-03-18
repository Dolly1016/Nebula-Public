using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Attributes;

/// <summary>
/// メソッド呼び出しをRPC呼び出しに置き換える属性です。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
[RequiringHandshake]
public class NebulaRPC : Attribute
{

}

/// <summary>
/// 呼び出しによってハンドシェイクが求められるメソッドであることを示す属性です。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiringHandshake : Attribute
{

}