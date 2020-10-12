using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SimpleKVM.Rules.Actions
{
    public interface IAction
    {
        public bool Run();
    }
}
