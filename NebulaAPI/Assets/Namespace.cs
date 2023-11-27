using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Media;

namespace Virial.Assets;

public interface INameSpace
{
    Stream? OpenRead(string innerAddress);
    Image? GetImage(string innerAddress, float pixelsPerUnit = 100f);
}
