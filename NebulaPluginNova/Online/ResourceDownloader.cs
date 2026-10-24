using System.Security.Cryptography;
using Nebula.Modules.Logging;
using Virial.Attributes;
using Virial.Runtime;

namespace Nebula.Online;

[NebulaPreprocess(PreprocessPhase.BuildNoSModuleContainer)]
internal static class ResourceDownloader
{
    private static readonly List<(string path, string url, string md5)> requests = [
        (Path.Combine(PathHelpers.BepInExRootPath, "download", "FungleMesh.zip"), "https://github.com/Dolly1016/Nebula/releases/download/v%2Cv3.5.3%2C110%2C1643/mesh.zip", "bb36b2370a5752c204655ba234937111"),
        ];

    private static bool processed = false;

    public static void Request(string path, string url, string md5)
    {
        if (processed)
        {
            NebulaLogger.Instance.Error($"Download request was added after preprocess. (Path: {path})");
            return;
        }
        requests.Add((path, url, md5));
    }

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        processed = true;
        if (requests.Count == 0) yield break;

        yield return preprocessor.SetLoadingText("Downloading Resources");

        using var md5 = MD5.Create();
        foreach (var (path, url, hash) in requests)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(PathHelpers.GameRootPath, path);
            var expectedHash = hash.Replace("-", "").ToLowerInvariant();

            if (File.Exists(fullPath))
            {
                string? existingHash = null;
                try
                {
                    using var stream = File.OpenRead(fullPath);
                    existingHash = ComputeHash(md5, stream);
                }
                catch (Exception e)
                {
                    NebulaLogger.Instance.Error($"Failed to read the existing file. (Path: {fullPath})\n{e}");
                }
                if (existingHash == expectedHash) continue;
            }

            if (!NebulaPlugin.AllowHttpCommunication)
            {
                NebulaLogger.Instance.Message($"Skipped downloading the resource because http communication is not allowed. (Path: {fullPath})");
                continue;
            }

            byte[]? data = null;
            yield return NebulaWebRequest.CoGetRaw(Helpers.ConvertUrl(url), true, d => data = d, () => NebulaLogger.Instance.Error($"Failed to download the resource. (URL: {url})"));
            if (data == null) continue;

            var actualHash = ComputeHash(md5, data);
            if (actualHash != expectedHash)
            {
                NebulaLogger.Instance.Error($"MD5 mismatch on downloaded resource. (URL: {url}, Expected: {expectedHash}, Actual: {actualHash})");
                continue;
            }

            try
            {
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                //書き込み途中で中断された場合に備えて別名にしておく
                var tempPath = fullPath + ".download";
                File.WriteAllBytes(tempPath, data);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.Move(tempPath, fullPath);
            }
            catch (Exception e)
            {
                NebulaLogger.Instance.Error($"Failed to write the downloaded resource. (Path: {fullPath})\n{e}");
            }
        }
    }

    private static string ComputeHash(MD5 md5, Stream stream) => BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    private static string ComputeHash(MD5 md5, byte[] data) => BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
}
