using SimpleKVM.GUI.Drawing.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.GUI.Drawing.Rendering
{
    public interface IRenderer
    {
        public void Draw(List<Element> elements);
        public void DrawRectangle(RectangleElement rectangle);
        public void DrawRectangle(RectangleWithText rectangle);
        public void DrawLine(Line line);
    }
}
