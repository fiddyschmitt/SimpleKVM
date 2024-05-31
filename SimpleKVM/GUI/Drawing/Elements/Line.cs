using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.GUI.Drawing.Elements
{
    public class Line(Element from, Element to, bool drawSourceArrow, bool drawDestinationArrow) : Element
    {
        public Element From { get; } = from;
        public Element To { get; } = to;
        public bool DrawSourceArrow { get; } = drawSourceArrow;
        public bool DrawDestinationArrow { get; } = drawDestinationArrow;

        public override Point GetPosition()
        {
            throw new NotImplementedException();
        }

        public override void SetPosition(Point p)
        {
            throw new NotImplementedException();
        }
    }
}
