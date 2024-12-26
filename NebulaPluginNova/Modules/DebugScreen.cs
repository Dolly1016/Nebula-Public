using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial;

namespace Nebula.Modules;

public interface IDebugTextContent {
    string? Text { get; }
    ILifespan Lifespan { get; }
}

public class FunctionalDebugTextContent : IDebugTextContent
{
    private Func<string?> text;
    private ILifespan lifespan;
    string? IDebugTextContent.Text => text.Invoke();
    ILifespan IDebugTextContent.Lifespan => lifespan;

    public FunctionalDebugTextContent(Func<string?> text, ILifespan lifespan)
    {
        this.text = text;
        this.lifespan = lifespan;
    }
}

public class DebugScreen
{
    static private List<IDebugTextContent> allContents = new();
    TextMeshPro text;
    SpriteRenderer background;

    private class TemporaryLog : IDebugTextContent
    {
        string text;
        ILifespan lifespan;
        public TemporaryLog(string text, float time)
        {
            this.text = text;
            this.lifespan = FunctionalLifespan.GetTimeLifespan(time);
        }
        string? IDebugTextContent.Text => text;
        ILifespan IDebugTextContent.Lifespan => lifespan;
    }

    static public void Push(IDebugTextContent content) => allContents.Add(content);
    static public void Push(string text, float duration) => Push(new TemporaryLog(text, duration));

    public DebugScreen(Transform transform)
    {
        try
        {
            new NoSGUIText(Virial.Media.GUIAlignment.TopLeft, new(GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent)) { FontSize = new(1.5f, false) }, new RawTextComponent("")) { PostBuilder = text => this.text = text }.Instantiate(new Virial.Compat.Size(0f, 0f), out _);
            text!.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text!.transform.SetParent(HudManager.InstanceExists ? HudManager.Instance.transform : transform);

            var aspectPosition = text!.gameObject.AddComponent<AspectPosition>();
            aspectPosition.Alignment = AspectPosition.EdgeAlignments.LeftTop;
            aspectPosition.DistanceFromEdge = new(0.1f, 0.1f, -800f);
            aspectPosition.anchorPoint = new(0f, 0f);
            aspectPosition.AdjustPosition();

            background = UnityHelper.CreateObject<SpriteRenderer>("Background", text.transform, Vector3.zero, LayerExpansion.GetUILayer());
            background.sprite = NebulaAsset.SharpWindowBackgroundSprite.GetSprite();
            background.drawMode = SpriteDrawMode.Sliced;
            background.tileMode = SpriteTileMode.Continuous;
            background.color = new Color(0f, 0f, 0f, 0.8f);
        }
        catch (Exception ex)
        {
            text = null!;
            background = null!;
        }

        IEnumerator CoUpdate()
        {
            while (true)
            {
                Update();
                yield return null;
            }
        }
        NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
    }

    private void Update()
    {
        if (!text) return;

        StringBuilder sb = new();
        allContents.RemoveAll(c =>
        {
            if (c.Lifespan.IsDeadObject) return true;
            string? t = c.Text;
            if (t != null) sb.AppendLine(t);
            return false;
        });
        string content = sb.ToString();

        if (content.IsEmpty())
        {
            text.gameObject.SetActive(false);
            return;
        }
        else
        {
            text.gameObject.SetActive(true);
            text.text = content;
        }



        Vector2 size = text.bounds.size;
        background.transform.localPosition = new Vector3(size.x, -size.y) * 0.5f;
        background.size = new Vector3(size.x + 0.2f, size.y + 0.2f, 1f);
    }
}
