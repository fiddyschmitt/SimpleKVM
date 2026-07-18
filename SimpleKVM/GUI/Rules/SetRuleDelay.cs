using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleKVM.GUI.Rules
{
    public class SetRuleDelay : Form
    {
        readonly NumericUpDown nudDelay;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int DelaySeconds
        {
            get => (int)nudDelay.Value;
            set => nudDelay.Value = Math.Clamp(value, (int)nudDelay.Minimum, (int)nudDelay.Maximum);
        }

        public SetRuleDelay()
        {
            Text = "Set delay";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(324, 106);

            var lblPrompt = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = "Wait this many seconds after the trigger fires\nbefore running the actions:"
            };

            nudDelay = new NumericUpDown
            {
                Location = new Point(12, 52),
                Size = new Size(60, 23),
                Maximum = 3600
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(156, 71),
                Size = new Size(75, 23)
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(237, 71),
                Size = new Size(75, 23)
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblPrompt);
            Controls.Add(nudDelay);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            Shown += (s, e) => nudDelay.Select(0, nudDelay.Text.Length);
        }
    }
}
