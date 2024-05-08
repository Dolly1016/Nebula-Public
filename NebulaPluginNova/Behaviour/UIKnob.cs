using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behaviour;

public class UIKnob : Scrollbar
{
    static UIKnob() => ClassInjector.RegisterTypeInIl2Cpp<UIKnob>();
    public UIKnob(System.IntPtr ptr) : base(ptr) { }
    public UIKnob() : base(ClassInjector.DerivedConstructorPointer<UIKnob>())
    { ClassInjector.DerivedConstructorBody(this); }

    public (float min, float max) Range = (0.0f, 0.0f);
    public bool IsVert = true;

    public Action? OnHold = null, OnRelease = null;
    public Action<float>? OnDragging = null;
    public SpriteRenderer? Renderer = null;

    private bool isHolding = false;

    public bool IsHolding { get => isHolding; private set {
            if (value == isHolding) return;
            isHolding = value;
            try
            {
                if (value)
                    OnHold?.Invoke();
                else
                    OnRelease?.Invoke();
            }
            catch { }
        } 
    }

    public override void ReceiveClickDown()
    {
        IsHolding = true;
    }

    public override void ReceiveClickUp()
    {
        PassiveButtonManager.Instance.controller.amTouching = null;
        try
        {
            IsHolding = false;
        }
        catch { }
    }

    public override void ReceiveClickDrag(Vector2 dragDelta)
    {
        try
        {
            float delta = IsVert ? dragDelta.y : dragDelta.x;

            var localPos = transform.localPosition;
            localPos += new Vector3(IsVert ? 0f : delta, IsVert ? delta : 0f, 0f);
            if (IsVert)
                localPos.y = Math.Clamp(localPos.y, Range.min, Range.max);
            else
                localPos.x = Math.Clamp(localPos.x, Range.min, Range.max);
            transform.localPosition = localPos;

            OnDragging?.Invoke(IsVert ? localPos.y : localPos.x);
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
    }

    public void SetValue(float value)
    {
        var localPos = transform.localPosition;
        if (IsVert)
            localPos.y = Math.Clamp(value, Range.min, Range.max);
        else
            localPos.x = Math.Clamp(value, Range.min, Range.max);
        transform.localPosition = localPos;
    }

    public override void ReceiveMouseOver()
    {
        if (Renderer) Renderer!.color = Color.white.RGBMultiplied(0.5f);
    }

    public override void ReceiveMouseOut()
    {
        if (Renderer) Renderer!.color = Color.white;
    }


    public override bool HandleDown => true;
    public override bool HandleUp => true;
    public override bool HandleDrag => true;
}
