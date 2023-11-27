using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Text;

public interface CommunicableTextTag
{
    public string TranslationKey { get; }

    internal int Id { get; }
    internal string Text { get; }
}
