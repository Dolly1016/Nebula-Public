using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Attributes;

[Flags]
public enum CallingEvent
{
    PreRoles = 0x01,
    PostRoles = 0x02
}

public class CallingRuleAttribute : Attribute
{
    public CallingEvent MyEventFlag { get; private set; }

    public CallingRuleAttribute(CallingEvent myEventFlag)
    {
        MyEventFlag = myEventFlag;
    }
}
