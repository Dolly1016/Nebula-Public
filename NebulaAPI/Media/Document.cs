using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;

namespace Virial.Media;

public interface IDocument
{
    /// <summary>
    /// クリック操作等によって画面遷移をする場合には、targetを使用することもできます。
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    GUIWidget? Build(Artifact<GUIScreen>? target);
}

public interface IDocumentWithId : IDocument
{
    void OnSetId(string documentId);
}