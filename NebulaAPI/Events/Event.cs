using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events;

public interface Event
{
}

public interface ICancelableEvent
{
    public bool IsCanceled { get; }
}