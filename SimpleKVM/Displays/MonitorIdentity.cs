namespace SimpleKVM.Displays
{
    /// <summary>
    /// Monitors are identified by an MD5 over their bounds in the global desktop coordinate
    /// space. Every platform must produce identical ids for identical geometry, and rules.json
    /// stores these ids, so the format below is frozen — changing it orphans users' rules.
    /// </summary>
    public static class MonitorIdentity
    {
        public static string FromBounds(int left, int top, int right, int bottom)
        {
            var str = $"{left},{top},{right},{bottom}";
            return str.CreateMD5();
        }
    }
}
