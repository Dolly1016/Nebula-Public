using Virial;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Utilities;

[NebulaPreprocess(PreprocessPhase.FixStructure)]
public class TranslatableTag : CommunicableTextTag
{
    static public List<TranslatableTag> AllTag = new();

    public string TranslateKey { get; private set; }
    string CommunicableTextTag.TranslationKey => TranslateKey;

    public string Text => Language.Translate(TranslateKey);
    public int Id { get;private set; }

    
    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        AllTag.Sort((tag1,tag2 )=> tag1.TranslateKey.CompareTo(tag2.TranslateKey));
        for (int i = 0; i < AllTag.Count; i++) AllTag[i].Id = i;
    }

    public TranslatableTag(string translateKey)
    {
        TranslateKey = translateKey;

        if (NebulaAPI.Preprocessor?.FinishPreprocess ?? true)
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.FatalError, "Pre-loading has been finished. Translatable tag \"" + TranslateKey + "\" is invalid on current process.");
        else
            AllTag.Add(this);
        
    }

    static public TranslatableTag? ValueOf(int id)
    {
        if(id < AllTag.Count && id>=0)
            return AllTag[id];
        return null;
    }
}
