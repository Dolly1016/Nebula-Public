using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming.Exception;

public class VPException : System.Exception
{
    public VPException(string message) : base(message)
    {
    }

    public override string ToString()
    {
        return $"{GetType()}: {Message}";
    }
}
