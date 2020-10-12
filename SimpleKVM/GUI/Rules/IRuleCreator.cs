using SimpleKVM.Rules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleKVM.GUI.Rules
{
    public interface IRuleCreator
    {
        public Rule? GetRule();
    }
}
