using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SmartPageDuplicate
{
    /// <summary>Egy választható sor: azonosító, név és egy típusfüggő kiegészítő oszlop.</summary>
    public record PickerRow(int Id, string Name, string Extra);

    /// <summary>
    /// Névre szűrhető listából enged elemet választani. Eddig az azonosítót kézzel kellett
    /// begépelni, pedig a szerver adja a listát - egy elgépelt ID-ből pedig rossz elem másolása
    /// lesz, amit csak a JSON-ból lehetne észrevenni.
    /// </summary>
    public class EntityPickerDialog : Form
    {
        private readonly List<PickerRow> _rows;
        private readonly TextBox _filter;
        private readonly ListView _list;
        private readonly Label _count;

        public int SelectedId { get; private set; }
        public string SelectedName { get; private set; } = "";

        public EntityPickerDialog(string title, string extraColumnHeader, List<PickerRow> rows)
        {
            _rows = rows;

            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(680, 460);
            MinimumSize = new Size(520, 320);
            Font = new Font("Segoe UI", 9F);

            var label = new Label
            {
                Text = "Szűrés névre vagy azonosítóra:",
                Location = new Point(12, 12),
                Size = new Size(200, 18)
            };

            _filter = new TextBox
            {
                Location = new Point(12, 32),
                Size = new Size(ClientSize.Width - 24, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _filter.TextChanged += (_, _) => ApplyFilter();

            _list = new ListView
            {
                Location = new Point(12, 64),
                Size = new Size(ClientSize.Width - 24, ClientSize.Height - 64 - 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                GridLines = false
            };
            _list.Columns.Add("ID", 70, HorizontalAlignment.Left);
            _list.Columns.Add("Név", 400, HorizontalAlignment.Left);
            _list.Columns.Add(extraColumnHeader, 170, HorizontalAlignment.Left);
            _list.DoubleClick += (_, _) => Accept();
            _list.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; }
            };

            _count = new Label
            {
                Location = new Point(12, ClientSize.Height - 46),
                Size = new Size(300, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                ForeColor = Theme.Info
            };

            var ok = new Button
            {
                Text = "Kiválaszt",
                Location = new Point(ClientSize.Width - 190, ClientSize.Height - 48),
                Size = new Size(85, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            ok.Click += (_, _) => Accept();

            var cancel = new Button
            {
                Text = "Mégse",
                Location = new Point(ClientSize.Width - 97, ClientSize.Height - 48),
                Size = new Size(85, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { label, _filter, _list, _count, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;

            ApplyFilter();
        }

        /// <summary>
        /// A szűrés szóközzel elválasztva több részletre is illeszkedik, hogy a hosszú,
        /// összetett neveket (pl. "MNR_Szekszard_29_Helyi4") is meg lehessen találni.
        /// </summary>
        private void ApplyFilter()
        {
            string[] terms = _filter.Text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var matches = _rows.Where(row =>
                terms.All(term =>
                    row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || row.Id.ToString().Contains(term, StringComparison.Ordinal)
                    || row.Extra.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in matches)
            {
                var item = new ListViewItem(row.Id.ToString()) { Tag = row };
                item.SubItems.Add(row.Name);
                item.SubItems.Add(row.Extra);
                _list.Items.Add(item);
            }
            _list.EndUpdate();

            if (_list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
            }
            _count.Text = matches.Count == _rows.Count
                ? $"{_rows.Count} elem"
                : $"{matches.Count} találat a(z) {_rows.Count} elemből";
        }

        private void Accept()
        {
            if (_list.SelectedItems.Count == 0) return;
            if (_list.SelectedItems[0].Tag is not PickerRow row) return;

            SelectedId = row.Id;
            SelectedName = row.Name;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
