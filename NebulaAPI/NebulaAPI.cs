global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Reflection;
using System.Runtime.CompilerServices;
using Virial.Achievements;
using Virial.Assignable;
using Virial.Compat;
using Virial.Components;
using Virial.Configuration;
using Virial.Events;
using Virial.Game;
using Virial.Internal;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;

[assembly: InternalsVisibleTo("Nebula")]

namespace Virial;

internal interface INebula
{
    Version APIVersion { get; }

    /// <summary>
    /// モジュールを取得します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T? Get<T>() where T : class;

    // ResourceAPI

    IResourceAllocator NebulaAsset { get; }
    IResourceAllocator InnerslothAsset { get; }
    IResourceAllocator? GetAddonResource(string addonId);
    IResourceAllocator GetCallingAddonResource(Assembly assembly);

    // GameAPI

    Game.Game? CurrentGame { get; }



    // Shortcuts

    Configuration.Configurations Configurations { get; }
    Media.GUI GUILibrary { get; }
    Media.Translator Language { get; }
    Utilities.IHasher Hasher { get; }
    Assignable.Assignables Assignables { get; }
    Logging.Logging Logging { get; }

    //GameStatsAPI
    GameStatsEntry CreateStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable, TextComponent? displayTitle, int innerPriority = 0, GameStatsAnnotation annotation = GameStatsAnnotation.None, GameStatsUnit unit = GameStatsUnit.None);
    DecimalGameStatsEntry CreateDecimalStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable, TextComponent? displayTitle, int innerPriority = 0, GameStatsAnnotation annotation = GameStatsAnnotation.None, int digits = 1, GameStatsUnit unit = GameStatsUnit.None);
    void IncrementStatsEntry(string id, int num);
    void IncrementDecimalStatsEntry(DecimalGameStatsEntry entry, float value);

    //DocumentTipAPI
    void RegisterTip(IDocumentTip tip);

    //LowLevel
    internal AmongUsLL AmongUs { get; }

    E RunEvent<E>(E ev) where E : class, Virial.Events.Event;
    void RegisterExtension<T, E>(Action<T, E> operation, int priority) where E : Virial.Events.Event;

    IModuleFactory Modules { get; }

    IDisposable CreateRPCSection(string? label);

    bool IsAndroid { get; }
    bool IsPC => !IsAndroid;

    //Scripting
    internal System.Collections.IEnumerator CoCompileDirectory(string moduleName, string sourceDir, IEnumerable<string> extraReferences, string outputPath, bool useHiddenMembers, Action<bool> callback);
}

public static class NebulaAPI
{
    static internal INebula instance = null!;
    static internal NebulaPreprocessor? preprocessor = null;

    public static Version APIVersion => instance.APIVersion;

    static public IResourceAllocator NebulaAsset => instance.NebulaAsset;
    static public IResourceAllocator InnerslothAsset => instance.InnerslothAsset;
    static public IResourceAllocator AddonAsset => instance.GetCallingAddonResource(Assembly.GetCallingAssembly());

    static public IResourceAllocator? GetAddon(string addonId) => instance.GetAddonResource(addonId);

    static public T? Get<T>() where T : class => instance.Get<T>();


    /// <summary>
    /// GUIモジュールです。
    /// </summary>
    static public Media.GUI GUI => instance.GUILibrary;

    /// <summary>
    /// 翻訳モジュールです。
    /// </summary>
    static public Media.Translator Language => instance.Language;

    /// <summary>
    /// オプションやゲーム内共有変数に関するモジュールです。
    /// </summary>
    static public Configuration.Configurations Configurations => instance.Configurations;

    /// <summary>
    /// 不変なハッシュ値を生成するモジュールです。
    /// </summary>
    static public Utilities.IHasher Hasher => instance.Hasher;

    /// <summary>
    /// 役職に関する情報を提供するモジュールです。
    /// </summary>
    static public Assignable.Assignables Assignables => instance.Assignables;

    /// <summary>
    /// ログツールを提供するモジュールです。
    /// </summary>
    static public Logging.Logging Logging => instance.Logging;

    /// <summary>
    /// 現在のゲームを取得します。
    /// </summary>
    static public Game.Game? CurrentGame => instance.CurrentGame;

    /// <summary>
    /// プリプロセッサを取得します。
    /// プリプロセス終了後はnullが返ります。
    /// </summary>
    static public NebulaPreprocessor? Preprocessor => preprocessor;

    /// <summary>
    /// 定義済み役職を取得します。
    /// </summary>
    /// <param name="internalName"></param>
    /// <returns></returns>
    [Obsolete("代わりにAssignables.GetRole()を使用してください。")]
    static public Assignable.DefinedRole? GetRole(string internalName) => instance.Assignables.GetRole(internalName);
    /// <summary>
    /// 定義済みモディファイアを取得します。
    /// </summary>
    /// <param name="internalName"></param>
    /// <returns></returns>
    [Obsolete("代わりにAssignables.GetModifier()を使用してください。")]
    static public Assignable.DefinedModifier? GetModifier(string internalName) => instance.Assignables.GetModifier(internalName);
    /// <summary>
    /// 定義済み幽霊役職を取得します。
    /// </summary>
    /// <param name="internalName"></param>
    /// <returns></returns>
    [Obsolete("代わりにAssignables.GetGhostRole()を使用してください。")]
    static public Assignable.DefinedGhostRole? GetGhostRole(string internalName) => instance.Assignables.GetGhostRole(internalName);

    /// <summary>
    /// イベントを実行します。
    /// </summary>
    /// <typeparam name="E"></typeparam>
    /// <param name="ev"></param>
    /// <returns></returns>
    static public E RunEvent<E>(E ev) where E : class, Virial.Events.Event => instance.RunEvent(ev);
    static public void RegisterExtension<T, E>(Action<T, E> operation, int priority = 0) where E : Virial.Events.Event => instance.RegisterExtension(operation, priority);
    static public GameStatsEntry CreateStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable = null, TextComponent? displayTitle = null, int innerPriority = 0, GameStatsAnnotation annotation = GameStatsAnnotation.None, GameStatsUnit unit = GameStatsUnit.None) => instance.CreateStatsEntry(id, category, assignable, displayTitle, innerPriority, annotation, unit);
    /// <summary>
    /// 小数の値を記録する統計を生成します。
    /// </summary>
    /// <param name="digits">表示する小数点以下の桁数。値は10^digits倍した整数として記録されます。</param>
    /// <returns></returns>
    static public DecimalGameStatsEntry CreateDecimalStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable = null, TextComponent? displayTitle = null, int innerPriority = 0, GameStatsAnnotation annotation = GameStatsAnnotation.None, int digits = 1, GameStatsUnit unit = GameStatsUnit.None) => instance.CreateDecimalStatsEntry(id, category, assignable, displayTitle, innerPriority, annotation, digits, unit);
    static public void Progress(this GameStatsEntry entry, int num = 1) => instance.IncrementStatsEntry(entry.Id, num);
    /// <summary>
    /// 小数の統計を進めます。値はゲーム中は小数のまま合算され、ゲーム終了時に整数化して記録されます。
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="value">実際の値</param>
    static public void Progress(this DecimalGameStatsEntry entry, float value) => instance.IncrementDecimalStatsEntry(entry, value);
    static public void IncrementStatsEntry(string  entryId, int num = 1) => instance.IncrementStatsEntry(entryId, num);

    /// <summary>
    /// ドキュメント内で表示される要素を追加します。
    /// 現在、<see cref="WinConditionTip"/>のみ使用可能です。
    /// <see cref="WinConditionTip"/>の場合、勝利条件一覧に表示されます。
    /// </summary>
    /// <param name="tip"></param>
    static public void RegisterTip(IDocumentTip tip) => instance.RegisterTip(tip);

    /// <summary>
    /// モジュールを生成するファクトリメソッド群です。
    /// </summary>
    static public IModuleFactory Modules => instance.Modules;

    static internal AmongUsLL AmongUs => instance.AmongUs;

    /// <summary>
    /// 一括で送信するRPCをまとめるセクションを取得します。
    /// セクション内のRPCは、全クライアントでの同時かつ同順序の実行が保証されます。
    /// </summary>
    /// <param name="label"></param>
    /// <returns>RPCセクション。戻り値をusingステートメントで使用すると一括でまとめられるRPCを視覚的に把握しやすくなります。</returns>
    static public IDisposable CreateRPCSection(string? label = null) => instance.CreateRPCSection(label);

    /// <summary>
    /// 指定したディレクトリ内のスクリプトをコンパイルします。PC版でのみ使用できます。
    /// コンパイルログは出力先の隣(出力先のパス + ".log")に出力されます。
    /// </summary>
    /// <param name="moduleName">モジュール名。</param>
    /// <param name="sourceDir">スクリプトのあるディレクトリ。サブディレクトリ内のスクリプトも対象になります。</param>
    /// <param name="extraReferences">追加で参照するDLLのパス。</param>
    /// <param name="outputPath">出力先のDLLのパス。</param>
    /// <param name="useHiddenMembers">NebulaとNebulaAPIの非公開メンバへのアクセスを許可するかどうか。</param>
    /// <param name="callback">コンパイルに成功し、DLLが出力された場合にtrueを受け取ります。</param>
    /// <returns>コンパイルが終わるまで待機するコルーチン。</returns>
    static internal System.Collections.IEnumerator CoCompileDirectory(string moduleName, string sourceDir, IEnumerable<string> extraReferences, string outputPath, bool useHiddenMembers, Action<bool> callback)
        => instance.CoCompileDirectory(moduleName, sourceDir, extraReferences, outputPath, useHiddenMembers, callback);


}