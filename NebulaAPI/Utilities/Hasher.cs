using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public interface IHasher
{
    int GetIntegerHash(string text);
    long GetLongHash(string text);
}
