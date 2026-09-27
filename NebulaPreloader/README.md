# NebulaPreloader (Steam Overlay Initializer)

Among Us.exe を Steam クライアント経由ではなく直接起動した場合でも Steam Overlay を有効化するための
BepInEx 6 patcher (preloader) プラグイン。アセンブリの書き換えは一切行わず、
通常プラグインの `BasePlugin.Load()` より前の段階で `SteamAPI_Init()` を一度だけ呼び出す。

## 確認した BepInEx API (BepInEx 6.0.0-be.783)

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `BepInEx.Preloader.Core.Patching.BasePatcher` (public abstract) |
| エントリポイント | `public virtual void Initialize()` / `public virtual void Finalizer()` |
| 属性 | `BepInEx.Preloader.Core.Patching.PatcherPluginInfoAttribute(string guid, string name, string version)` |
| 探索ディレクトリ | `BepInEx.Paths.PatcherPluginPath` = `<GameRoot>/BepInEx/patchers` |
| 参照DLL | `BepInEx/core/BepInEx.Core.dll`, `BepInEx/core/BepInEx.Preloader.Core.dll` |

## 使い方 (Overlay で URL を開く)

`BepInEx/patchers` に置いたアセンブリは BepInEx のアセンブリ解決対象になるため、
通常のプラグインから直接参照して呼び出せる。

```csharp
// 例: Mod 内のボタンから localhost の Web UI を開く
Nebula.Preloader.SteamOverlay.OpenWebPage("http://127.0.0.1:22000/");
```

内部では `SteamAPI_ISteamFriends_ActivateGameOverlayToWebPage()` を呼ぶ。
Overlay が利用できない状態では何もせず `false` を返す (この状態で呼ぶと Steam クライアント側で
ページが開かれてしまうため)。

**ゲームウィンドウが前面にあるときに呼ぶこと。** 前面にない場合、Steam は Overlay ではなく
Steam クライアントでページを開く (実機で確認済み)。ゲーム内のボタン操作から呼ぶ限りは問題ない。

## 起動順序 (BepInEx.Unity.IL2CPP を逆アセンブルして確認)

```
Among Us.exe → UnityPlayer → il2cpp_init (Doorstop hook)
  → Doorstop.Entrypoint.Start()
  → BepInEx.Unity.IL2CPP.UnityPreloaderRunner.PreloaderMain()
  → BepInEx.Unity.IL2CPP.Preloader.Run()
      → ConsoleManager / Logger 初期化
      → Il2CppInteropManager.Initialize()          (interop 生成)
      → AssemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath)
      → AssemblyPatcher.PatchAndLoad()
            → BasePatcher.Initialize()   ★ ここで SteamAPI_Init() を呼ぶ
            → アセンブリパッチ適用 → BasePatcher.Finalizer()
      → IL2CPPChainloader.Initialize() → 各 BasePlugin.Load()
  → Unity のグラフィックス初期化 (D3D11 device 作成) → ゲーム本体
```

実機で計測したところ、`Initialize()` の時点では `d3d11.dll` / `dxgi.dll` はいずれも未ロードで、
`SteamAPI_Init()` の直後に `gameoverlayrenderer.dll` が注入された
(= Unity の D3D11 デバイス生成より前に Overlay のフックが入る)。

## ビルド

環境変数 `AmongUs` にゲームディレクトリ (BepInEx 入り) を設定した上で

```
dotnet build NebulaPreloader/NebulaPreloader.csproj -c Release
```

ビルド後、`NebulaPreloader.dll` が以下へ自動コピーされる。

* `%AmongUs%\BepInEx\patchers\`
* `%AmongUsRelease%\Nebula_Steam\BepInEx\patchers\` (Release のみ)
* `%AmongUsMod%\BepInEx\patchers\` (Release のみ)

## 配置

```
Among Us\
├─ Among Us.exe
├─ steam_appid.txt            <- "945360" (改行のみ、BOM なし)
├─ Among Us_Data\Plugins\x86\steam_api.dll
└─ BepInEx\patchers\NebulaPreloader.dll
```

`steam_appid.txt` は Steam 外から起動したときに AppID を判別するために必要で、
`SteamAPI_Init()` はカレントディレクトリから読む (通常は exe のあるディレクトリ)。

## 注意

* Among Us (Steam版) は **32bit (x86) ビルド** であり、`steam_api64.dll` ではなく
  `Among Us_Data\Plugins\x86\steam_api.dll` を使う。このパスは既定の DLL 検索パスに
  含まれないため、`NativeLibrary.SetDllImportResolver` でフルパス解決している
  (64bit ビルドになった場合は `steam_api64.dll` / `Plugins\x86_64` を自動で選択)。
* `SteamAPI_RestartAppIfNecessary()` および `steam://run/945360` は使用しない。
