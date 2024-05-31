using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.GUI.Drawing.Elements
{
    public class RectangleWithText : RectangleElement
    {
        public RectangleWithText(Rectangle rectangle, Pen borderPen, string text, EnumPosition textPosition, Font font, Brush textBrush) : base(rectangle, borderPen)
        {
            Text = text;
            TextPosition = textPosition;
            Font = font;
            TextBrush = textBrush;
        }

        public string Text { get; }
        public EnumPosition TextPosition { get; }
        public Font Font { get; }
        public Brush TextBrush { get; }

        public enum EnumPosition
        {
            TopLeft,
            TopCenter,
            TopRight,
            Left,
            Center,
            Right,
            BottomLeft,
            BottomCenter,
            BottomRight,
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
