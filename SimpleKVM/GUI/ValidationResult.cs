using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SimpleKVM.GUI
{
    public class ValidationResult
    {
        public ValidationResult(Control control, string errorMessage, ErrorIconAlignment topRight, int iconPadding)
        {
            Control = control;
            ErrorMessage = errorMessage;
            Alignment = topRight;
            IconPadding = iconPadding;
        }

        public Control Control { get; }
        public string ErrorMessage { get; }
        public ErrorIconAlignment Alignment { get; }
        public int IconPadding { get; }
    }
}
