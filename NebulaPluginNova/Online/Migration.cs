using Nebula.Utilities;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nebula.Modules;

internal class Migration
{
    private class UploadRequest
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("data")]
        public Dictionary<string, string> Data { get; set; }
    }
    private class UploadResponse
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private class CheckAndDownloadRequest
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    private class CheckResponse
    {
        [JsonPropertyName("exists")]
        public bool Exists { get; set; }
        [JsonPropertyName("savedAt")]
        public string SavedAt { get; set; }
        public DateTime GetSavedLocalTime
        {
            get
            {
                DateTimeOffset dto = DateTimeOffset.Parse(SavedAt);
                return dto.ToLocalTime().DateTime;
            }
        }
    }

    private class DownloadResponse
    {
        [JsonPropertyName("data")]
        public Dictionary<string, string> Data { get; set; }
    }

    internal enum CheckResponseType
    {
        Error,
        Found,
        NotFound,
    }

    private static IEnumerable<(string systemPath, string virtualPath)> GetPathes()
    {
        var dataPath = DataSaver.DataSaverFolderPath;
        foreach (var sysPath in Directory.EnumerateFiles(dataPath))
        {
            if (!sysPath.EndsWith(".dat")) continue;
            var virtualPath = "D$" + Path.GetRelativePath(dataPath, sysPath);
            yield return (sysPath, virtualPath);
        }

        var presetsPath = PathHelpers.GameRootPath + Path.DirectorySeparatorChar + "Presets";
        if (Directory.Exists(presetsPath))
        {
            foreach (var sysPath in Directory.EnumerateFiles(presetsPath))
            {
                if (!sysPath.EndsWith(".dat")) continue;
                var virtualPath = "P$" + Path.GetRelativePath(presetsPath, sysPath);
                yield return (sysPath, virtualPath);
            }
        }
    }

    public static bool CanUseMigration => EOSManager.InstanceExists && EOSManager.Instance.FriendCode != null;
    private static Dictionary<string, string> GetMigrationData()
    {
        Dictionary<string, string> migrationDic = [];
        foreach(var entry in GetPathes())
        {
            var content = File.ReadAllText(entry.systemPath);
            migrationDic.Add(entry.virtualPath, content);
        }
        return migrationDic;
    }

    public static bool ParseMigrationData(Dictionary<string, string> dic)
    {
        foreach(var entry in dic)
        {
            if (entry.Key.Length < 2) continue;
            var firstLetter = entry.Key[0];
            var path = entry.Key.Substring(2);
            string folderPath;
            if(firstLetter == 'D')
            {
                folderPath = DataSaver.DataSaverFolderPath;
            }
            else if(firstLetter == 'P')
            {
                folderPath = PathHelpers.GameRootPath + Path.DirectorySeparatorChar + "Presets";
            }
            else
            {
                continue;
            }
            var filePath = folderPath + Path.DirectorySeparatorChar + path;
            File.WriteAllText(filePath, entry.Value);
        }
        return true;
    }

    public static IEnumerator CoUploadData(Action<string> callBack, Action onFailed)
    {
        string friendCode = EOSManager.Instance.FriendCode;
        bool error = false;
        string password = "";
        yield return NebulaWebRequest.CoPost<UploadRequest, UploadResponse>(NebulaWebRequest.GetNoSAPI("migration/upload"), new() { UserId = friendCode, Data = GetMigrationData()}, response =>
        {
            if(response.Error != null)
            {
                error = true;
            }
            else
            {
                password = response.Password;
            }
        }, () => error = true);

        if (error)
            onFailed();
        else
            callBack(password);
    }

    public static IEnumerator CoCheckData(string password, Action<CheckResponseType, DateTimeOffset?> callBack)
    {
        string friendCode = EOSManager.Instance.FriendCode;
        CheckResponseType responseType = CheckResponseType.Error;
        DateTimeOffset? time = null;
        yield return NebulaWebRequest.CoPost<CheckAndDownloadRequest, CheckResponse>(NebulaWebRequest.GetNoSAPI("migration/check"), new() { UserId = friendCode, Password = password }, response =>
        {
            if (response.Exists)
            {
                responseType = CheckResponseType.Found;
                time = response.GetSavedLocalTime;
            }
            else
            {
                responseType = CheckResponseType.NotFound;
            }
        }, null);
        callBack.Invoke(responseType, time);
    }

    public static IEnumerator CoDownloadData(string password, Action<CheckResponseType> callBack)
    {
        string friendCode = EOSManager.Instance.FriendCode;
        CheckResponseType responseType = CheckResponseType.Error;
        yield return NebulaWebRequest.CoPost<CheckAndDownloadRequest, DownloadResponse>(NebulaWebRequest.GetNoSAPI("migration/download"), new() { UserId = friendCode, Password = password }, response =>
        {
            responseType = CheckResponseType.Found;
            var dic = response.Data;
            ParseMigrationData(dic);
        }, null);
        callBack.Invoke(responseType);
    }
}
