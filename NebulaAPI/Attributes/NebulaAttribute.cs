using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Runtime;

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

/// <summary>
/// プリプロセスメソッドを実装しているクラスにこの属性を付加します。
/// 静的な公開メソッドPreprocess(INebulaPreprocessor)を実装している必要があります。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NebulaPreprocess : Attribute
{

}