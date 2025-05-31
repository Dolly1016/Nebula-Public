using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming.Exception;

public sealed class BrokenProgramException : VPException
{
    public BrokenProgramException(string? message) : base(message : "The program is broken.")
    {
    }
}
