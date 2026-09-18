using JetBrains.Annotations;
using MS.Internal.Xml.XPath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Helpers;

internal struct ChildInfo
{
    private int index;
    private string? name;
    private ChildInfo(int index, string? name)
    {
        this.index = index;
        this.name = name;
    }
    public override string ToString() => name != null ? name : index.ToString();

    internal UnityEngine.Transform TryGetChild(UnityEngine.Transform transform)
    {
        if(name != null) return transform.FindChild(name);
        return transform.GetChild(index);
    }

    static public implicit operator ChildInfo(int index) => new(index, null);
    static public implicit operator ChildInfo(string name) => new(0, name);
}
internal static class UnityEngineHelper
{
    static private Virial.Logging.ILogger logger { get { field ??= NebulaAPI.Logging.CombinedLogger(NebulaAPI.Logging.NebulaLogger("UnityHelper"), NebulaAPI.Logging.BepInExLogger()); return field; } }
    static internal bool TryFindChild(this UnityEngine.Transform transform, out UnityEngine.Transform found, params ChildInfo[] route)
    {
        var firstTransform = transform;
        found = null!;
        int num = 0;
        foreach(var info in route)
        {
            if (!transform.AsBoolFast())
            {
                if (firstTransform.AsBoolFast())
                {
                    logger.Error($"No child is found. (route: [{string.Join(", ", route)}], failed at: {num}, {info})");
                }
                else
                {
                    logger.Error("Tried to search child for invalid transform.");
                }
                return false;
            }
            transform = info.TryGetChild(transform);
            num++;
        }
        found = transform;
        return found.AsBoolFast();
    }
}
