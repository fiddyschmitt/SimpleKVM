using System.Text;

namespace SimpleKVM.Platform.linux
{
    /// <summary>
    /// Escaping for the Exec key of a freedesktop .desktop file, which has two layers: an
    /// argument is double-quoted with backslash escapes for the shell-like parser, and the
    /// whole value is then a "string" in which every backslash is itself escaped and every
    /// literal percent sign is doubled (single ones are field codes).
    /// </summary>
    public static class DesktopEntry
    {
        public static string QuoteExecArgument(string argument)
        {
            var quoted = new StringBuilder("\"");
            foreach (char c in argument)
            {
                if (c is '"' or '`' or '$' or '\\') quoted.Append('\\');
                quoted.Append(c);
            }
            quoted.Append('"');

            return quoted.ToString().Replace("\\", "\\\\").Replace("%", "%%");
        }
    }
}
