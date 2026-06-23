using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace Nebula.Utilities;

internal class TextMeshProHandler
{
    private TextMeshPro text;

    public TextMeshPro MyTextComponent => text;

    public bool IsActive => text.AsBoolFast();
    
    private string? lastText = null;
    private string? currentText = null;

    private Color? lastColor = null;
    private Color? currentColor = null;

    private bool? lastVisibility = null;
    private bool? currentVisibility = null;

    public TextMeshProHandler(TextMeshPro text)
    {
        this.text = text;
    }

    public void RequestChange(string text)
    {
        currentText = text;
    }

    public void RequestChange(Color color)
    {
        currentColor = color;
    }

    public void RequestVisibility(bool active)
    {
        currentVisibility = active;
    }

    public void Reflect()
    {
        if (currentColor.HasValue)
        {
            if (!lastColor.HasValue) {
                text.color = currentColor.Value;
            }
            else if (
                currentColor.Value.r != lastColor.Value.r ||
                currentColor.Value.g != lastColor.Value.g ||
                currentColor.Value.b != lastColor.Value.b ||
                currentColor.Value.a != lastColor.Value.a
                )
            {
                text.color = currentColor.Value;
            }
            lastColor = currentColor;
            currentColor = null;
        }

        if(currentText != null)
        {
            if(lastText != currentText)
            {
                text.text = currentText;
                lastText = currentText;
            }
            currentText = null;
        }

        if(currentVisibility.HasValue)
        {
            if(!lastVisibility.HasValue || lastVisibility.Value == currentVisibility.Value)
            {
                text.gameObject.SetActive(currentVisibility.Value);
            }
            lastVisibility = currentVisibility;
            currentVisibility = null;
        }
    }
}
