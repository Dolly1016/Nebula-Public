global using System;
global using System.Collections.Generic;
global using System.Reflection;
global using System.Linq;
global using System.IO;
using System.Runtime.CompilerServices;
using Virial.Assignable;
using Virial.Compat;
using Virial.Components;
using Virial.Configuration;
using Virial.Events;
using Virial.Game;
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

    Configuration.Configurations Configurations => Get<Configuration.Configurations>()!;
    Media.GUI GUILibrary => Get<Media.GUI>()!;
    Media.Translator Language => Get<Media.Translator>()!;
    Utilities.IHasher Hasher => Get<Utilities.IHasher>()!;

    //AssignableAPI

    Assignable.DefinedRole? GetRole(string internalName);
    Assignable.DefinedModifier? GetModifier(string internalName);
    Assignable.DefinedGhostRole? GetGhostRole(string internalName);

    //GameStatsAPI
    GameStatsEntry CreateStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable, TextComponent? displayTitle, int innerPriority = 0);
    void IncrementStatsEntry(string id, int num);

    //DocumentTipAPI
    void RegisterTip(IDocumentTip tip);

    E RunEvent<E>(E ev) where E : class, Event;

    IModuleFactory Modules { get; }
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
    static public Assignable.DefinedRole? GetRole(string internalName) => instance.GetRole(internalName);
    /// <summary>
    /// 定義済みモディファイアを取得します。
    /// </summary>
    /// <param name="internalName"></param>
    /// <returns></returns>
    static public Assignable.DefinedModifier? GetModifier(string internalName) => instance.GetModifier(internalName);
    /// <summary>
    /// 定義済み幽霊役職を取得します。
    /// </summary>
    /// <param name="internalName"></param>
    /// <returns></returns>
    static public Assignable.DefinedGhostRole? GetGhostRole(string internalName) => instance.GetGhostRole(internalName);

    /// <summary>
    /// イベントを実行します。
    /// </summary>
    /// <typeparam name="E"></typeparam>
    /// <param name="ev"></param>
    /// <returns></returns>
    static public E RunEvent<E>(E ev) where E : class, Event => instance.RunEvent(ev);
    static public GameStatsEntry CreateStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable = null, TextComponent? displayTitle = null, int innerPriority = 0) => instance.CreateStatsEntry(id, category, assignable, displayTitle, innerPriority);
    static public void Progress(this GameStatsEntry entry, int num = 1) => instance.IncrementStatsEntry(entry.Id, num);
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
}