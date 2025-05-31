using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FilterPopUp.FilterInfoUI;
using UnityEngine;

namespace Nebula.Utilities;

static public class PremultipliedConversion
{
    static public void ConvertImages(string srcPath, string dstPath)
    {
        if (!Directory.Exists(srcPath))
        {
            Directory.CreateDirectory(srcPath);
            MetaUI.ShowConfirmDialog(null, new TranslateTextComponent("ui.utility.premultipliedConversion.folderGenerated"));
            return;
        }
        var files = Directory.GetFiles(srcPath).Where(path => path.EndsWith(".png")).ToArray();
        if(files.Length == 0)
        {
            MetaUI.ShowConfirmDialog(null, new TranslateTextComponent("ui.utility.premultipliedConversion.noImages"));
            return;
        }
        var startDialog = MetaUI.ShowConfirmDialog(null, new RawTextComponent(Language.Translate("ui.utility.premultipliedConversion.start").Replace("%NUM%", files.Length.ToString())));

        if(!Directory.Exists(dstPath)) Directory.CreateDirectory(dstPath);

        int converted = 0;
        IEnumerator CoConvert(string path)
        {
            string fileName =  Path.GetFileName(path);

            Texture2D texture = GraphicsHelper.LoadTextureFromDisk(path, true);
            Color[] pixels = texture.GetPixels();
            bool maybeNotMultiplied = false;

            var task = Parallel.For(0, pixels.Length, i => { 
                var pixel = pixels[i];
                if (pixel.r > pixel.a || pixel.g > pixel.a || pixel.b > pixel.a) maybeNotMultiplied = true;
                pixels[i] = new(pixel.r * pixel.a, pixel.g * pixel.a, pixel.b * pixel.a, pixel.a); 
            });

            while (!task.IsCompleted) yield return null;
            texture.SetPixels(pixels);

            if (maybeNotMultiplied)
            {
                converted++;
                var bytes = texture.EncodeToPNG();
                yield return File.WriteAllBytesAsync(dstPath + Path.DirectorySeparatorChar + fileName, bytes).WaitAsCoroutine();
            }

            GameObject.Destroy(texture);
        }
        NebulaManager.Instance.StartCoroutine(ManagedEffects.Sequence([..files.Select(path => CoConvert(path)), ManagedEffects.Action(() => {
            if(startDialog) startDialog.CloseScreen();
            MetaUI.ShowConfirmDialog(null,  new RawTextComponent(Language.Translate("ui.utility.premultipliedConversion.finish").Replace("%MAX%", files.Length.ToString()).Replace("%NUM%", converted.ToString())));
        })]).WrapToIl2Cpp());
    }
}
