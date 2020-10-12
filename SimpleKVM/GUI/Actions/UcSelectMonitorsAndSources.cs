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
        public UcSelectMonitorsAndSources(IValueChangedListener? valueChangedListener = null, Rule? ruleToEdit = null)
        {
            InitializeComponent();

            //give the panel a scrollbar
            panel1.AutoScroll = false;
            var monitors = Displays.DisplaySystem.GetMonitors();

            var monitorsWithAutogenName = monitors
                                            .Select((monitor, index) =>
                                            {
                                                //var autogenName = monitor.MonitorName.Trim();
                                                //if (string.IsNullOrEmpty(autogenName)) autogenName = monitor.MonitorDeviceName;
                                                //autogenName = $"Monitor {index + 1}: {autogenName}";
                                                var autogenName = $"Monitor {monitor.MonitorUniqueId}";
                                                if (!string.IsNullOrEmpty(monitor.Model)) autogenName += $" ({monitor.Model})";

                                                return new
                                                {
                                                    AutogenName = autogenName,
                                                    Monitor = monitor
                                                };
                                            })
                                            .ToList();

            var monitorControls = monitorsWithAutogenName
                                    .Select((monitor, index) =>
                                    {
                                        int sourceIdToSelect;
                                        if (ruleToEdit == null)
                                        {
                                            sourceIdToSelect = monitor.Monitor.CurrentSource;
                                        }
                                        else
                                        {
                                            //We are editing the action. So let's set the sourceId to whatever the user chose when they first created the action.

                                            var setMonitorAction = ruleToEdit
                                                                    .Actions
                                                                    .OfType<SetMonitorSourceAction>()
                                                                    .FirstOrDefault(a => a.Monitor.MonitorUniqueId.Equals(monitor.Monitor.MonitorUniqueId));

                                            if (setMonitorAction == null)
                                            {
                                                sourceIdToSelect = -1;
                                            } else
                                            {
                                                sourceIdToSelect = setMonitorAction.SetMonitorSourceIdTo;
                                            }
                                        }

                                        var uc = new UcSelectMonitorSource();
                                        uc.DisplayMonitor(monitor.AutogenName, monitor.Monitor, sourceIdToSelect);
                                        uc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                                        uc.Width = (int)(panel1.Width * 0.95);

                                        uc.Left = 8;
                                        uc.Top = panel1.Controls.Count * uc.Height;

                                        panel1.Controls.Add(uc);

                                        return uc;
                                    })
                                    .ToArray();

            panel1.HorizontalScroll.Enabled = false;
            panel1.HorizontalScroll.Visible = false;
            panel1.HorizontalScroll.Maximum = 0;
            panel1.AutoScroll = true;
        }


        public List<ValidationResult> ValidateData()
        {
            var result = new List<ValidationResult>();
            return result;
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
