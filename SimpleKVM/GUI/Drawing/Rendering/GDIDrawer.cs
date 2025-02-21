using SimpleKVM.GUI.Drawing.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static SimpleKVM.GUI.Drawing.Elements.RectangleWithText;
using static System.Windows.Forms.DataFormats;

namespace SimpleKVM.GUI.Drawing.Rendering
{
    public class GDIDrawer : Panel, IRenderer
    {
        public EventHandler<Element>? BoxClicked;
        public bool DrawArrowheads { get; set; } = true;
        public bool AllowDragging { get; set; } = true;

        public GDIDrawer()
        {
            /*
            this.SetStyle(ControlStyles.AllPaintingInWmPaint
              | ControlStyles.OptimizedDoubleBuffer
              | ControlStyles.ResizeRedraw
              | ControlStyles.DoubleBuffer
              | ControlStyles.UserPaint
              , true);
            */

            //DoubleBuffered = true;

            // the magic calls for invoking doublebuffering (copied from C:\Users\fiddy\Desktop\dev\cs\automatic-graph-layout-master\GraphLayout\Samples\DrawingFromGeometryGraphSample\DrawingFromGeometryGraphForm.cs)
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            MouseDown += GDIDrawer_MouseDown;
            MouseMove += GDIDrawer_MouseMove;
            MouseUp += GDIDrawer_MouseUp;
        }

        private void GDIDrawer_MouseDown(object? sender, MouseEventArgs e)
        {
            if (Elements == null) return;

            if (!AllowDragging) return;

            if (e is not MouseEventArgs mouseArgs) return;
            if (mouseArgs.Button != MouseButtons.Left) return;

            clickedElement = Elements
                                .OfType<Container>()
                                .Where(container => container.ContainsPoint(mouseArgs.Location.Minus(AutoScrollPosition)))
                                .FirstOrDefault();
            performedDrag = false;

            if (clickedElement != null)
            {
                var deltaX = mouseArgs.Location.X - clickedElement.GetPosition().X;
                var deltaY = mouseArgs.Location.Y - clickedElement.GetPosition().Y;
                clickedPointOnElement = new Point(deltaX, deltaY);
            }

            scrollSizeAtStartOfDrag = AutoScrollPosition;
        }

        Element? clickedElement = null;
        Point? clickedPointOnElement = null;
        bool performedDrag = false;
        Point? scrollSizeAtStartOfDrag = null;


        private void GDIDrawer_MouseMove(object? sender, MouseEventArgs e)
        {
            if (clickedElement == null || clickedPointOnElement == null) return;

            performedDrag = true;

            var originalElementPos = clickedElement.GetPosition();

            var deltaScrollX = 0;
            var deltaScrollY = 0;

            if (scrollSizeAtStartOfDrag != null)
            {
                deltaScrollX = scrollSizeAtStartOfDrag.Value.X - AutoScrollPosition.X;
                deltaScrollY = scrollSizeAtStartOfDrag.Value.Y - AutoScrollPosition.Y;
            }

            var newElementPos = new Point(
                                    e.Location.X - clickedPointOnElement.Value.X + deltaScrollX,
                                    e.Location.Y - clickedPointOnElement.Value.Y + deltaScrollY);

            clickedElement.SetPosition(newElementPos);

            var deltaX = originalElementPos.X - newElementPos.X;
            var deltaY = originalElementPos.Y - newElementPos.Y;

            var allAffectedElements = new[] { (clickedElement as Element) }
                                        .Recurse(element => element.Children)
                                        .OfType<Container>()
                                        .ToList();

            allAffectedElements
                .Where(element => element != clickedElement)
                .ToList()
                .ForEach(element =>
                {
                    var currentPosition = element.GetPosition();

                    var newElementPos = new Point(currentPosition.X - deltaX, currentPosition.Y - deltaY);

                    element.SetPosition(newElementPos);
                });

            AdjustControlSize();

            Invalidate();
        }


        private void GDIDrawer_MouseUp(object? sender, MouseEventArgs mouseArgs)
        {
            if (performedDrag)
            {
                AdjustControlSize();
            }
            else
            {
                var deepestClickedElement = Elements
                                ?.Recurse(e => e.Children)
                                .OfType<Container>()
                                .Where(container => container.ContainsPoint(mouseArgs.Location.Minus(AutoScrollPosition)))
                                .LastOrDefault();

                if (deepestClickedElement != null)
                {
                    BoxClicked?.Invoke(this, deepestClickedElement);
                }
            }

            clickedElement = null;
            clickedPointOnElement = null;
        }

        Dictionary<string, Point> previousPositionsByID = [];

        public void ResetLayout()
        {
            Elements = null;
            previousPositionsByID.Clear();
        }

        public void Draw(List<Element> elements)
        {
            if (Elements != null)
            {
                previousPositionsByID = Elements
                                            .Where(element => !Elements.Any(e => e.Children.Contains(element)))
                                            .OfType<Container>()
                                            .GroupBy(
                                                element => element.ToString(),
                                                element => element,
                                                (k, g) => new
                                                {
                                                    Name = k,
                                                    Items = g
                                                            .Select((element, index) => new
                                                            {
                                                                Element = element,
                                                                Index = index,
                                                                ID = $"{element}[{index}"
                                                            })
                                                            .ToList()
                                                })
                                            .SelectMany(g => g.Items)
                                            .ToDictionary(
                                                g => g.ID,
                                                g => g.Element.GetPosition());
            }

            Elements = elements;

            //Restore their positions
            if (previousPositionsByID != null)
            {
                Elements
                    .Where(element => !Elements.Any(e => e.Children.Contains(element)))
                    .OfType<Container>()
                    .GroupBy(
                        element => element.ToString(),
                        element => element,
                        (k, g) => new
                        {
                            Name = k,
                            Items = g
                                    .Select((element, index) => new
                                    {
                                        Element = element,
                                        Index = index,
                                        ID = $"{element}[{index}"
                                    })
                                    .ToList()
                        })
                    .SelectMany(g => g.Items)
                    .ToList()
                    .ForEach(item =>
                    {
                        if (previousPositionsByID.TryGetValue(item.ID, out Point previousPosition))
                        {
                            var originalElementPos = item.Element.GetPosition();
                            var deltaX = originalElementPos.X - previousPosition.X;
                            var deltaY = originalElementPos.Y - previousPosition.Y;

                            var allAffectedElements = new[] { (item.Element as Element) }
                                        .Recurse(element => element.Children)
                                        .OfType<Container>()
                                        .ToList();

                            allAffectedElements
                                .Where(element => element != clickedElement)
                                .ToList()
                                .ForEach(element =>
                                {
                                    var currentPosition = element.GetPosition();

                                    var newElementPos = new Point(currentPosition.X - deltaX, currentPosition.Y - deltaY);

                                    element.SetPosition(newElementPos);
                                });
                        }
                    });
            }

            Invalidate();

            AdjustControlSize();
        }

        private void AdjustControlSize()
        {
            var allElements = Elements
                    ?.Recurse(e => e.Children)
                    .OfType<RectangleElement>()
                    .ToList();

            if (allElements == null || allElements.Count == 0) return;

            var newWidth = allElements.Max(element => element.GetPosition().X + element.Rectangle.Width);
            var newHeight = allElements.Max(element => element.GetPosition().Y + element.Rectangle.Height);

            AutoScrollMinSize = new Size(newWidth, newHeight);
        }

        public List<Element>? Elements { get; protected set; } = null;
        PaintEventArgs? e;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Elements == null) return;
            this.e = e;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            e.Graphics.TranslateTransform(this.AutoScrollPosition.X, this.AutoScrollPosition.Y);

            //e.Graphics.Transform = new Matrix(0.5f, 0, 0, 0.5f, 1, 1);

            Elements
                .OrderBy(element =>
                {
                    //we want the highlighted rectangle to be drawn last
                    return (element is RectangleElement r && r.ShouldHighlight);
                })
                .ToList()
                .ForEach(element =>
                {
                    switch (element)
                    {
                        case RectangleWithText r:
                            DrawRectangle(r);
                            break;
                        case RectangleElement r:
                            DrawRectangle(r);
                            break;
                        case Line l:
                            DrawLine(l);
                            break;
                        case null:
                            throw new Exception($"No draw method for element: {nameof(element)}");
                    }
                });
        }

        public void DrawRectangle(RectangleElement rectangle)
        {
            if (e == null) return;

            var pen = rectangle.Pen;
            if (rectangle.ShouldHighlight)
            {
                pen = new Pen(Color.Blue, 3);
            }
            e.Graphics.DrawRectangle(pen, rectangle.Rectangle);
        }

        public void DrawRectangle(RectangleWithText rectangle)
        {
            if (e == null) return;

            var pen = rectangle.Pen;

            if (rectangle.ShouldHighlight)
            {
                //var fillColour = new SolidBrush(Color.FromArgb(41, 146, 204)); //light blue
                var fillColour = new SolidBrush(SystemColors.Highlight);
                e.Graphics.FillRectangle(fillColour, rectangle.Rectangle);
            }

            e.Graphics.DrawRectangle(pen, rectangle.Rectangle);


            var labelSize = rectangle.Font.MeasureText(rectangle.Text);
            var format = new StringFormat() { Alignment = StringAlignment.Center };

            var r = rectangle.Rectangle;

            var textPosition = rectangle.TextPosition switch
            {
                EnumPosition.TopCenter => new PointF(r.Left + r.Width / 2f, r.Top + labelSize.Height / 3),
                EnumPosition.Center => new PointF(r.Left + r.Width / 2f, r.Top + r.Height / 2f - labelSize.Height / 3),
                _ => throw new Exception($"Unhandled text position: {rectangle.TextPosition}")
            };

            e.Graphics.DrawString(rectangle.Text, rectangle.Font, rectangle.TextBrush, textPosition.X, textPosition.Y, format);
        }

        public void DrawLine(Line line)
        {
            var pen = new Pen(Color.Black, 1);

            //draws a triangle, but too small
            //pen.EndCap = LineCap.Triangle;

            if (DrawArrowheads && line.DrawDestinationArrow) pen.CustomEndCap = new AdjustableArrowCap(5, 5);
            if (DrawArrowheads && line.DrawSourceArrow) pen.CustomStartCap = new AdjustableArrowCap(5, 5);


            var from = line.From.GetPosition();
            if (line.From is RectangleElement fromRect)
            {
                from = fromRect
                        .Rectangle
                        .AnchorPoints(true)
                        .OrderBy(anchorPoint => anchorPoint.DistanceTo(line.To.GetPosition()))
                        .First();
            }

            var to = line.To.GetPosition();

            if (line.To is RectangleElement toRect)
            {
                to = toRect
                        .Rectangle
                        .AnchorPoints(true)
                        .OrderBy(anchorPoint => anchorPoint.DistanceTo(line.From.GetPosition()))
                        .First();

                //to = RectangleUtility.GetNearestPointInPerimeter(from, toRect.Rectangle);
            }

            e?.Graphics.DrawLine(pen, from, to);
        }
    }
}
