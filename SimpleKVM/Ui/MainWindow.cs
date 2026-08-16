using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SimpleKVM.Configuration;
using SimpleKVM.Rules;
using SimpleKVM.USB;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SimpleKVM.Ui
{
    public class MainWindow : Window
    {
        const string ProgramName = "Simple KVM";

        readonly USBSystem? usbSystem;
        readonly SourceFollowWatcher? sourceFollowWatcher;
        readonly Task initMonitorList;
        readonly DataGrid rulesGrid;
        readonly ObservableCollection<RuleRow> ruleRows = [];
        readonly Avalonia.Collections.DataGridCollectionView rulesView;
        readonly DispatcherTimer statsTimer;

        public MainWindow()
        {
            Title = $"{ProgramName} {GetVersion()}";
            Icon = App.LoadIcon();
            Width = 720;
            Height = 360;

            //An explicit collection view lets the closing handler read back the sorted order
            rulesView = new Avalonia.Collections.DataGridCollectionView(ruleRows);

            ConfigManager.Load();
            AppSettingsManager.Load();

            initMonitorList = Task.Factory.StartNew(() =>
            {
                Displays.DisplaySystem.GetMonitors();   //for the monitors to be cached
            }, TaskCreationOptions.LongRunning);

            rulesGrid = BuildRulesGrid();

            var btnNewRule = new Button { Content = "New rule" };
            btnNewRule.Flyout = BuildNewRuleFlyout();

            var btnSettings = new Button { Content = "Settings" };
            btnSettings.Click += async (s, e) => await ShowSettings();

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(8)
            };
            buttonRow.Children.Add(btnNewRule);
            buttonRow.Children.Add(btnSettings);

            var layout = new DockPanel();
            DockPanel.SetDock(buttonRow, Dock.Bottom);
            layout.Children.Add(buttonRow);
            rulesGrid.Margin = new Thickness(8, 8, 8, 0);
            layout.Children.Add(rulesGrid);
            Content = layout;

            RuleStore.Load();

            RuleStore.Rules.ForEach(rule =>
            {
                ruleRows.Add(new RuleRow(rule));

                rule.Triggered += Rule_Triggered;

                if (rule.Status == EnumRuleStatus.Running || rule.Status == EnumRuleStatus.Error || rule.Status == EnumRuleStatus.Stopped)
                {
                    rule.Status = EnumRuleStatus.Stopped;
                    rule.StartMonitoring();
                }
            });

            usbSystem = USBSystem.INSTANCE;

            sourceFollowWatcher = new SourceFollowWatcher(() => RuleStore.Rules);
            ApplyFollowSourceSetting();

            statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            statsTimer.Tick += (s, e) => RefreshRows();
            statsTimer.Start();

            //Minimizing hides the window; the tray icon brings it back (matching the old behavior)
            PropertyChanged += (s, e) =>
            {
                if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized)
                {
                    Hide();
                }
            };

            Closing += MainWindow_Closing;

            //Grow the window so every column is in view on startup
            Opened += (s, e) => Dispatcher.UIThread.Post(FitWidthToColumns, DispatcherPriority.Background);
        }

        static string GetVersion()
        {
            var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                        ?? "";
            return info.Split('+')[0];
        }

        DataGrid BuildRulesGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                CanUserSortColumns = true,
                SelectionMode = DataGridSelectionMode.Extended,
                ItemsSource = rulesView
            };

            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(RuleRow.Name)), Width = DataGridLength.Auto });
            grid.Columns.Add(new DataGridTextColumn { Header = "Trigger", Binding = new Binding(nameof(RuleRow.TriggerText)), Width = DataGridLength.Auto });
            grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding(nameof(RuleRow.StatusText)), Width = DataGridLength.Auto });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Last run",
                Binding = new Binding(nameof(RuleRow.LastRunText)),
                Width = DataGridLength.Auto,
                CustomSortComparer = RuleRow.CompareBy(row => row.LastRun)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Run count",
                Binding = new Binding(nameof(RuleRow.RunCountText)),
                Width = DataGridLength.Auto,
                CustomSortComparer = RuleRow.CompareBy(row => row.RunCount)
            });

            grid.DoubleTapped += async (s, e) =>
            {
                var selected = SelectedRules().FirstOrDefault();
                if (selected != null)
                {
                    await EditRule(null, null, selected);
                }
            };

            grid.ContextMenu = BuildContextMenu();

            return grid;
        }

        ContextMenu BuildContextMenu()
        {
            var enableItem = new MenuItem { Header = "Enable" };
            enableItem.Click += (s, e) =>
            {
                SelectedRules().ForEach(rule => rule.Enable());
                RuleStore.Save();
                RefreshRows();
            };

            var disableItem = new MenuItem { Header = "Disable" };
            disableItem.Click += (s, e) =>
            {
                SelectedRules().ForEach(rule => rule.Disable());
                RuleStore.Save();
                RefreshRows();
            };

            var editItem = new MenuItem { Header = "Edit" };
            editItem.Click += async (s, e) =>
            {
                var selected = SelectedRules().FirstOrDefault();
                if (selected != null)
                {
                    await EditRule(null, null, selected);
                }
            };

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += (s, e) =>
            {
                SelectedRules().ForEach(rule =>
                {
                    rule.StopMonitoring();
                    RuleStore.Rules.Remove(rule);

                    var row = ruleRows.FirstOrDefault(r => r.Rule == rule);
                    if (row != null) ruleRows.Remove(row);
                });

                RuleStore.Save();
            };

            var runNowItem = new MenuItem { Header = "Run now" };
            runNowItem.Click += (s, e) =>
            {
                var selected = SelectedRules().FirstOrDefault();
                selected?.Run();
            };

            var setDelayItem = new MenuItem { Header = "Set delay..." };
            setDelayItem.Click += async (s, e) =>
            {
                var selected = SelectedRules().FirstOrDefault();
                if (selected == null) return;

                var dialog = new SetRuleDelayWindow { DelaySeconds = selected.DelaySeconds };
                var ok = await dialog.ShowDialog<bool>(this);
                if (ok)
                {
                    selected.DelaySeconds = dialog.DelaySeconds;
                    RuleStore.Save();
                }
            };

            var menu = new ContextMenu();
            menu.Items.Add(enableItem);
            menu.Items.Add(disableItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(editItem);
            menu.Items.Add(deleteItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(runNowItem);
            menu.Items.Add(setDelayItem);

            menu.Opening += (s, e) =>
            {
                var selectedRules = SelectedRules();

                enableItem.IsEnabled = selectedRules.Any(rule => rule.Status != EnumRuleStatus.Running);
                disableItem.IsEnabled = selectedRules.Any(rule => rule.Status != EnumRuleStatus.Disabled);
                editItem.IsEnabled = selectedRules.Count == 1;
                deleteItem.IsEnabled = selectedRules.Count > 0;
                runNowItem.IsEnabled = selectedRules.Count == 1;

                setDelayItem.IsEnabled = selectedRules.Count == 1;
                var delayRule = selectedRules.FirstOrDefault();
                setDelayItem.Header = delayRule?.DelaySeconds > 0 ? $"Set delay ({delayRule.DelaySeconds} s)..." : "Set delay...";
            };

            return menu;
        }

        MenuFlyout BuildNewRuleFlyout()
        {
            var hotkeyItem = new MenuItem { Header = "Hotkey rule" };
            hotkeyItem.Click += async (s, e) => await EditRule(EnumTriggerType.Hotkey, EnumActionType.SelectMonitorSource, null);

            var usbItem = new MenuItem { Header = "USB rule" };
            usbItem.Click += async (s, e) => await EditRule(EnumTriggerType.Usb, EnumActionType.SelectMonitorSource, null);

            var idleItem = new MenuItem { Header = "No Longer Idle rule" };
            idleItem.Click += async (s, e) => await EditRule(EnumTriggerType.NoLongerIdle, EnumActionType.SelectMonitorSource, null);

            var flyout = new MenuFlyout();
            flyout.Items.Add(hotkeyItem);
            flyout.Items.Add(usbItem);
            flyout.Items.Add(idleItem);
            return flyout;
        }

        System.Collections.Generic.List<Rule> SelectedRules()
        {
            return rulesGrid
                    .SelectedItems
                    .Cast<RuleRow>()
                    .Select(row => row.Rule)
                    .ToList();
        }

        public async Task EditRule(EnumTriggerType? triggerType, EnumActionType? actionType, Rule? rule)
        {
            if (usbSystem == null) return;

            //pause the rules which are currently running
            var paused = RuleStore.Rules
                            .Where(r => r.Status == EnumRuleStatus.Running || r.Status == EnumRuleStatus.Error)
                            .Select(r =>
                            {
                                r.StopMonitoring();
                                return r;
                            })
                            .ToList();

            await Task.Run(initMonitorList.Wait);

            var creatingNewRule = rule == null;
            var title = creatingNewRule ? "Create new rule" : "Edit rule";

            ModifyRuleWindow editRuleWindow;
            if (rule == null)
            {
                if (triggerType == null || actionType == null) return;
                editRuleWindow = new ModifyRuleWindow(usbSystem, title, triggerType.Value, actionType.Value, null);
            }
            else
            {
                editRuleWindow = new ModifyRuleWindow(usbSystem, title, rule);
            }

            var saved = await editRuleWindow.ShowDialog<bool>(this);
            var save = false;

            if (saved)
            {
                if (creatingNewRule)
                {
                    var newRule = editRuleWindow.GetRule();

                    if (newRule != null)
                    {
                        ruleRows.Add(new RuleRow(newRule));

                        newRule.Triggered += Rule_Triggered;
                        RuleStore.Rules.Add(newRule);

                        newRule.StartMonitoring();
                    }
                }
                else
                {
                    var editedRule = editRuleWindow.GetRule();  //forces the new data to be collected into the Rule object

                    if (editedRule?.Status != EnumRuleStatus.Disabled)
                    {
                        editedRule?.StartMonitoring();
                    }
                }

                save = true;
            }

            //resume the rules which were running earlier
            paused.ForEach(r => r.StartMonitoring());

            if (save)
            {
                RuleStore.Save();
                RefreshRows();
            }
        }

        async Task ShowSettings()
        {
            var settingsWindow = new SettingsWindow();
            await settingsWindow.ShowDialog(this);

            //The follow-source setting may have been toggled
            ApplyFollowSourceSetting();
        }

        void ApplyFollowSourceSetting()
        {
            if (AppSettingsManager.Current.FollowSourceChanges)
            {
                sourceFollowWatcher?.Start();
            }
            else
            {
                sourceFollowWatcher?.Stop();
            }
        }

        void Rule_Triggered(object? sender, EventArgs e)
        {
            //Triggers run on background threads; Post never blocks them on the UI thread
            Dispatcher.UIThread.Post(() =>
            {
                //Save the rules, because the stats have changed
                RuleStore.Save();
                RefreshRows();
            });
        }

        void RefreshRows()
        {
            foreach (var row in ruleRows)
            {
                row.Refresh();
            }
        }

        void FitWidthToColumns()
        {
            //Runs after the first layout pass, when the auto-sized columns know their widths.
            //+60 covers the grid margins, a vertical scrollbar and some breathing room.
            var neededWidth = rulesGrid.Columns.Sum(column => column.ActualWidth) + 60;
            if (neededWidth <= Width) return;

            double maxWidth = 1400;
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen != null && RenderScaling > 0)
            {
                maxWidth = screen.WorkingArea.Width / RenderScaling * 0.9;
            }

            Width = Math.Min(neededWidth, maxWidth);
        }

        void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            sourceFollowWatcher?.Stop();

            //Preserve the order the user sees (including any column sort), like the old
            //ListView did, so the next start lists the rules the same way
            var orderedRules = rulesView.Cast<RuleRow>().Select(row => row.Rule).ToList();
            if (orderedRules.Count == RuleStore.Rules.Count)
            {
                RuleStore.Rules = orderedRules;
            }

            RuleStore.Save();
        }
    }

    /// <summary>
    /// Bindable wrapper of a Rule for the DataGrid; Refresh() raises change notifications
    /// only for values that actually changed (the old ListView did the same diffing).
    /// </summary>
    public class RuleRow(Rule rule) : INotifyPropertyChanged
    {
        public Rule Rule { get; } = rule;

        public event PropertyChangedEventHandler? PropertyChanged;

        string name = rule.Name;
        string triggerText = rule.GetTriggerAsFriendlyString();
        string statusText = rule.Status.ToString();
        string lastRunText = rule.GetLastRunAsFriendlyString();
        int runCount = rule.RunCount;

        public string Name => name;
        public string TriggerText => triggerText;
        public string StatusText => statusText;
        public string LastRunText => lastRunText;
        public string RunCountText => $"{runCount:N0}";

        public DateTime LastRun => Rule.LastRun ?? DateTime.MinValue;
        public int RunCount => runCount;

        public void Refresh()
        {
            Update(ref name, Rule.Name, nameof(Name));
            Update(ref triggerText, Rule.GetTriggerAsFriendlyString(), nameof(TriggerText));
            Update(ref statusText, Rule.Status.ToString(), nameof(StatusText));
            Update(ref lastRunText, Rule.GetLastRunAsFriendlyString(), nameof(LastRunText));

            if (runCount != Rule.RunCount)
            {
                runCount = Rule.RunCount;
                Raise(nameof(RunCount));
                Raise(nameof(RunCountText));
            }
        }

        void Update(ref string field, string newValue, string propertyName)
        {
            if (field == newValue) return;
            field = newValue;
            Raise(propertyName);
        }

        void Raise([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static IComparer CompareBy<TKey>(Func<RuleRow, TKey> selector) where TKey : IComparable<TKey>
        {
            return System.Collections.Generic.Comparer<object>.Create((x, y) =>
            {
                if (x is not RuleRow rowX || y is not RuleRow rowY) return 0;
                return selector(rowX).CompareTo(selector(rowY));
            });
        }
    }
}
