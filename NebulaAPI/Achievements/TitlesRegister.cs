using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Achievements;

interface TitlesRegister
{
    bool Register(string group, string id, bool isSecret, DefinedAssignable? relatedRole);
}
