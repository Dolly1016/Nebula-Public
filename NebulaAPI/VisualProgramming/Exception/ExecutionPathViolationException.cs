using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming.Exception;

public sealed class ExecutionPathViolationException : VPException
{
    public ExecutionPathViolationException(string? message) : base(message: "An unreached value was accessed.")
    {
    }
}
