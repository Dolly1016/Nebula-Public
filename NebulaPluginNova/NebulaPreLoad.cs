using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal class NebulaPreLoad : Attribute
{
    public Type[] PreLoadTypes { get; private set; }
    public Type[] PostLoadTypes { get; private set; }
    public bool IsFinalizer { get; private set; }
    public NebulaPreLoad(params Type[] preLoadType) : this(false, preLoadType, []) { }
    public NebulaPreLoad(bool isFinalizer, params Type[] preLoadType) : this(isFinalizer, preLoadType, []) { }

    public NebulaPreLoad(bool isFinalizer, Type[] preLoad, Type[] postLoad)
    {
        PreLoadTypes = preLoad ?? [];
        PostLoadTypes = postLoad ?? [];
        IsFinalizer = isFinalizer;
    }

    static public bool FinishedLoading => PreloadManager.FinishedPreload;
}