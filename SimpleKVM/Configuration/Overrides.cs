using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.Configuration
{
    public class Overrides
    {
        public List<MonitorOverride>? MonitorOverrides;
    }

    public class MonitorOverride
    {
        public required string Model;
        public List<SourceDisplayNameOverride> SourceDisplayNames = [];
    }

    public class SourceDisplayNameOverride
    {
        public required int SourceId;
        public required string NewDisplayName;
    }
}
