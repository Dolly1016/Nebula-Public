using Il2CppSystem.CodeDom;
using Il2CppSystem.Xml.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.VisualProgramming;

public delegate IEnumerator EvaluatorFactory(VPEnvironment env);

public interface IVPCalculable
{
    IEnumerator PrepareIfNeeded(VPEnvironment env);
    IVPNumeric Get(VPEnvironment env);

    /// <summary>
    /// 値のIDを返します。-1を除き、VP回路内で重複しない値です。
    /// </summary>
    /// <returns>-1の場合、IDが割り振られない定数値。0以上の場合、回路内で重複しないID</returns>
    int Id { get; }

    /// <summary>
    /// 計算がコンテキスト依存だった場合はtrueを返すようにしてください。
    /// PrepareIfNeededの実行以前では、値を使用しないでください。
    /// </summary>

    bool IsContextDependent { get; }
}

public class VPCacheValue : IVPCalculable
{
    private IVPNumeric? immutableCache;
    private int id;
    private EvaluatorFactory evaluator;
    private bool isContextDependent;
    public IEnumerator PrepareIfNeeded(VPEnvironment env)
    {
        if (immutableCache != null) yield break;
        if (env.TryGetValue(id, out _)) yield break;
        yield return evaluator.Invoke(env);
    }
    public VPCacheValue(EvaluatorFactory evaluator, int id)
    {
        this.evaluator = evaluator;
        this.id = id;
    }
    public IVPNumeric Get(VPEnvironment env) => env.TryGetValue(id, out var val) ? val : immutableCache;
    private void SetImmutableCache(IVPNumeric numeric) => this.immutableCache = numeric;
    public void Set(VPEnvironment env, IVPNumeric numeric, bool contextDependent)
    {
        if (contextDependent)
        {
            MarkAsContextDependentValue();
            env.PushContextIndependentValue(Id, numeric);
        }
        else
        {
            SetImmutableCache(numeric);
        }
    }

    public int Id => id;
    public bool IsContextDependent => isContextDependent;
    public void MarkAsContextDependentValue() => isContextDependent = true;
}

public class VPConstant : IVPCalculable
{
    private IVPNumeric numeric;
    public IEnumerator PrepareIfNeeded(VPEnvironment env) { yield break; }
    public VPConstant(IVPNumeric numeric)
    {
        this.numeric = numeric;
    }
    public IVPNumeric Get(VPEnvironment env) => numeric;
    public int Id => -1;
    public bool IsContextDependent => false;
}


public class VPDummyValue : IVPCalculable
{
    private int id;
    public IEnumerator PrepareIfNeeded(VPEnvironment env) { yield break; }
    public VPDummyValue(int id)
    {
        this.id = id;
    }
    public IVPNumeric Get(VPEnvironment env) {
        if (env.TryGetValue(this.id, out var result))
            return result;
        else
            throw new InvalidOperationException();
    }
    public int Id => id;
    public bool IsContextDependent => true;
}

