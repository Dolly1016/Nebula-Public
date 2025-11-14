using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;

namespace Virial.Utilities;

public interface IObserver<T> : Reference<T>
{
}
