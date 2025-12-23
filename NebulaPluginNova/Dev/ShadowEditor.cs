using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Dev;


[NebulaPreprocess(PreprocessPhase.FixStructure)]
internal class ShadowEditor : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess) => DIManager.Instance.RegisterModule(() => DebugTools.UseShadowEditor ? new ShadowEditor() : null);
    private ShadowEditor()
    {
        ModSingleton<ShadowEditor>.Instance = this;
        this.RegisterPermanently();

        lineRenderer = UnityHelper.SetUpLineRenderer("Line", null, Vector3.zero, LayerExpansion.GetDefaultLayer(), 0.05f);
        lineRenderer.SetColors(Color.white, Color.white);
        lineRenderer.loop = false;
        collider = UnityHelper.CreateObject<EdgeCollider2D>("Collider", null, Vector3.zero, LayerExpansion.GetShadowLayer());
    }

    LineRenderer lineRenderer;
    EdgeCollider2D collider;
    List<Vector2> points = [];

    void AddPoint(Vector2 pos)
    {
        points.Add(pos);
        UpdatePoints();
    }

    void RemoveLastPoint()
    {
        if(points.Count > 0) points.RemoveAt(points.Count - 1);
        UpdatePoints();
    }

    void UpdatePoints() {
        collider.SetPoints(points.ToIl2CppList());
        lineRenderer.numPositions = points.Count;
        lineRenderer.SetPositions(points.Select(p => p.AsVector3(-20f)).ToArray());
        lineRenderer.numPositions = points.Count;
        collider.gameObject.SetActive(points.Count != 0);
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        if (NebulaInput.SomeUiIsActive) return;

        if (Input.GetMouseButtonDown(0))
        {
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            AddPoint(pos);
        }
        if(Input.GetMouseButtonDown(1))
        {
            if (Input.GetKey(KeyCode.LeftShift)) points.Clear();
            RemoveLastPoint();
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            var text = "[" + string.Join(", ", points.Select(p => $"new({p.x.ToString("F2")}f, {p.y.ToString("F2")}f)")) + "]";
            ClipboardHelper.PutClipboardString(text);
            DebugScreen.Push("Copied to clipboard!", 3f);
        }

        lineRenderer.gameObject.SetActive(Input.GetKey(KeyCode.LeftShift));
    }
}
