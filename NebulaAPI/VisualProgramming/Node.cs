using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.VisualProgramming.Exception;

namespace Virial.VisualProgramming;

public interface INode
{
    string Operation { get; }
    void SetInput(int index, IVPCalculable value);
    IVPCalculable GetInput(int index);
    IVPCalculable GetOutput(int index);
    int GetFollowerId(int branch);
    IEnumerator CoEvaluate(VPEnvironment env);

    int InputLength { get; }
    int OutputLength { get; }
    int FollowersLength { get; }
}

public abstract class AbstractExecutableNode : Virial.VisualProgramming.INode
{
    public abstract string Operation { get; }
    public abstract int InputLength { get; }
    public abstract int OutputLength { get; }
    private int[] followers;
    public int FollowersLength => followers.Length;
    public abstract IVPCalculable GetInput(int index);
    public abstract IVPCalculable GetOutput(int index);
    public abstract void SetInput(int index, IVPCalculable value);

    public int GetFollowerId(int branch) => (branch >= 0 && branch < followers.Length) ? followers[branch] : -1;
    public abstract IEnumerator CoEvaluate(VPEnvironment env);
    protected IEnumerator CoEvaluateFollower(VPEnvironment env, int branch)
    {
        var node = env.GetNode(GetFollowerId(branch));
        if (node != null) yield return node.CoEvaluate(env);
    }
}

public class FunctionNode : AbstractExecutableNode
{
    IVPCalculable[] inputs;
    ICircuit circuit;
    VPCacheValue[] outputs;
    EvaluatorFactory evaluator;

    string operation;
    override public string Operation => operation;

    override public int InputLength => inputs.Length;
    override public int OutputLength => outputs.Length;

    private IEnumerator CoCalc(VPEnvironment env)
    {
        IVPNumeric[] numericInputs = new IVPNumeric[inputs.Length];
        for(int i = 0;i<inputs.Length;i++)
        {
            yield return inputs[i].PrepareIfNeeded(env);
            numericInputs[i] = inputs[i].Get(env);
        }

        var instanceEnv = circuit.GenerateInstance(numericInputs);

        for (int i = 0; i < outputs.Length; i++)
        {
            var output = circuit.GetOutput(i);
            outputs[i].Set(env, output.Get(instanceEnv), output.IsContextDependent);
        }

        yield return CoEvaluateFollower(env, 0);
    }
    public FunctionNode(int inputs, int[] outputs, int followers)
    {
        evaluator = (env) => CoCalc(env);
        this.outputs = new VPCacheValue[outputs.Length];
        for (int i = 0; i < outputs.Length; i++) this.outputs[i] = new VPCacheValue(evaluator, outputs[i]);
    }

    override public void SetInput(int index, IVPCalculable value)
    {
        inputs[index] = value;
    }
    override public IVPCalculable GetInput(int index) => inputs[index];

    override public IVPCalculable GetOutput(int index) => outputs[index];

    public override IEnumerator CoEvaluate(VPEnvironment env) => evaluator.Invoke(env);
}