using SimpleKVM.GUI.Drawing.Elements;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static SimpleKVM.GUI.Drawing.Elements.RectangleWithText;

namespace SimpleKVM.GUI
{
    public partial class UcMonitorLayout : UserControl
    {
        public event EventHandler<MonitorBox>? MonitorClicked;
        List<MonitorBox> monitors = [];

        public UcMonitorLayout()
        {
            InitializeComponent();
        }

        private void UcMonitorLayout_Load(object sender, EventArgs e)
        {
            try
            {
                var left = Screen.AllScreens.Min(screen => screen.Bounds.Left);
                var right = Screen.AllScreens.Max(screen => screen.Bounds.Right);
                var top = Screen.AllScreens.Min(screen => screen.Bounds.Top);
                var bottom = Screen.AllScreens.Max(screen => screen.Bounds.Bottom);

                var virtualScreen = new Rectangle(left, top, right - left, bottom - top);
                var offset = new Point(-virtualScreen.X, -virtualScreen.Y);
                virtualScreen.Offset(offset);

                //gdiDrawer1.BackColor = Color.FromArgb(23, 23, 23);    //dark grey

                var scaleX = (monitorDrawer.Width - 100) / (double)virtualScreen.Width;
                var scaleY = (monitorDrawer.Height - 100) / (double)virtualScreen.Height;
                var scale = Math.Min(scaleX, scaleY);

                var drawRects = Screen.AllScreens
                                        .Select(screen =>
                                        {
                                            var screenRect = screen.Bounds;
                                            screenRect.Offset(offset);

                                            var drawRect = new Rectangle(
                                                (int)(screenRect.X * scale),
                                                (int)(screenRect.Y * scale),
                                                (int)(screenRect.Width * scale),
                                                (int)(screenRect.Height * scale));

                                            return new
                                            {
                                                UniqueId = screen.GetUniqueId(),
                                                ScreenIndex = screen.ScreenIndex(),
                                                DrawRectable = drawRect
                                            };
                                        })
                                        .OrderBy(screen => screen.ScreenIndex)
                                        .ToList();

                var rightMost = drawRects.Max(rect => rect.DrawRectable.Right);
                var bottomMost = drawRects.Max(rect => rect.DrawRectable.Bottom);
                var shiftRight = (int)((monitorDrawer.Width - rightMost) / 2d);
                var shiftDown = (int)((monitorDrawer.Height - bottomMost) / 2d);
                var offsetToCenter = new Point(shiftRight, shiftDown);


                var borderColour = Color.FromArgb(209, 209, 209);   //light gray
                borderColour = Color.Black;
                var borderPen = new Pen(borderColour, 1);

                var fontFamily = new FontFamily("Arial");
                var font = new Font(fontFamily, (int)(360 * scale), FontStyle.Regular, GraphicsUnit.Point);
                var textBrush = new SolidBrush(borderColour);

                monitors = drawRects
                                .Select(rect =>
                                {
                                    var drawRect = rect.DrawRectable;
                                    drawRect.Offset(offsetToCenter);

                                    var r = new MonitorBox(rect.UniqueId, drawRect, borderPen, $"{rect.ScreenIndex}", EnumPosition.Center, font, textBrush);
                                    return r;
                                })
                                .OfType<MonitorBox>()
                                .ToList();

                var elems = monitors
                                .OfType<Element>()
                                .ToList();

                monitorDrawer.AllowDragging = false;
                monitorDrawer.Draw(elems);

                monitorDrawer.BoxClicked += BoxClicked;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error displaying monitor layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SelectMonitor(string monitorUniqueId)
        {
            var monitorBox = monitors.FirstOrDefault(mon => mon.UniqueId == monitorUniqueId);

            if (monitorBox != null)
            {
                BoxClicked(this, monitorBox);
            }
        }

        public void BoxClicked(object? sender, Element e)
        {
            if (e is MonitorBox clickedMonitor)
            {
                monitors
                    .ForEach(elem =>
                    {
                        if (elem == e)
                        {
                            //elem.ShouldHighlight = !elem.ShouldHighlight;
                            elem.ShouldHighlight = true;
                        }
                        else
                        {
                            elem.ShouldHighlight = false;
                        }
                    });

                monitorDrawer.Invalidate();

                MonitorClicked?.Invoke(this, clickedMonitor);
            }
        }
    }

    public class MonitorBox(string uniqueName, Rectangle rectangle, Pen borderPen, string text, EnumPosition textPosition, Font font, Brush textBrush) : RectangleWithText(rectangle, borderPen, text, textPosition, font, textBrush)
    {
        public string UniqueId { get; } = uniqueName;
    }
}
