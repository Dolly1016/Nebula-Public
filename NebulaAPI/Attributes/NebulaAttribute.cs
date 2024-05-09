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
/// 静的な公開メソッドPreprocess(INebulaPreprocessor)を実装していると記述された処理が実行されます。
/// そうでない場合でも静的コンストラクタが走ります。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NebulaPreprocess : Attribute
{

}

/// <summary>
/// GameOperatorに付与できる属性です。
/// そのクラスに紐づけられたプレイヤーに関するイベントでのみ呼び出されるよう制限されます。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OnlyMyPlayer : Attribute
{
}

/// <summary>
/// ローカルでのみ呼び出される手続きを表します。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class Local : Attribute
{

}