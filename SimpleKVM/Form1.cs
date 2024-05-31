using SimpleKVM.Displays.win;
using SimpleKVM.GUI;
using SimpleKVM.GUI.Rules;
using SimpleKVM.GUI.Triggers;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.USB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MethodInvoker = System.Windows.Forms.MethodInvoker;

namespace SimpleKVM
{
    public partial class Form1 : Form
    {
        readonly USBSystem? usbSystem;
        ListViewEx<Rule>? ruleListview;
        const string ProgramName = "Simple KVM";
        const string Version = "2.0.0";
        public static List<Rule> Rules { get; protected set; } = [];

        public Form1()
        {
            //DisplaySystem_2_experiments.GetMonitors();

            //var hk = new ChooseHotkey();
            //hk.Show();

            InitializeComponent();
            Text = $"{ProgramName} {Version}";

            InitialiseSystemTray();

            InitializeRuleListView();

            LoadRules();

            Rules.ForEach(rule =>
            {
                ruleListview?.Add(rule);

                rule.Triggered += Rule_Triggered;

                if (rule.Status == EnumRuleStatus.Running || rule.Status == EnumRuleStatus.Error || rule.Status == EnumRuleStatus.Stopped)
                {
                    rule.Status = EnumRuleStatus.Stopped;
                    rule.StartMonitoring();
                }
            });

            usbSystem = USBSystem.INSTANCE;
        }

        private void InitialiseSystemTray()
        {
            notifyIcon1.Icon = Icon;
            notifyIcon1.Text = Text;
            notifyIcon1.Visible = true;

            notifyIcon1.ContextMenuStrip = new ContextMenuStrip();
            var exitButton = notifyIcon1.ContextMenuStrip.Items.Add("Exit");
            exitButton.Click += (sender, obj) =>
            {
                Close();
            };
        }

        private void InitializeRuleListView()
        {
            var columnMapping = new List<(string ColumnName, Func<Rule, object> ValueLookup, Func<Rule, string> DisplayStringLookup)>()
            {
                ("Name", rule => rule.Name, rule => rule.Name),
                ("Trigger", rule => rule.GetTriggerAsFriendlyString(), rule => rule.GetTriggerAsFriendlyString()),
                ("Status", rule => rule.Status, rule => rule.Status.ToString()),
                ("Last run", rule => rule.LastRun ?? DateTime.MinValue, rule => rule.GetLastRunAsFriendlyString()),
                ("Run count", rule => rule.RunCount, rule => $"{rule.RunCount:N0}"),
            };

            ruleListview = new ListViewEx<Rule>(columnMapping)
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                View = View.Details
            };

            var editAction = new EventHandler((sender, e) =>
            {
                var selectedObject = ruleListview
                                        .GetSelectedItems()
                                        .FirstOrDefault();

                if (selectedObject != null)
                {
                    EditRule(null, null, selectedObject);
                }
            });

            ruleListview.DoubleClick += editAction;

            var selectedRules = ruleListview
                                    .SelectedItems
                                    .Cast<ListViewItem>()
                                    .Select(lvi => lvi.Tag as Rule);

            ruleListview.ContextMenuStrip = new ContextMenuStrip();

            ruleListview.ContextMenuStrip.Items.Add("Enable", null, (sender, obj) =>
            {
                var toDelete = selectedRules
                                    .ToList();

                toDelete
                    .ToList()
                    .ForEach(rule =>
                    {
                        rule?.Enable();
                    });

                SaveRules();
            });

            ruleListview.ContextMenuStrip.Items.Add("Disable", null, (sender, obj) =>
            {
                var toDelete = selectedRules
                                    .ToList();

                toDelete
                    .ToList()
                    .ForEach(rule =>
                    {
                        rule?.Disable();
                    });

                SaveRules();
            });

            ruleListview.ContextMenuStrip.Items.Add("-");

            ruleListview.ContextMenuStrip.Items.Add("Edit", null, (sender, obj) =>
            {
                editAction.Invoke(sender, obj);
            });

            ruleListview.ContextMenuStrip.Items.Add("Delete", null, (sender, obj) =>
            {
                var toDelete = selectedRules
                                    .ToList();

                toDelete
                    .ToList()
                    .ForEach(rule =>
                    {
                        if (rule != null)
                        {
                            rule.StopMonitoring();
                            Rules.Remove(rule);
                            ruleListview?.Remove(rule);
                        }
                    });

                SaveRules();
            });

            ruleListview.ContextMenuStrip.Items.Add("-");

            ruleListview.ContextMenuStrip.Items.Add("Run now", null, (sender, obj) =>
            {
                var selectedRule = ruleListview
                                    .SelectedItems
                                    .Cast<ListViewItem>()
                                    .Select(lvi => lvi.Tag as Rule)
                                    .FirstOrDefault();

                selectedRule?.Run();
            });

            panel1.Controls.Add(ruleListview);

            statsTimer.Start();
        }

        public void EditRule(EnumTriggerType? triggerType, EnumActionType? actionType, Rule? rule)
        {
            if (usbSystem == null) return;

            //pause the rules which are current running
            var paused = Rules
                            .Where(rule => rule.Status == EnumRuleStatus.Running || rule.Status == EnumRuleStatus.Error)
                            .Select(rule =>
                            {
                                rule.StopMonitoring();
                                return rule;
                            })
                            .ToList();

            var creatingNewRule = (rule == null);

            var title = creatingNewRule ? "Create new rule" : "Edit rule";

            ModifyRule editRuleForm;
            if (rule == null)
            {
                if (triggerType == null) return;
                if (actionType == null) return;

                try
                {
                    Cursor = Cursors.WaitCursor;
                    editRuleForm = new ModifyRule(usbSystem, title, triggerType.Value, actionType.Value);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
            else
            {
                try
                {
                    Cursor = Cursors.WaitCursor;
                    editRuleForm = new ModifyRule(usbSystem, title, rule);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }

            editRuleForm.StartPosition = FormStartPosition.CenterParent;
            var diagResult = editRuleForm.ShowDialog(this);

            if (diagResult == DialogResult.OK)
            {
                if (creatingNewRule)
                {
                    var newRule = editRuleForm.GetRule();

                    if (newRule != null)
                    {
                        ruleListview?.Add(newRule);

                        newRule.Triggered += Rule_Triggered;
                        Rules.Add(newRule);

                        newRule.StartMonitoring();
                    }
                }
                else
                {
                    var editedRule = editRuleForm.GetRule(); //forces the new data to be collected into the Rule object
                    editedRule?.StartMonitoring();

                    ruleListview?.RefreshContent();
                }

                SaveRules();
            }

            //resume the rules which were running earlier
            paused
                .ForEach(rule =>
                {
                    rule.StartMonitoring();
                });
        }

        private void BtnNewRule_Click(object sender, EventArgs e)
        {
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Hotkey rule", null, (sender, obj) => { EditRule(EnumTriggerType.Hotkey, EnumActionType.SelectMonitorSource, null); });
            contextMenu.Items.Add("USB rule", null, (sender, obj) => { EditRule(EnumTriggerType.Usb, EnumActionType.SelectMonitorSource, null); });
            contextMenu.Items.Add("No Longer Idle rule", null, (sender, obj) => { EditRule(EnumTriggerType.NoLongerIdle, EnumActionType.SelectMonitorSource, null); });
            contextMenu.Show(btnNewRule, new Point(btnNewRule.Width, (int)(btnNewRule.Height / 2d)));
        }

        private static void LoadRules()
        {
            var rulesJson = Extensions.ReadTextFile("", "rules.json");
            var loadedRules = rulesJson?.DeserializJson<List<Rule>>() ?? [];
            Rules.AddRange(loadedRules);
        }

        private static void SaveRules()
        {
            var rulesJson = Rules.SerializeToJson();
            if (rulesJson != null)
            {
                Extensions.WriteTextFile("", "rules.json", rulesJson);
            }
        }

        private void Rule_Triggered(object? sender, EventArgs e)
        {
            Invoke(new MethodInvoker(() =>
            {
                //DisplayRules();

                //Save the rules, because the stats have changed
                SaveRules();
            }));
        }

        void UpdateStats()
        {
            ruleListview?.RefreshContent();
        }

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            UpdateStats();
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (ruleListview == null) return;

            //preserve the order
            Rules = ruleListview.GetItems();

            SaveRules();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
            }
        }

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            //notifyIcon1.Visible = false;
        }

        private void NotifyIcon1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                mi?.Invoke(notifyIcon1, null);
            }
        }
    }
}
