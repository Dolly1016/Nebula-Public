using Nebula;
using Octokit;
using System.Diagnostics;

Console.WriteLine(GetDisplayVersion() + " ("+ GetTagVersion() +")");

List<Process> AllProcess = [];

var githubClient = new GitHubClient(new ProductHeaderValue("lr")) { Credentials = new(GetGitHubToken()) };

bool exit = false;
string? githubReleaseUrl = null;

AllProcess.Add(Process.Start(GenerateShortcut("AmongUsMod") + "\\Among Us.exe"));

while (!exit)
{
    Console.WriteLine("\nキーを入力して操作してください。\nD: リリース名用の名称をコピー\nT: タグ用の名称をコピー\nC: (リリース後のみ)リリースのURLをコピー\nR: GitHubのリリース作成ページを開く\nF: リリースフォルダを開く\nG: ゲームの起動・ゲームフォルダを開く\nU: 最新版として公開\nSpace: 終了");

    ConsoleKey ReadKey(){
        var key = Console.ReadKey();
        Console.WriteLine("\n\n");
        return key.Key;
    }
    
    switch (ReadKey())
    {
        case ConsoleKey.D:
            CopyText(GetDisplayVersion());
            Console.WriteLine("リリース名称用テキストをコピーしました。");
            break;
        case ConsoleKey.T:
            CopyText(GetTagVersion());
            Console.WriteLine("タグテキストをコピーしました。");
            break;
        case ConsoleKey.C:
            if (githubReleaseUrl != null)
            {
                CopyText(githubReleaseUrl);
                Console.WriteLine("公開済みリリースのURLをコピーしました。");
            }
            else
            {
                Console.WriteLine("まだリリースを公開していません！");
            }
            break;
        case ConsoleKey.F:
            TryOpenDirectory("AmongUsRelease");
            break;
        case ConsoleKey.R:
            OpenWebpage("https://github.com/Dolly1016/Nebula/releases/new");
            Console.WriteLine("GitHubのリリースページを開きます。");
            break;
        case ConsoleKey.U:
            Console.Write("リリースの説明文を入力してください。\n>");
            var dscr = Console.ReadLine() ?? "";
            if (dscr.Length > 0)
            {
                var release = CreateRelease(dscr.Replace("<br>","\r\n"));
                if (release != null)
                {
                    githubReleaseUrl = release.HtmlUrl;
                    CopyText(githubReleaseUrl);
                    Console.WriteLine("リリースのURLをクリップボードにコピーしました。");
                    var path = Environment.GetEnvironmentVariable("AmongUsRelease");
                    Console.WriteLine("dllファイルをアップロードしています...");
                    UploadAsset(release, "Nebula.dll", File.OpenRead(path + "\\Nebula.dll"));
                    Console.WriteLine("Steam版 zipファイルをアップロードしています...");
                    UploadAsset(release, "Nebula_Steam.zip", File.OpenRead(path + "\\Nebula_Steam.zip"));
                    Console.WriteLine("Epic版 zipファイルをアップロードしています...");
                    UploadAsset(release, "Nebula_Epic.zip", File.OpenRead(path + "\\Nebula_Epic.zip"));
                    Console.WriteLine("公開が完了しました。");
                }
            }
            else
            {
                Console.WriteLine("公開をキャンセルしました。");
            }
            break;
        case ConsoleKey.G:
            Console.WriteLine("\n\n[ゲームの実行] キーを入力して操作してください。\n数字: 同時立ち上げ数を指定して起動\nZ: 全ゲームを終了\nF: フォルダを開く\nV: バニラに切り替える\nSpace: もどる");
            var key = ReadKey();
            string envPath = "AmongUsMod";

            if(key == ConsoleKey.V)
            {
                envPath = "AmongUsVanilla";
                Console.WriteLine("\n\n起動するゲームをバニラに切り替えます。引き続き操作してください。");
                key = ReadKey();
            }

            switch (key)
            {
                case ConsoleKey.F:
                    TryOpenDirectory(envPath);
                    break;
                case ConsoleKey.Z:
                    Console.WriteLine("関連するゲームをすべて終了します。");
                    foreach (var p in AllProcess) if (!p.HasExited) p.Kill();
                    AllProcess.Clear();
                    break;
                case ConsoleKey.Spacebar:
                    break;
                default:
                    var path = GenerateShortcut(envPath);
                    var strNum = key.ToString();
                    if(strNum.Length == 2 && int.TryParse(strNum[1].ToString(), out var num))
                    {
                        Console.WriteLine(num + "重で起動を開始します。");
                        for (int i = 0; i < num; i++)
                        {
                            AllProcess.Add(Process.Start(path + "\\Among Us.exe"));
                            Console.WriteLine($"{i + 1}/{num}回目 起動済み");
                            if (i + 1 < num) Thread.Sleep(8000);
                            path += "\\m";
                            
                        }

                        Console.WriteLine("ゲームの起動が完了しました。");
                    }
                    else
                    {
                        Console.WriteLine("ゲームの起動には数字キーの入力が必要です。初めの画面に戻ります。");
                    }
                    break;
            }
            break;
        case ConsoleKey.Spacebar:
            Console.WriteLine("終了します。");
            exit = true;
            break;
    }
    
}

foreach (var p in AllProcess) if (!p.HasExited) p.Kill();

string GenerateShortcut(string envPath)
{
    var path = Environment.GetEnvironmentVariable(envPath);

    if (!Directory.Exists(path + "\\m"))
    {
        Console.WriteLine("シンボリックリンクを作成しています。");

        var proc = new Process();
        proc.StartInfo.FileName = @"c:\Windows\System32\cmd.exe";
        proc.StartInfo.Verb = "RunAs";
        proc.StartInfo.UseShellExecute = true;
        proc.StartInfo.Arguments = $"/c mklink /d \"{path}\\m\" \"{path}\"";
        proc.Start();

        while (!proc.HasExited) { }
    }
    return path;
}

Process? OpenWebpage(string url)
{
    ProcessStartInfo pi = new()
    {
        FileName = url,
        UseShellExecute = true,
    };

    return Process.Start(pi);
}

void TryOpenDirectory(string env)
{
    var releasePath = Environment.GetEnvironmentVariable(env);
    if (releasePath == null)
    {
        Console.WriteLine("フォルダが見つかりませんでした。");
    }
    else
    {
        System.Diagnostics.Process.Start("EXPLORER.EXE", releasePath);
        Console.WriteLine("フォルダを開きました。");
    }
}

void CopyText(params string[] text)
{
    foreach (var t in text) Console.WriteLine("Copied: " + t);

    System.Threading.Thread thread = new(() => {
        foreach (var t in text)
        {
            Clipboard.SetDataObject(t, true);
            Thread.Sleep(500);
        }
    });
    thread.SetApartmentState(System.Threading.ApartmentState.STA);
    thread.Start();
}

bool IsStableVersion()
{
    return NebulaPlugin.VisualVersion.StartsWith('v') && NebulaPlugin.VisualVersion.Length >= 2 && char.IsNumber(NebulaPlugin.VisualVersion[1]);
}

string FormatDisplayVersion()
{
    if(IsStableVersion())
    {
        return "ver " + NebulaPlugin.VisualVersion.Substring(1);
    }
    return NebulaPlugin.VisualVersion;
}

string GetDisplayVersion()
{
    return "Nebula on the Ship " + FormatDisplayVersion();
}

string GetTagVersion()
{
    string prefix = "c";
    if (IsStableVersion())
        prefix = "v";
    else if (NebulaPlugin.VisualVersion.StartsWith("Snapshot"))
        prefix = "s";

    return prefix + "," + NebulaPlugin.VisualVersion.Replace(" ","_") + "," + NebulaPlugin.PluginEpochStr + "," + NebulaPlugin.PluginBuildNumStr;
}

string GetGitHubToken()
{
    return Environment.GetEnvironmentVariable("AmongUsGitHubToken") ?? "";
}

Release? CreateRelease(string description)
{
    var task = githubClient.Repository.Release.Create("Dolly1016", "Nebula", new(GetTagVersion()) { Name = GetDisplayVersion(), Body = description, Draft = false });
    task.Wait();
    return task.Result;
}

void UploadAsset(Release release, string fileName, Stream rawData)
{
    var task = githubClient.Repository.Release.UploadAsset(release, new(fileName, "application/zip", rawData, null));
    task.Wait();
    return;
}

class ReleaseResponse
{
    public string upload_url;
}