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
        public required int MonitorNumber;
        public List<Source> Sources = [];
    }

    public class Source
    {
        public required int SourceId;
        public required string SourceName;
    }
}
