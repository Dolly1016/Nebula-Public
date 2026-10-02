using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NebulaInstaller;

public enum GamePlatform
{
    Steam,
    Epic
}

static public class AUInstalling
{
    public static bool IsVanillaAmongUsDirectory(string path)
    {
        return !Directory.Exists(path + Path.DirectorySeparatorChar + "BepInEx");
    }

    public static bool CurrentDirectoryHasAmongUsExe()
    {
        return File.Exists("Among Us.exe");
    }
    public static string? GetAmongUsDirectoryPath()
    {
        string steamDirectory = "\\Steam\\steamapps\\common\\Among Us";
        string initialDirectory = Directory.GetCurrentDirectory();
        if (Directory.Exists("C:\\Program Files" + steamDirectory))
            initialDirectory = "C:\\Program Files" + steamDirectory;
        else if (Directory.Exists("D:\\Program Files" + steamDirectory))
            initialDirectory = "D:\\Program Files" + steamDirectory;
        var dialog = new OpenFileDialog();
        dialog.Title = "Among Us.exeを選択してください。";
        dialog.Filter = "Game File|Among Us.exe";
        dialog.InitialDirectory = initialDirectory;
        if (dialog.ShowDialog() == true)
        {
            return Path.GetDirectoryName(dialog.FileName);
        }
        else
        {
            return null;
        }
    }
    public static string? GetCopyToDirectoryPath()
    {
        OpenFolderDialog dialog = new();
        dialog.Multiselect = false;
        dialog.Title = "NoSのインストール先を選択してください。";
        if (dialog.ShowDialog() == true)
        {
            return dialog.FolderName;
        }
        else
        {
            return null;
        }
    }

    public static GamePlatform GuessPlatform(string vanillaDirectoryPath)
    {
        if (Directory.Exists(vanillaDirectoryPath + Path.DirectorySeparatorChar + ".egstore"))
            return GamePlatform.Epic;
        if (vanillaDirectoryPath.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
            return GamePlatform.Epic;
        if (vanillaDirectoryPath.Contains("steamapps", StringComparison.OrdinalIgnoreCase))
            return GamePlatform.Steam;

        return GamePlatform.Steam;
    }

    public static string? GetModDirectoryPathFromVanilla(string vanillaDirectoryPath)
    {
        var parent = Directory.GetParent(vanillaDirectoryPath)?.FullName;
        if (parent == null) return null;

        string path = parent + Path.DirectorySeparatorChar + "Among Us NoS";
        if (!Directory.Exists(path)) return path;
        int num = 2;
        while(Directory.Exists(path + " " + num))
        {
            num++;
        }
        return path + " " + num;
    }

    static public async Task CoInstall(string vanillaDirectoryPath, string installToDirectoryPath, GamePlatform platform, Action callBack)
    {
        void CopyDirectory(string innerPath = "")
        {
            Directory.CreateDirectory(installToDirectoryPath + innerPath);

            foreach (var file in Directory.GetFiles(vanillaDirectoryPath + innerPath))
                File.Copy(file, installToDirectoryPath + innerPath + Path.DirectorySeparatorChar + Path.GetFileName(file), true);
            foreach (var directory in Directory.GetDirectories(vanillaDirectoryPath + innerPath))
            {
                string newInnerPath = innerPath + Path.DirectorySeparatorChar + Path.GetFileName(directory);
                CopyDirectory(newInnerPath);
            }
        }

        /*
        string zipResourceName = platform == GamePlatform.Epic
            ? "NebulaInstaller.Resources.Nebula_Epic.zip"
            : "NebulaInstaller.Resources.Nebula_Steam.zip";
        */
        string zipResourceName = "NebulaInstaller.Resources.Nebula.zip";

        await Task.Run(() =>
        {
            CopyDirectory();

            using var archive = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(zipResourceName)!);
            archive.ExtractToDirectory(installToDirectoryPath, true);

            callBack.Invoke();
        });
    }
}
