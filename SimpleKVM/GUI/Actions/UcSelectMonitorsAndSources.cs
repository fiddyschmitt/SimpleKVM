using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using SimpleKVM.GUI.Actions;
using SimpleKVM.Rules.Actions;
using IAction = SimpleKVM.Rules.Actions.IAction;
using SimpleKVM.Rules;

namespace SimpleKVM.GUI.Actions
{
    public partial class UcSelectMonitorsAndSources : UserControl, IValidate, IActionCreator
    {
        public Rule? RuleToEdit { get; }

        public UcSelectMonitorsAndSources(Rule? ruleToEdit = null)
        {
            InitializeComponent();

            RuleToEdit = ruleToEdit;

            RefreshMonitorList();

            ucMonitorLayout1.MonitorClicked += UcMonitorLayout1_MonitorClicked;
        }

        private void RefreshMonitorList()
        {
            Cursor = Cursors.WaitCursor;

            ucMonitorLayout1.Reload();
            Application.DoEvents();

            //give the panel a scrollbar
            panel1.AutoScroll = false;
            var monitors = Displays.DisplaySystem.GetMonitors();

            var monitorsWithAutogenName = monitors
                                            .Select(mon => new
                                            {
                                                Mon = mon,
                                                Screen = Screen.AllScreens.FirstOrDefault(s => s.GetUniqueId() == mon.MonitorUniqueId)
                                            })
                                            .OrderBy(mon => mon.Screen?.ScreenIndex())
                                            .Select(mon =>
                                            {
                                                var autogenName = $"Monitor {mon.Screen?.ScreenIndex()}";
                                                var model = mon.Mon.Model.Trim();
                                                if (!string.IsNullOrEmpty(model)) autogenName += $" ({model})";

                                                return new
                                                {
                                                    AutogenName = autogenName,
                                                    Monitor = mon.Mon
                                                };
                                            })
                                            .ToList();


            panel1.Controls.Clear();

            monitorsWithAutogenName
                .ForEach(monitor =>
                {
                    int sourceIdToSelect;
                    if (RuleToEdit == null)
                    {
                        sourceIdToSelect = monitor.Monitor.GetCurrentSource();
                    }
                    else
                    {
                        //We are editing the action. So let's set the sourceId to whatever the user chose when they first created the action.

                        var setMonitorAction = RuleToEdit
                                                .Actions
                                                .OfType<SetMonitorSourceAction>()
                                                .FirstOrDefault(a => a.Monitor.MonitorUniqueId.Equals(monitor.Monitor.MonitorUniqueId));


                        sourceIdToSelect = setMonitorAction?.SetMonitorSourceIdTo ?? -1;
                    }

                    var uc = new UcSelectMonitorSource();
                    uc.DisplayMonitor(monitor.AutogenName, monitor.Monitor, sourceIdToSelect);
                    uc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    uc.Width = panel1.Width;


                    uc.Padding = new Padding(16, 4, 16, 4);
                    uc.Height = uc.Height + uc.Padding.Top + uc.Padding.Bottom;

                    uc.Top = panel1.Controls.Count * uc.Height;

                    uc.MouseClick += Uc_MouseClick;

                    panel1.Controls.Add(uc);
                });

            panel1.HorizontalScroll.Enabled = false;
            panel1.HorizontalScroll.Visible = false;
            panel1.HorizontalScroll.Maximum = 0;
            panel1.AutoScroll = true;

            Cursor = Cursors.Default;
        }

        private void Uc_MouseClick(object? sender, MouseEventArgs e)
        {
            if (sender is UcSelectMonitorSource sourceSelector && sourceSelector.Monitor != null)
            {
                ucMonitorLayout1.SelectMonitor(sourceSelector.Monitor.MonitorUniqueId);
            }
        }

        private void UcMonitorLayout1_MonitorClicked(object? sender, MonitorBox? clickedMonitor)
        {
            panel1
                .Controls
                .OfType<UcSelectMonitorSource>()
                .ToList()
                .ForEach(sourceSelector =>
                {
                    if (sourceSelector.Monitor?.MonitorUniqueId == clickedMonitor?.UniqueId)
                    {
                        sourceSelector.BackColor = SystemColors.Highlight;
                    }
                    else
                    {
                        sourceSelector.BackColor = SystemColors.Control;
                    }
                });
        }

        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();
            return result;
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshMonitorList();
        }

        List<IAction> IActionCreator.GetAction()
        {
            var result = panel1
                                .Controls
                                .OfType<UcSelectMonitorSource>()
                                .SelectMany(uc => uc.GetAction())
                                .ToList();

            return result;
        }
    }
}
