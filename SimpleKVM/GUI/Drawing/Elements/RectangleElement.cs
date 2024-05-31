using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.GUI.Drawing.Elements
{
    public class RectangleElement : Container
    {

        public Rectangle Rectangle { get; set; }
        public Pen Pen { get; }

        public RectangleElement(Rectangle rectangle, Pen pen)
        {
            Rectangle = rectangle;
            Pen = pen;
        }

        public override bool ContainsPoint(Point p)
        {
            var result = Rectangle.Contains(p);
            return result;
        }

        public override Point GetPosition()
        {
            var result = new Point(Rectangle.Left, Rectangle.Top);
            return result;
        }

        public override void SetPosition(Point p)
        {
            Rectangle = new Rectangle(p.X, p.Y, Rectangle.Width, Rectangle.Height);
        }
    }
}
