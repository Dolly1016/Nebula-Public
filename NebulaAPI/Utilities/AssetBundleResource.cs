using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Virial.Utilities;

internal abstract class AssetBundleResource<T> where T : UnityEngine.Object
{
    abstract protected AssetBundle AssetBundle { get; }

    private string name;
    private T? asset = null;
    public T? Asset { get {
            if(!asset) asset = AssetBundle.LoadAsset(name, Il2CppInterop.Runtime.Il2CppType.Of<T>())?.Cast<T>()!;
            return asset;
        } }

    public AssetBundleResource(string name)
    {
        this.name = name;
    }
}
