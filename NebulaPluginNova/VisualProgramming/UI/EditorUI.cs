using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using Virial.Text;

namespace Nebula.VisualProgramming.UI;

internal class FunctionBlock
{
    GameObject block, group;
    SpriteRenderer innerRenderer, frameRenderer, bannerRenderer;
    SpriteMask mask;

    const float frameScale = 0.3f;
    private void GenerateBackground()
    {
        innerRenderer = UnityHelper.CreateObject<SpriteRenderer>("Inner", block.transform, new Vector3(0, 0, 0.1f));
        innerRenderer.sprite = MetaScreen.BackInnerImage.GetSprite();
        innerRenderer.drawMode = SpriteDrawMode.Sliced;
        innerRenderer.tileMode = SpriteTileMode.Continuous;
        innerRenderer.gameObject.layer = LayerExpansion.GetUILayer();
        innerRenderer.color = Color.white.RGBMultiplied(0.55f);
        innerRenderer.transform.localScale = new(frameScale, frameScale, 1f);

        frameRenderer = UnityHelper.CreateObject<SpriteRenderer>("Frame", block.transform, new Vector3(0, 0, -0.1f));
        frameRenderer.sprite = MetaScreen.BackFrameImage.GetSprite();
        frameRenderer.drawMode = SpriteDrawMode.Sliced;
        frameRenderer.tileMode = SpriteTileMode.Continuous;
        frameRenderer.gameObject.layer = LayerExpansion.GetUILayer();
        frameRenderer.transform.localScale = new(frameScale, frameScale, 1f);

        bannerRenderer = UnityHelper.CreateObject<SpriteRenderer>("Frame", block.transform, new Vector3(0, 0, 0.095f));
        bannerRenderer.sprite = VanillaAsset.FullScreenSprite;
        bannerRenderer.gameObject.layer = LayerExpansion.GetUILayer();
        

        group = UnityHelper.CreateObject<SortingGroup>("Group", block.transform, Vector3.zero).gameObject;
        mask = UnityHelper.CreateObject<SpriteMask>("Mask", group.transform, Vector3.zero);
        mask.sprite = VanillaAsset.FullScreenSprite;
    }

    private void Resize(Vector2 size)
    {
        innerRenderer.size = size / frameScale + new Vector2(1f, 1f);
        frameRenderer.size = size / frameScale + new Vector2(1f, 1f);
        mask.transform.localScale = size;

        bannerRenderer.transform.localScale = new(size.x + 0.15f, 0.35f);
        bannerRenderer.transform.localPosition = new(0f, size.y * 0.5f - 0.1f, 0.095f);
    }

    public void SetUp()
    {
        var instantiated = GUI.API.VerticalHolder(Virial.Media.GUIAlignment.TopLeft,
            new Modules.GUIWidget.NoSGUIText(Virial.Media.GUIAlignment.Left, AttributeAsset.DocumentTitle, new RawTextComponent("Test Function")) { 
                PostBuilder = text => text.outlineColor = Color.clear
            },
            GUI.API.VerticalMargin(0.08f),
            GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.DocumentStandard, "Properties")
            ).Instantiate(new(10f,10f), out var actualSize);
        instantiated?.transform.SetParent(group.transform, false);
        bannerRenderer.color = Color.green.AlphaMultiplied(0.3f);
        
        Resize(actualSize.ToUnityVector());
    }

    public FunctionBlock(Transform parent, Vector3 localPos)
    {
        block = UnityHelper.CreateObject("Block", parent, localPos);
        GenerateBackground();
        SetUp();
    }
}

internal class EdgeGenerator {
    Vector2[] targets;
    Vector2 from;
    LineRenderer line;
    bool isActive = true;
    public bool IsActive => isActive;

    public EdgeGenerator(Vector2 from, Vector2[] targets) {
        this.from = from;
        this.targets = targets;
        line = UnityHelper.SetUpLineRenderer("Line", HudManager.Instance.transform, new(0f, 0f, -100f), width: 0.02f);
        line.SetPositions((Vector3[])[from, from]);
        line.SetColors(Color.white, Color.white);
    }

    public void Update()
    {
        if(!isActive) return;

        Vector2 mousePos = HudManager.Instance.transform.InverseTransformPoint(HudManager.Instance.UICamera.ScreenToWorldPoint(Input.mousePosition));

        int currentTarget = -1;
        float distance = 0.2f;
        for (int i = 0; i < targets.Length; i++) {
            float d = mousePos.Distance(targets[i]);
            if (d < distance)
            {
                currentTarget = i;
                distance = d;
            }
        }

        if (!Input.GetMouseButton(0))
        {
            //マウスを離したとき
            isActive = false;

            if(currentTarget == -1)
            {
                GameObject.Destroy(line.gameObject);
            }
            else
            {
                line.SetPosition(1, targets[currentTarget]);
            }
        }
        else
        {
            if (currentTarget == -1)
            {
                line.SetPosition(1, mousePos);
            }
            else
            {
                line.SetPosition(1, targets[currentTarget]);
            }
        }
    }
}
