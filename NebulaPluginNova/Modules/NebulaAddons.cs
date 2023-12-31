﻿using Epic.OnlineServices.PlayerDataStorage;
using Mono.CSharp;
using NAudio.MediaFoundation;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules;

[NebulaPreLoad]
public class NebulaAddon : IDisposable, INameSpace
{
    public class AddonMeta
    {
        [JsonSerializableField]
        public string? Id = null;
        [JsonSerializableField]
        public string Name = "Undefined";
        [JsonSerializableField]
        public string Author = "Unknown";
        [JsonSerializableField]
        public string Description = "";
        [JsonSerializableField]
        public string Version = "";
        [JsonSerializableField]
        public int Priority = 0;
    }

    static private Dictionary<string, NebulaAddon> allAddons = new();
    static private List<NebulaAddon> allOrderedAddons = new();
    static public IEnumerable<NebulaAddon> AllAddons => allOrderedAddons;
    static public NebulaAddon? GetAddon(string id)
    {
        if (allAddons.TryGetValue(id, out var addon)) return addon;
        return null;
    }

    static public IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Loading Addons";
        yield return null;

        Directory.CreateDirectory("Addons");

        var md5 = MD5.Create();

        //ローカルなアドオンを更新
        foreach (var dir in Directory.GetDirectories("Addons", "*"))
        {
            string id = Path.GetFileName(dir);
            string filePath = dir + "/" + id + ".zip";
            if (File.Exists(filePath)) File.Move(filePath, "Addons/" + id + ".zip", true);
        }


        //組込アドオンの読み込み
        Assembly assembly = Assembly.GetExecutingAssembly();
        foreach (var file in assembly.GetManifestResourceNames().Where(name => name.StartsWith("Nebula.Resources.Addons.") && name.EndsWith(".zip")))
        {
            try
            {
                var stream = assembly.GetManifestResourceStream(file);
                if (stream == null) continue;
                var zip = new ZipArchive(stream);

                var addon = new NebulaAddon(zip, file) { Priority = -100 };
                allAddons.Add(addon.Id, addon);

                addon.HandshakeHash = System.BitConverter.ToString(md5.ComputeHash(assembly.GetManifestResourceStream(file)!)).ComputeConstantHash();
            }
            catch
            {
            }
        }

        //外部アドオンの読み込み
        foreach (var file in Directory.GetFiles("Addons"))
        {
            var ext = Path.GetExtension(file);
            if (ext == null) continue;
            if (!ext.Equals(".zip")) continue;

            var zip = ZipFile.OpenRead(file);

            try
            {
                var addon = new NebulaAddon(zip, file);
                allAddons.Add(addon.Id, addon);

                addon.HandshakeHash = System.BitConverter.ToString(md5.ComputeHash(File.OpenRead(file))).ComputeConstantHash();
            }
            catch
            {
                NebulaPlugin.Log.Print(NebulaLog.LogCategory.Addon, "Failed to load addon \"" + Path.GetFileName(file) + "\".");
            }
        }

        allOrderedAddons = new List<NebulaAddon>(allAddons.Values.OrderBy(addon => addon.Priority));

        foreach (var addon in allOrderedAddons)
        {
            if (addon.Archive.Entries.Any(entry => entry.FullName.StartsWith(addon.InZipPath + "Scripts/"))) addon.NeedHandshake = true;
        }

        AddonScriptManager.EvaluateScript("Initializers");
    }

    static private string MetaFileName = "addon.meta";
    private NebulaAddon(ZipArchive zip, string path)
    {
        foreach (var entry in zip.Entries)
        {
            if (entry.Name != MetaFileName) continue;

            using var metaFile = entry.Open();

            AddonMeta? meta = (AddonMeta?)JsonStructure.Deserialize(metaFile, typeof(AddonMeta));
            if (meta == null) throw new Exception();

            Id = meta.Id ?? Path.GetFileNameWithoutExtension(path);
            AddonName = meta.Name;
            Author = meta.Author;
            Description = meta.Description;
            Version = meta.Version;
            Priority = meta.Priority;

            InZipPath = entry.FullName.Substring(0, entry.FullName.Length - MetaFileName.Length);
            break;
        }

        using var iconEntry = zip.GetEntry(InZipPath + "icon.png")?.Open();
        if (iconEntry != null)
        {
            var texture = GraphicsHelper.LoadTextureFromStream(iconEntry);
            texture.MarkDontUnload();

            Icon = texture.ToSprite(Mathf.Max(texture.width, texture.height));
            Icon.MarkDontUnload();
        }

        Archive = zip;
    }

    public Stream? OpenStream(string path)
    {
        return Archive.GetEntry(InZipPath + path.Replace('\\', '/'))?.Open();
    }

    public void Dispose()
    {
        Archive.Dispose();
    }

    public Stream? OpenRead(string innerAddress)
    {
        innerAddress = (InZipPath + innerAddress).Replace('/', '.');
        Debug.Log(innerAddress);
        foreach (var entry in Archive.Entries)
        {
            if (entry.FullName.Replace('/', '.') == innerAddress) return entry.Open();
        }
        return null;
    }

    public string Id { get; private set; } = "";
    public string InZipPath { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string AddonName { get; private set; } = "";
    public int Priority { get; private set; } = 0;
    public Sprite? Icon { get; private set; } = null;
    public ZipArchive Archive { get; private set; }

    //互換性チェックが必要なアドオン
    public bool NeedHandshake { get; private set; } = false;

    public int HandshakeHash { get; private set; } = 0;

    static public int AddonHandshakeHash
    {
        get
        {
            int val = 0;
            foreach(var addon in allOrderedAddons)
            {
                if(addon.NeedHandshake) val ^= addon.HandshakeHash;
            }
            return val;
        }
    }
}
