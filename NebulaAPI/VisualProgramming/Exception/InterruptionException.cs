using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming.Exception;

public class InterruptionException : VPException
{
    public InterruptionException(string? message) : base(message: "Internal exception.")
    {
    }
}