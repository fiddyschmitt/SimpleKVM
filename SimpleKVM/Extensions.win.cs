using SimpleKVM.Displays;
using System.Linq;
using System.Windows.Forms;
using static DDCKVMService.MonitorController;

namespace SimpleKVM
{
    public static partial class Extensions
    {
        public static ColumnHeader? GetColumnByName(this ListView listView, string columnName)
        {
            var result = listView
                            .Columns
                            .Cast<ColumnHeader>()
                            .FirstOrDefault(col => col.Text.Equals(columnName));

            return result;
        }

        public static int ScreenIndex(this Screen screen)
        {
            var result = Screen
                            .AllScreens
                            .OrderBy(scr => scr.Bounds.Left)
                            .ThenBy(scr => scr.Bounds.Top)
                            .ThenBy(scr => scr.DeviceName)
                            .Select((scr, index) => new
                            {
                                Screen = scr,
                                Index = index
                            })
                            .Where(scr => scr.Screen.DeviceName.Equals(screen.DeviceName))
                            .Select(scr => scr.Index)
                            .First();

            result++;

            return result;
        }

        public static string GetUniqueId(this Screen screen)
        {
            return MonitorIdentity.FromBounds(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Right, screen.Bounds.Bottom);
        }

        public static string GetUniqueId(this MONITORINFOEX monitorInfo)
        {
            return MonitorIdentity.FromBounds(monitorInfo.Monitor.Left, monitorInfo.Monitor.Top, monitorInfo.Monitor.Right, monitorInfo.Monitor.Bottom);
        }
    }
}
