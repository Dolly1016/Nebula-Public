using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Listeners;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal partial class NebulaGameEventListeners : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule(() => new NebulaGameEventListeners().RegisterPermanently());
    }
}
