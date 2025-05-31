

using System;

namespace Virial.Attributes;

/// <summary>
/// メソッド呼び出しをRPC呼び出しに置き換える属性です。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
[RequiringHandshake]
public class NebulaRPC : Attribute
{

}

public enum PreprocessPhase
{
    /// <summary>
    /// NoSがモジュールコンテナを追加するフェイズです。
    /// </summary>
    BuildNoSModuleContainer,
    /// <summary>
    /// NoSがモジュールを追加するフェイズです。
    /// </summary>
    BuildNoSModule,
    /// <summary>
    /// NoSが提供するAPIの内部構造の構築が終わった直後のフェイズです。
    /// アドオンはこのフェイズに処理を差し込めません。
    /// </summary>
    PostBuildNoS,
    /// <summary>
    /// アドオンを読み込むフェイズです。
    /// </summary>
    LoadAddons,
    /// <summary>
    /// 読み込んだアドオンをコンパイルするフェイズです。
    /// </summary>
    CompileAddons,
    /// <summary>
    /// アドオンを読み込んだ直後のフェイズです。
    /// </summary>
    PostLoadAddons,
    /// <summary>
    /// 役職の追加をする直前のフェイズです。
    /// </summary>
    PreRoles,
    /// <summary>
    /// 役職を追加するフェイズです。
    /// </summary>
    Roles,
    /// <summary>
    /// 役職を確定させるフェイズです。これ以降役職を追加することはできません。
    /// </summary>
    FixRoles,
    /// <summary>
    /// 役職の追加が終了した直後のフェイズです。
    /// </summary>
    PostRoles,
    /// <summary>
    /// 共有可能変数等のデータ構造を確定させる直前のフェイズです。
    /// </summary>
    PreFixStructure,
    /// <summary>
    /// 共有可能変数等のデータ構造を確定させるフェイズです。
    /// </summary>
    FixStructure,
    FixStructureRoleFilter,
    FixStructureConfig,
    /// <summary>
    /// 共有可能変数等のデータ構造を確定させた直後のフェイズです。
    /// </summary>
    PostFixStructure,
    /// <summary>
    /// フェイズの数を表す定数です。フェイズとしては実在しません。
    /// </summary>
    NumOfPhases
}


/// <summary>
/// プリプロセスメソッドを実装しているクラスにこの属性を付加します。
/// 静的な公開メソッドPreprocess(INebulaPreprocessor)を実装していると記述された処理が実行されます。
/// そうでない場合でも静的コンストラクタが走ります。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NebulaPreprocess : Attribute
{
    internal PreprocessPhase MyPhase { get; init; }


    
    public NebulaPreprocess(PreprocessPhase phase)
    {
        MyPhase = phase;
    }
    
}


/// <summary>
/// 呼び出しによってハンドシェイクが求められるメソッドであることを示す属性です。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiringHandshake : Attribute
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

/// <summary>
/// ホストのみ呼び出される手続きを表します。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OnlyHost : Attribute
{

}

/// <summary>
/// リスナに優先度を設定します。デフォルトの優先度は100で、値が大きいほど優先して実行されます。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EventPriority : Attribute
{
    public int Priority { get; private init; }
    public EventPriority(int priority = 100)
    {
        Priority = priority;
    }
}

/// <summary>
/// ID付きドキュメントを表すクラスです。
/// </summary>
[AttributeUsage(AttributeTargets.Class,AllowMultiple =true)]
public class AddonDocumentAttribute : Attribute
{
    public string DocumentId { get; private init; }    
    public object[] Arguments { get; private init; }
    public AddonDocumentAttribute(string documentId, params object[] args)
    {
        this.DocumentId = documentId;
        this.Arguments = args;
    }
}
