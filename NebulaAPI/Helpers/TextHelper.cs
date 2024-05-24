using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Text;

namespace Virial.Helpers;

static public class TextHelper
{
    internal class CompareTextComponent : TextComponent
    {
        private TextComponent inner;
        private string textForCompare;
        string TextComponent.GetString() =>inner.GetString();
        string TextComponent.TextForCompare => textForCompare;

        public CompareTextComponent(TextComponent inner, string textForCompare)
        {
            this.inner = inner;
            this.textForCompare = textForCompare;
        }
    }

    static public TextComponent WithComparison(this TextComponent inner, string textForCompare) => new CompareTextComponent(inner, textForCompare);
}
