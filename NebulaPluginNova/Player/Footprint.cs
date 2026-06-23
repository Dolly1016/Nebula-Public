using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.DI;
using Virial.Game.Object;
using Virial.Runtime;

namespace Nebula.Player;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class FootprintStateImpl : AbstractModule<Virial.Game.Game>, FootprintState
{
    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule(() => new FootprintStateImpl());
    }

    Dictionary<string, ILifespan> tags = [];
    void FootprintState.SetAsInvisible(string attributeTag, ILifespan lifespan)
    {
        if(tags.TryGetValue(attributeTag, out var existed))
        {
            if(existed is CombinedInternalLifespan cil)
            {
                cil.Add(lifespan);
            }
            else if(existed.IsAliveObject)
            {
                CombinedInternalLifespan newCil = new();
                newCil.Add(existed);
                newCil.Add(lifespan);
                tags[attributeTag] = newCil;
            }
            else
            {
                tags[attributeTag] = lifespan;
            }
        }
        else
        {
            tags[attributeTag] = lifespan;
        }
    }

    bool FootprintState.CheckVisibility(string attributeTag)
    {
        return tags.TryGetValue(attributeTag, out var lifespan) ? !lifespan.IsAliveObject : true;
    }
}

