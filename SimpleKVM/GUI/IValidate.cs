using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleKVM.GUI
{
    public interface IValidate
    {
        public List<ValidationResult> ValidateData();
    }
}
