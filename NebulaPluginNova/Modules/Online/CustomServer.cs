using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Virial.Runtime;

namespace Nebula.Modules.Online;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
internal class CustomServerLoader
{
    private class SuppliedServerData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("address")]
        public string Address { get; set; }
        [JsonPropertyName("port")]
        public ushort Port { get; set; }
        [JsonPropertyName("cond")]
        public string[] Cond { get; set; } = [];
        [JsonPropertyName("vc")]
        public string? VcServer { get; set; } = null;
    }

    private class CustomServerData
    {
        [JsonSerializableField]
        public string DisplayName = null!;
        [JsonSerializableField]
        public string Ip = null!;
        [JsonSerializableField]
        public ushort Port = 443;

        public bool IsValid => DisplayName != null && Ip != null;
    }

    static private IRegionInfo[] defaultRegions = [];
    static private readonly List<IRegionInfo> addonRegions = [];
    static private List<SuppliedServerData> suppliedRegions = [
        new(){ Name =  "Modded NA (MNA)", Address = "www.aumods.us", Port = 443},
        new(){ Name =  "Modded EU (MEU)", Address = "au-eu.duikbo.at", Port = 443},
        new(){ Name =  "Modded Asia (MAS)", Address = "au-as.duikbo.at", Port = 443},
        ];
    static private IRegionInfo[] currentRegions = [];

    static private readonly DataSaver Saver = new("NoSRegion");

    // 最後に選ばれたリージョンの名前。バニラの regionInfo.json には添字しか残らず、
    // Nebula のリージョンは AvailableRegions に居ないため名前が復元できない
    static private readonly StringDataEntry LastRegionName = new("region", Saver, "");

    public static IRegionInfo GenerateRegion(string name, string ip, ushort port) => new StaticHttpRegionInfo(name, StringNames.NoTranslation, ip,
        new ServerInfo[] { new ServerInfo("Http-1", ip, port, false) }).Cast<IRegionInfo>();

    internal static string GetVCServer()
    {
        if (suppliedRegions.Find(info => info.Name == ServerManager.Instance.CurrentRegion.Name, out var found) && found.VcServer != null) return found.VcServer;
        return "ws://www.nebula-on-the-ship.com:22010";
    }

    private static void LoadAddonRegion()
    {
        foreach (var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenRead("CustomServer.json");
            if (stream == null) continue;
            var servers = JsonStructure.Deserialize<List<CustomServerData>>(stream);

            foreach (var server in servers ?? [])
            {
                if (server.IsValid)
                {
                    addonRegions.Add(GenerateRegion(server.DisplayName, server.Ip, server.Port));
                }
            }
        }
    }

    private static IEnumerator LoadOnlineRegion()
    {
        yield return NebulaWebRequest.CoGet(NebulaWebRequest.GetNoSAPI("server/get"), false, json =>
        {
            suppliedRegions = JsonSerializer.Deserialize<SuppliedServerData[]>(json)?.ToList() ?? [];
        });
    }

    /// <summary>選ばれたリージョンの名前を控える。次回の起動でこの名前を探す。</summary>
    internal static void RememberRegion(IRegionInfo region)
    {
        if (region == null) return;
        LastRegionName.Value = region.Name;
    }

    /// <summary>
    /// 使えるリージョンを組み直し、前回と同じ名前のものを選び直す。
    /// </summary>
    /// <remarks>
    /// 選んでよいのは <see cref="CurrentAvailableRegions"/> が返すものだけ。
    /// バニラは Nebula のリージョンを覚えられないので、起動のたびにここで選び直す。
    /// 前回の名前が配信リストから消えていれば先頭に落とす。
    /// </remarks>
    private static void UpdateRegions()
    {
        ServerManager serverManager = DestroyableSingleton<ServerManager>.Instance;

        currentRegions = defaultRegions.Concat(addonRegions).Concat(suppliedRegions.Select(info => GenerateRegion(info.Name, info.Address, info.Port))).DistinctBy(info => info.Name).ToArray();
        serverManager.LoadServers();

        var available = CurrentAvailableRegions().ToArray();
        if (available.Length == 0) return;

        var region = available.FirstOrDefault(r => r.Name == LastRegionName.Value) ?? available[0];
        serverManager.StartCoroutine(ManagedEffects.Sequence(ManagedEffects.Wait(3f), ManagedEffects.Action(() => serverManager.SetRegion(region))).WrapToIl2Cpp());
    }

    static internal IEnumerable<IRegionInfo> CurrentAvailableRegions() => currentRegions.Where(r => r.TranslateName == StringNames.NoTranslation && (!suppliedRegions.Find(info => info.Name == r.Name, out var found) || found.Cond.Length == 0 || found.Cond.Contains(Language.GetCurrentLanguage())));

    static private IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        defaultRegions = ServerManager.DefaultRegions;
        LoadAddonRegion();
        yield return LoadOnlineRegion();
        UpdateRegions();
    }
}