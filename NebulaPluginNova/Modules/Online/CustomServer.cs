using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    static private readonly StringDataEntry LastRegionName = new("region", Saver, "Nebula on the Ship JP");

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

    /// <summary>
    /// 今選ばれているリージョンの名前を控える。次回の起動でこの名前を探す。
    /// </summary>
    /// <remarks>
    /// 控えるのは <see cref="CurrentAvailableRegions"/> に載っているものだけ。
    /// 他 Mod が残したリージョンや、部屋コード検索中の一時的な切り替えで
    /// 前回の選択を失わないようにする。
    /// </remarks>
    internal static void UpdateLastRegion()
    {
        if (!DestroyableSingleton<ServerManager>.InstanceExists) return;

        var name = DestroyableSingleton<ServerManager>.Instance.CurrentRegion?.Name;
        if (name == null) return;

        if (!CurrentAvailableRegions().Any(region => region.Name == name)) return;

        LastRegionName.Value = name;
    }

    /// <summary>
    /// 起動時に選び直すリージョンを返す。前回と同じ名前のものを探す。
    /// </summary>
    /// <remarks>
    /// 選んでよいのは <see cref="CurrentAvailableRegions"/> が返すものだけ。
    /// バニラは Nebula のリージョンを覚えられないので、起動のたびに選び直す。
    /// 前回の名前が配信リストから消えていれば先頭に落とす。
    /// リージョンの一覧がまだ組まれていなければ false を返す。
    /// </remarks>
    internal static bool TryGetRegionToRestore([MaybeNullWhen(false)] out IRegionInfo region)
    {
        var available = CurrentAvailableRegions().ToArray();
        if (available.Length == 0)
        {
            region = null;
            return false;
        }

        region = available.FirstOrDefault(r => r.Name == LastRegionName.Value) ?? available[0];
        return true;
    }

    /// <summary>
    /// 使えるリージョンを組み直す。
    /// </summary>
    /// <remarks>
    /// 前回のリージョンの復元は <see cref="ServerManager.LoadServers"/> のパッチが行う。
    /// <see cref="ServerManager"/> がすでに読み込みを終えていれば、ここで読み込み直して復元させる。
    /// まだ無いときに Instance を触ると使い捨てのオブジェクトが作られてしまうので触らない。
    /// </remarks>
    private static void UpdateRegions()
    {
        currentRegions = defaultRegions.Concat(addonRegions).Concat(suppliedRegions.Select(info => GenerateRegion(info.Name, info.Address, info.Port))).DistinctBy(info => info.Name).ToArray();

        if (DestroyableSingleton<ServerManager>.InstanceExists) DestroyableSingleton<ServerManager>.Instance.LoadServers();
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