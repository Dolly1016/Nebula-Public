using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Nebula.Modules.Cosmetics;

//読み込み済みModコスチュームの一覧。BepInEx/MoreCosmic/LoadedContents.jsonに書き出します。
//TBCLFieldsのPlayerDataにはコスチューム名と分割画像のインデックスしか載せないため、
//外部ツールはコスチューム名(ProductId)をキーにこのファイルから詳細を引きます。
internal class LoadedContents
{
    public int Version { get; set; }
    //ProductId(outfitのHatId/VisorId)をキーにした辞書
    public Dictionary<string, LoadedCostume> Hats { get; set; } = [];
    public Dictionary<string, LoadedCostume> Visors { get; set; } = [];
}

internal class LoadedCostume
{
    //outfitのHatId/VisorIdと一致します。
    public string ProductId { get; set; } = "";

    //画像が置かれているディスク上のディレクトリ。zip内のコスチュームはnullです。
    public string? RelatedRawLocalPath { get; set; }

    public int FPS { get; set; }
    public bool Adaptive { get; set; }

    //hatのみ
    public bool IsSkinny { get; set; }

    //visorのみ
    public bool BehindHat { get; set; }
    public bool BackmostBack { get; set; }

    public List<LoadedCostumeImage> Images { get; set; } = [];
}

internal class LoadedCostumeImage
{
    //CosmicImageのフィールド名(Main, Flip, Back, Move, Climb ...)
    public string Layer { get; set; } = "";

    //RelatedRawLocalPathからの相対パス
    public string? Address { get; set; }
    public string? ExAddress { get; set; }
    public string? MaskAddress { get; set; }

    //exレイヤーが前面に出る
    public bool ExIsFront { get; set; }

    //分割画像の数
    public int DivisionX { get; set; }
    public int DivisionY { get; set; }
    //有効な分割画像の枚数
    public int Length { get; set; }

    public List<float>? FPSCurve { get; set; }
}

internal static class LoadedContentsExporter
{
    public const int Version = 20261005;

    static private readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    static public string DirectoryPath => Path.Combine(BepInEx.Paths.BepInExRootPath, "MoreCosmic");
    static public string FilePath => Path.Combine(DirectoryPath, "LoadedContents.json");

    //連結して使うパスなので、区切りをプラットフォームのセパレータに揃えておく
    static private string? ToLocalSeparator(string? path) => path?.Replace('/', Path.DirectorySeparatorChar);

    static private LoadedCostumeImage ToImageEntry(CosmicImage image)
    {
        return new LoadedCostumeImage
        {
            //AllImage()がMyImageTagにフィールド名を入れている
            Layer = image.MyImageTag,
            Address = ToLocalSeparator(image.Address),
            ExAddress = ToLocalSeparator(image.ExAddress),
            MaskAddress = ToLocalSeparator(image.MaskAddress),
            ExIsFront = image.ExIsFront,
            //分割数はDividedSpriteLoaderに渡しているものと同じ解釈
            DivisionX = image.X ?? image.Length ?? 1,
            DivisionY = image.Y ?? 1,
            Length = image.GetLength(),
            FPSCurve = image.FPSCurve,
        };
    }

    static private LoadedCostume ToEntry(CustomCosmicItem item)
    {
        var entry = new LoadedCostume
        {
            ProductId = item.ProductId,
            RelatedRawLocalPath = ToLocalSeparator(item.RelatedRawLocalPath),
            FPS = item.FPS,
        };

        if (item is CosmicHat hat)
        {
            entry.Adaptive = hat.Adaptive;
            entry.IsSkinny = hat.IsSkinny;
        }
        else if (item is CosmicVisor visor)
        {
            entry.Adaptive = visor.Adaptive;
            entry.BehindHat = visor.BehindHat;
            entry.BackmostBack = visor.BackmostBack;
        }

        foreach (var image in item.AllImage()) entry.Images.Add(ToImageEntry(image));

        return entry;
    }

    //読み込み済みコスチュームの一覧を書き出します。読み込みが進むたびに上書きされます。
    static public void Export()
    {
        try
        {
            var contents = new LoadedContents { Version = Version };
            //AllHats/AllVisorsのキーがoutfitから引かれるIdなので、そのキーをそのまま使う
            foreach (var hat in MoreCosmic.AllHats) contents.Hats[hat.Key] = ToEntry(hat.Value);
            foreach (var visor in MoreCosmic.AllVisors) contents.Visors[visor.Key] = ToEntry(visor.Value);

            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(contents, Options), Encoding.UTF8);

            MoreCosmic.Log.Message($"LoadedContents.json has been exported. (Hats: {contents.Hats.Count}, Visors: {contents.Visors.Count})");
        }
        catch (System.Exception e)
        {
            MoreCosmic.Log.Error("Failed to export LoadedContents.json\n" + e.ToString());
        }
    }
}
