using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using SimpleKVM.GUI.Actions;
using SimpleKVM.Rules.Actions;
using IAction = SimpleKVM.Rules.Actions.IAction;
using SimpleKVM.Rules;
using SimpleKVM.Displays;

namespace SimpleKVM.GUI.Actions
{
    public partial class UcSelectMonitorsAndSources : UserControl, IValidate, IActionCreator
    {
        public Rule? RuleToEdit { get; }

        readonly List<MonitorComboEntry> monitorCombos = [];

        public UcSelectMonitorsAndSources(Rule? ruleToEdit = null)
        {
            InitializeComponent();

            RuleToEdit = ruleToEdit;

            RefreshMonitorList();
        }

        private void RefreshMonitorList()
        {
            Cursor = Cursors.WaitCursor;

            foreach (var entry in monitorCombos)
                ucMonitorLayout1.DrawingPanel.Controls.Remove(entry.ComboBox);
            monitorCombos.Clear();

            ucMonitorLayout1.Reload();
            Size = ucMonitorLayout1.Size;
            Application.DoEvents();

            var monitors = Displays.DisplaySystem.GetMonitors();

            var monitorsWithScreen = monitors
                .Select(mon => new
                {
                    Mon = mon,
                    Screen = Screen.AllScreens.FirstOrDefault(s => s.GetUniqueId() == mon.MonitorUniqueId)
                })
                .Where(m => m.Screen != null)
                .ToList();

            foreach (var monitorBox in ucMonitorLayout1.Monitors)
            {
                var match = monitorsWithScreen
                    .FirstOrDefault(m => m.Screen!.GetUniqueId() == monitorBox.UniqueId);
                if (match == null) continue;

                var monitor = match.Mon;

                int sourceIdToSelect;
                if (RuleToEdit == null)
                {
                    sourceIdToSelect = monitor.GetCurrentSource();
                }
                else
                {
                    var setMonitorAction = RuleToEdit
                        .Actions
                        .OfType<SetMonitorSourceAction>()
                        .FirstOrDefault(a => a.Monitor.MonitorUniqueId.Equals(monitor.MonitorUniqueId));
                    sourceIdToSelect = setMonitorAction?.SetMonitorSourceIdTo ?? -1;
                }

                var currentSource = monitor.GetCurrentSource();

                var items = monitor
                    .ValidSources
                    .Select(source =>
                    {
                        var sourceName = source.SourceName;
                        if (source.SourceId == currentSource)
                            sourceName += " (Active)";
                        return new { SourceName = sourceName, source.SourceId };
                    })
                    .ToList();

                items.Add(new { SourceName = "Leave unchanged", SourceId = -1 });

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
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FormattingEnabled = true,
                    Visible = false,
                };

                ucMonitorLayout1.DrawingPanel.Controls.Add(combo);
                combo.BringToFront();

                combo.DisplayMember = "SourceName";
                combo.ValueMember = "SourceId";
                combo.DataSource = items;
                combo.SelectedIndex = selectedIndex;

                int maxTextWidth = 0;
                using (var g = combo.CreateGraphics())
                {
                    foreach (var item in items)
                    {
                        var size = g.MeasureString(item.SourceName, combo.Font);
                        maxTextWidth = Math.Max(maxTextWidth, (int)Math.Ceiling(size.Width));
                    }
                }
                int dropdownButtonWidth = SystemInformation.VerticalScrollBarWidth;
                combo.Width = maxTextWidth + dropdownButtonWidth + 10;

                var rect = monitorBox.Rectangle;
                int padding = 6;
                combo.Left = rect.Left + (rect.Width - combo.Width) / 2;
                combo.Top = rect.Bottom - padding - combo.Height;

                combo.Visible = true;

                monitorCombos.Add(new MonitorComboEntry(monitor, combo, sourceIdToSelect));
            }

            Cursor = Cursors.Default;
        }

        public List<ValidationResult> ValidateData()
        {
            return [];
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshMonitorList();
        }

        List<IAction> IActionCreator.GetAction()
        {
            return monitorCombos
                .Select(entry =>
                {
                    var selectedSourceId = (entry.ComboBox.SelectedValue as int?) ?? entry.OriginalSourceId;
                    return (IAction)new SetMonitorSourceAction(entry.Monitor, selectedSourceId);
                })
                .ToList();
        }

        record MonitorComboEntry(Monitor Monitor, ComboBox ComboBox, int OriginalSourceId);
    }
}
