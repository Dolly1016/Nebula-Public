using Nebula.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Virial.Media;

namespace Nebula.Modules.Cosmetics;


internal class RingMenu
{
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

    public record RingMenuElement(GUIWidgetSupplier Widget, Action OnClick);
    GameObject obj = null!;
    SpriteRenderer ringRenderer = null!;
    PassiveButton button = null!;
    Func<bool> showWhile = null!;
    RingMenuElement[] elements = null!;
    (GameObject obj, Vector2 origScale)[] generated = null!;
    int currentSelection = -1;
    private Vector2 startPos = Vector2.zero;

    public RingMenu()
    {

    }

    static private Image backgroundSprite = SpriteLoader.FromResource("Nebula.Resources.RingMenu.png", 100f);

    private void OnDestroy()
    {
        if (ClientOption.AllOptions[ClientOption.ClientOptionType.StampMenu].Value == 2)
        {
            SetCursorPos((int)startPos.x, (int)startPos.y);
        }
    }

    public void Show(RingMenuElement[] elements, Func<bool> showWhile)
    {
        if (obj) UnityEngine.Object.Destroy(obj);

        GetCursorPos(out var point);
        startPos = new(point.X, point.Y);

        Vector2 objPos = Vector2.zeroVector;
        if (ClientOption.AllOptions[ClientOption.ClientOptionType.StampMenu].Value != 1)
        {
            objPos = UnityHelper.ScreenToLocalPoint(Input.mousePosition, LayerExpansion.GetUILayer(), HudManager.Instance.transform);
        }

        obj = UnityHelper.CreateObject("RingMenu", HudManager.Instance.transform, objPos);
        obj.transform.SetLocalZ(-780f);

        generated = new (GameObject obj, Vector2 origScale)[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            var widget = element.Widget.Invoke().Instantiate(new(2f, 2f), out _);
            if (widget != null)
            {
                widget.transform.SetParent(obj.transform);
                widget.transform.localPosition = Vector3.right.RotateZ(-180f + (0.5f + i) * 360f / elements.Length) * 0.68f;

                generated[i] = (widget, widget.transform.localScale);

                widget.transform.localScale *= 0.9f;
            }
        }

        var collider = obj.AddComponent<CircleCollider2D>();
        collider.radius = 2.2f;
        collider.isTrigger = true;
        ringRenderer = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, new Vector3(0f, 0f, 0.1f));
        ringRenderer.sprite = backgroundSprite.GetSprite();
        ringRenderer.material.shader = NebulaAsset.RingMenuShader;
        ringRenderer.material.SetColor("_Color1", Color.green.AlphaMultiplied(0.5f));
        ringRenderer.material.SetColor("_Color2", new Color(0.2f, 0.2f, 0.2f, 0.7f));
        ringRenderer.material.SetFloat("_Guage", 0f);
        button = obj.SetUpButton(false);
        button.OnClick.AddListener(() =>
        {
            if (currentSelection != -1)
            {
                VanillaAsset.PlaySelectSE();

                elements[currentSelection].OnClick.Invoke();
                UnityEngine.Object.Destroy(obj);
                obj = null!;
                showWhile = null!;
                OnDestroy();
            }
        });
        this.showWhile = showWhile;
        this.elements = elements;
        currentSelection = -1;
    }


    public void Update()
    {
        if (obj)
        {
            if (!(showWhile?.Invoke() ?? true))
            {
                if (currentSelection != -1)
                {
                    VanillaAsset.PlaySelectSE();
                    elements[currentSelection].OnClick.Invoke();
                }

                UnityEngine.Object.Destroy(obj);
                OnDestroy();
                obj = null!;
                showWhile = null!;
            }
            else
            {
                var pos = UnityHelper.ScreenToLocalPoint(Input.mousePosition, LayerExpansion.GetUILayer(), obj.transform);
                pos.z = 0f;
                if (pos.magnitude > 0.3f && PassiveButtonManager.Instance.currentOver && PassiveButtonManager.Instance.currentOver.GetInstanceID() == button.GetInstanceID())
                {
                    int length = elements.Length;
                    var nextSelection = (int)((float)(Math.Atan2(pos.y, pos.x) + Math.PI) / (Math.PI * 2.0) * length);
                    if (nextSelection != currentSelection)
                    {
                        ringRenderer.material.SetFloat("_Guage", 1f / length);
                        var unit = 360f / length;
                        ringRenderer.transform.localEulerAngles = new(0f, 0f, -90f - 180f + unit * (nextSelection + 1));
                        currentSelection = nextSelection;

                        VanillaAsset.PlayHoverSE();
                    }
                }
                else if (currentSelection != -1)
                {
                    currentSelection = -1;
                    ringRenderer.material.SetFloat("_Guage", 0f);
                }

                for (int i = 0; i < generated.Length; i++)
                {
                    generated[i].obj.transform.localScale = (generated[i].origScale * (currentSelection == i ? 1.4f : 0.9f)).AsVector3(1f);
                    generated[i].obj.transform.SetLocalZ(currentSelection == i ? -0.5f : -0.05f);
                }
            }
        }
    }
}