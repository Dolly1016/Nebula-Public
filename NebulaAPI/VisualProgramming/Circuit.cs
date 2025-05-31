using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming;

public interface ICircuit
{
    /// <summary>
    /// 入力をもとに回路のインスタンスを作成します。
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    VPEnvironment GenerateInstance(IVPNumeric[] input);
    
    /// <summary>
    /// 出力を取得します。
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    IVPCalculable GetOutput(int index);

    INode GetNode(int id);

    int InputLength { get; }
    int OutputLength { get; }
}
