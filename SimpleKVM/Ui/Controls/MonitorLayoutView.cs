using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SimpleKVM.Platform;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using IAction = SimpleKVM.Rules.Actions.IAction;

namespace SimpleKVM.Ui.Controls
{
    /// <summary>
    /// Draws every screen as a scaled rectangle (numbered like the OS numbers them) with a
    /// source-selection dropdown inside each. Replaces the old GDI+ monitor drawer and the
    /// combo-positioning logic that sat on top of it.
    /// </summary>
    public class MonitorLayoutView : UserControl, IValidate, IActionCreator
    {
        static readonly Size InitialDrawerSize = new(650, 250);
        const int Pad = 50;

        readonly Canvas canvas;
        readonly Rule? ruleToEdit;
        readonly List<MonitorComboEntry> monitorCombos = [];

        public MonitorLayoutView(Rule? ruleToEdit)
        {
            this.ruleToEdit = ruleToEdit;

            canvas = new Canvas();

            var btnRefresh = new Button { Content = "Refresh" };
            btnRefresh.Click += (s, e) => Reload();

            var layout = new StackPanel { Spacing = 4 };
            layout.Children.Add(canvas);
            layout.Children.Add(btnRefresh);
            Content = layout;

            Reload();
        }

        void Reload()
        {
            canvas.Children.Clear();
            monitorCombos.Clear();

            List<ScreenRect> screens;
            try
            {
                screens = PlatformServices.Current.Displays.GetScreenBounds();
            }
            catch
            {
                screens = [];
            }

            if (screens.Count == 0)
            {
                canvas.Width = 200;
                canvas.Height = 40;
                var message = new TextBlock { Text = "No screens found" };
                Canvas.SetLeft(message, 8);
                Canvas.SetTop(message, 8);
                canvas.Children.Add(message);
                return;
            }

            var left = screens.Min(s => s.Left);
            var top = screens.Min(s => s.Top);
            var right = screens.Max(s => s.Right);
            var bottom = screens.Max(s => s.Bottom);

            var scaleX = InitialDrawerSize.Width / (right - left);
            var scaleY = InitialDrawerSize.Height / (bottom - top);
            var scale = Math.Min(scaleX, scaleY);

            var monitors = Displays.DisplaySystem.GetMonitors();

            //Screens are numbered in the same order the platforms use: left, then top
            var orderedScreens = screens
                                    .OrderBy(s => s.Left)
                                    .ThenBy(s => s.Top)
                                    .Select((s, index) => new { Screen = s, Number = index + 1 })
                                    .ToList();

            double maxRight = 0, maxBottom = 0;

            foreach (var entry in orderedScreens)
            {
                var screen = entry.Screen;

                var rect = new Rect(
                    Pad + (screen.Left - left) * scale,
                    Pad + (screen.Top - top) * scale,
                    (screen.Right - screen.Left) * scale,
                    (screen.Bottom - screen.Top) * scale);

                maxRight = Math.Max(maxRight, rect.Right);
                maxBottom = Math.Max(maxBottom, rect.Bottom);

                var container = new Grid
                {
                    Width = rect.Width,
                    Height = rect.Height
                };

                container.Children.Add(new TextBlock
                {
                    Text = $"{entry.Number}",
                    FontSize = Math.Max(14, 480 * scale),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Foreground = Brushes.Gray
                });

                var uniqueId = Displays.MonitorIdentity.FromBounds(screen.Left, screen.Top, screen.Right, screen.Bottom);
                var monitor = monitors.FirstOrDefault(m => m.MonitorUniqueId == uniqueId);

                if (monitor != null)
                {
                    var combo = BuildSourceCombo(monitor);
                    combo.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                    combo.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
                    combo.Margin = new Thickness(2, 0, 2, 6);
                    container.Children.Add(combo);
                }

                var border = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Child = container
                };

                Canvas.SetLeft(border, rect.X);
                Canvas.SetTop(border, rect.Y);
                canvas.Children.Add(border);
            }

            canvas.Width = maxRight + Pad;
            canvas.Height = maxBottom + Pad;
        }

        ComboBox BuildSourceCombo(Displays.Monitor monitor)
        {
            var currentSource = monitor.GetCurrentSource();

            int sourceIdToSelect;
            if (ruleToEdit == null)
            {
                sourceIdToSelect = currentSource;
            }
            else
            {
                var setMonitorAction = ruleToEdit
                    .Actions
                    .OfType<SetMonitorSourceAction>()
                    .FirstOrDefault(a => a.Monitor.MonitorUniqueId.Equals(monitor.MonitorUniqueId));
                sourceIdToSelect = setMonitorAction?.SetMonitorSourceIdTo ?? -1;
            }

            var items = monitor
                .ValidSources
                .Select(source => new SourceItem(
                    source.SourceId == currentSource ? $"{source.SourceName} (Active)" : source.SourceName,
                    source.SourceId))
                .ToList();

            items.Add(new SourceItem("Leave unchanged", -1));

            var selectedIndex = items.Count - 1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].SourceId == sourceIdToSelect)
                {
                    selectedIndex = i;
                    break;
                }
            }

            var combo = new ComboBox
            {
                ItemsSource = items,
                SelectedIndex = selectedIndex
            };

            monitorCombos.Add(new MonitorComboEntry(monitor, combo, sourceIdToSelect));

            return combo;
        }

        public List<ValidationResult> ValidateData()
        {
            return [];
        }

        public List<IAction> GetAction()
        {
            return monitorCombos
                .Select(entry =>
                {
                    var selectedSourceId = (entry.ComboBox.SelectedItem as SourceItem)?.SourceId ?? entry.OriginalSourceId;
                    return (IAction)new SetMonitorSourceAction(entry.Monitor, selectedSourceId);
                })
                .ToList();
        }

        record SourceItem(string SourceName, int SourceId)
        {
            public override string ToString() => SourceName;
        }

        record MonitorComboEntry(Displays.Monitor Monitor, ComboBox ComboBox, int OriginalSourceId);
    }
}
