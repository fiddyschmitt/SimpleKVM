using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.GUI.Drawing.Elements
{
    public abstract class Element
    {
        public abstract Point GetPosition();
        public abstract void SetPosition(Point p);

        public List<Element> Children = [];

        public bool ShouldHighlight = false;
    }
}
