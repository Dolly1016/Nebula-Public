using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Runtime;
using Virial.Text;
using Virial.VisualProgramming;

namespace Nebula.VisualProgramming;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class NodesCollection : INodesCollection
{
    private readonly Dictionary<string, NodeFactory> allFactories = [];
    public static NodesCollection Default = new();
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        /*
        Default.allFactories.Add("__add", id => new FloatBinaryNode((n1, n2) => n1 + n2, id[0]));
        Default.allFactories.Add("__sub", id => new FloatBinaryNode((n1, n2) => n1 - n2, id[0]));
        Default.allFactories.Add("__mul", id => new FloatBinaryNode((n1, n2) => n1 * n2, id[0]));
        Default.allFactories.Add("__div", id => new FloatBinaryNode((n1, n2) => n1 / n2, id[0]));
        Default.allFactories.Add("read", id => new InputNode(id[0]));
        */
    }

    public bool TryGetNode(string operation, [MaybeNullWhen(false)] out NodeFactory factory) => allFactories.TryGetValue(operation, out factory);

    public void AddNode(string operation, NodeFactory factory) => allFactories[operation] = factory;
}

public class MixedNodesCollection : INodesCollection
{
    private readonly List<INodesCollection> collections = [];
    public MixedNodesCollection() { }
    public void AddCollection(INodesCollection collection) => collections.Add(collection);
    public bool TryGetNode(string operation, [MaybeNullWhen(false)] out NodeFactory factory)
    {
        foreach (var collection in collections)
        {
            if (collection.TryGetNode(operation, out factory)) return true;
        }
        factory = null;
        return false;
    }
}


/*
public class FloatBinaryNode : Virial.VisualProgramming.INode
{
    IVPCalculable left, right;
    VPCacheValue? output;
    EvaluatorFactory evaluator;
    string operation;

    public int InputLength => 2;
    public int OutputLength => 1;

    string INode.Operation => operation;

    private IEnumerator CoCalc(VPEnvironment env, Func<float, float, float> operation)
    {
        if (output == null) yield break;

        yield return left.PrepareIfNeeded(env);
        yield return right.PrepareIfNeeded(env);

        var leftVal = left.Get(env);
        var rightVal = right.Get(env);
        var contextDependent = left.IsContextDependent || right.IsContextDependent;

        var outputRawVal = new VPFloat(operation.Invoke(left.Get(env).GetFloat(), right.Get(env).GetFloat()));
        output.Set(env, outputRawVal, contextDependent);
    }
    public FloatBinaryNode(Func<float, float, float> operation, int outputId)
    {
        evaluator = (env) => CoCalc(env, operation);
        output = outputId == -1 ? null : new VPCacheValue(evaluator, outputId);
    }

    public void SetInput(int index, IVPCalculable value)
    {
        if (index == 0)
            left = value;
        else
            right = value;
    }
    public IVPCalculable GetInput(int index) => index == 0 ? left : right;

    public IVPCalculable GetOutput(int index) => output!;
}

public class InputNode : Virial.VisualProgramming.INode
{
    IVPCalculable text = null!;
    VPCacheValue? output = null!;
    EvaluatorFactory evaluator;
    string INode.Operation => "read";

    public int InputLength => 1;
    public int OutputLength => 1;

    private IEnumerator CoCalc(VPEnvironment env)
    {
        if (output == null) yield break;

        yield return text.PrepareIfNeeded(env);

        var textVal = text.Get(env);

        bool finished = false;
        string input = null!;
        var window = MetaScreen.GenerateWindow(new(5f, 3f), HudManager.InstanceExists ? HudManager.Instance.transform : null, Vector3.zero, true, false, true, BackgroundSetting.Modern);
        window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
            GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.DocumentTitle, textVal.GetString()),
            new Modules.GUIWidget.GUITextField(Virial.Media.GUIAlignment.Left, new(4.6f, 0.3f))
            {
                MaxLines = 1,
                EnterAction = text => { finished = true; input = text; window.CloseScreen(); return true; }
            }), out _);
        while (!finished || window) yield return null;

        if (finished) output?.Set(env, new VPString(input!), true);
    }
    public InputNode(int outputId)
    {
        evaluator = (env) => CoCalc(env);
        output = outputId == -1 ? null : new VPCacheValue(evaluator, outputId);
    }

    public void SetInput(int index, IVPCalculable value) => text = value;
    public IVPCalculable GetInput(int index) => text;

    public IVPCalculable GetOutput(int index) => output!;
}
*/