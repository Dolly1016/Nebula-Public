using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Logging;

public interface ILogger
{
    void Debug(string message);
    void Message(string message);
    void Warning(string message);
    void Error(string message);
}
