using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartPageDuplicate
{
    /// <summary>
    /// A mentés előtti utolsó képernyő: mi fordítódik le, mi marad ki, és hova kerül a másolat.
    /// Eddig a Mentés gomb azonnal írt a szerverre, és a figyelmeztetések csak utólag, a
    /// státusznaplóban jelentek meg - amikor már késő volt.
    /// </summary>
    public class CopyPreviewDialog : Form
    {
        public CopyPreviewDialog(string summary, bool dryRun)
        {
            Text = dryRun ? "Előnézet - száraz futtatás" : "Előnézet - mentés a szerverre";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(640, 520);
            MinimumSize = new Size(480, 360);
            Font = new Font("Segoe UI", 9F);

            var text = new RichTextBox
            {
                Location = new Point(12, 12),
                Size = new Size(ClientSize.Width - 24, ClientSize.Height - 12 - 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.White,
                Text = summary,
                DetectUrls = false
            };
            text.Select(0, 0);

            var confirm = new Button
            {
                Text = dryRun ? "Száraz futtatás indítása" : "Mentés indítása",
                Location = new Point(ClientSize.Width - 300, ClientSize.Height - 44),
                Size = new Size(190, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            if (!dryRun)
            {
                confirm.BackColor = Color.FromArgb(230, 240, 230);
            }

            var cancel = new Button
            {
                Text = "Mégse",
                Location = new Point(ClientSize.Width - 100, ClientSize.Height - 44),
                Size = new Size(88, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { text, confirm, cancel });
            AcceptButton = confirm;
            CancelButton = cancel;

            // A Mégse legyen az alapértelmezett fókusz: a szerverre írás ne egy véletlen
            // Enter-lenyomáson múljon.
            ActiveControl = cancel;
        }
    }
}
