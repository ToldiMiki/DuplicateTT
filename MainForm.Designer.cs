using System.Drawing;
using System.Windows.Forms;

namespace SmartPageDuplicate
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        // --- Elrendezés ---
        private const int Margin_ = 14;
        private const int PanelW = 432;         // a két oszlop szélessége
        private const int HeaderH = 66;
        private const int LabelW = 92;
        private const int RowH = 30;
        private const int CtrlH = 25;

        // --- Színek: visszafogott, egyetlen hangsúllyal ---
        private static readonly Color Ink = Color.FromArgb(28, 32, 38);
        private static readonly Color InkSoft = Color.FromArgb(96, 104, 114);
        private static readonly Color Ground = Color.FromArgb(246, 247, 249);
        private static readonly Color Surface = Color.White;
        private static readonly Color Rule = Color.FromArgb(214, 220, 227);
        private static readonly Color AccentSrc = Color.FromArgb(38, 92, 150);   // forrás: hideg
        private static readonly Color AccentDst = Color.FromArgb(150, 96, 20);   // cél: meleg
        private static readonly Color DangerTint = Color.FromArgb(62, 72, 132);

        private static Font UiFont(float size = 9F, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI", size, style);

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Betűalapú skálázással a WinForms átméretezi a formot a saját font-metrikái szerint,
            // és a megadott ClientSize nem érvényesül - a jobb oldali panel levágódna. A DPI-alapú
            // skálázás kiszámítható, és a nagy felbontású kijelzőkön is helyesen nagyít.
            this.AutoScaleMode = AutoScaleMode.Dpi;

            this.Font = UiFont();
            this.BackColor = Ground;
            this.ForeColor = Ink;
            this.ClientSize = new Size(Margin_ * 3 + PanelW * 2, 830);
            this.MinimumSize = new Size(Margin_ * 3 + PanelW * 2 + 18, 620);
            this.StartPosition = FormStartPosition.CenterScreen;

            BuildHeader();
            BuildSourcePanel();
            BuildTargetPanel();
            BuildOutputArea();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ------------------------------------------------------------------ fejléc

        private void BuildHeader()
        {
            pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, HeaderH),
                BackColor = Surface,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            // Finom elválasztó a fejléc alján - keret helyett, hogy ne legyen dobozos.
            pnlHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(Rule);
                e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };

            lblAppName = new Label
            {
                Text = "SmartPage Duplicate",
                Font = UiFont(14F, FontStyle.Regular),
                ForeColor = Ink,
                // A magasságnak bőven a betűméret fölött kell lennie: 125%-os kijelzőskálázáson
                // a szoros keret levágja a lelógó szárakat.
                Location = new Point(Margin_, 8),
                Size = new Size(340, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblAppSubtitle = new Label
            {
                Text = "Menetrend és elrendezés másolása Smartpage szerverek között",
                Font = UiFont(8.5F),
                ForeColor = InkSoft,
                Location = new Point(Margin_ + 2, 40),
                Size = new Size(520, 18)
            };

            lblVersion = new Label
            {
                Font = UiFont(8.5F),
                ForeColor = InkSoft,
                Size = new Size(120, 18),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblVersion.Location = new Point(pnlHeader.Width - 120 - Margin_, 24);

            pnlHeader.Controls.AddRange(new Control[] { lblAppName, lblAppSubtitle, lblVersion });
            this.Controls.Add(pnlHeader);
        }

        // ------------------------------------------------------------------ forrás

        private void BuildSourcePanel()
        {
            grpSource = MakeGroup("Forrás", Margin_, HeaderH + Margin_, AccentSrc);

            int y = 26;
            lblServerLoad = MakeLabel("Szerver", y);
            cmbServerLoad = MakeCombo(y);

            y += RowH;
            lblLoadUsername = MakeLabel("Belépve", y);
            txtLoadUsername = MakeReadOnlyBox(y);

            y += RowH;
            lblLoadEntityType = MakeLabel("Típus", y);
            cmbLoadEntityType = MakeCombo(y);
            cmbLoadEntityType.Items.AddRange(new object[] { "Timetable", "Layout" });
            cmbLoadEntityType.SelectedIndex = 0;

            y += RowH;
            lblLoadEntityId = MakeLabel("Azonosító", y);
            txtLoadEntityId = new TextBox
            {
                Location = new Point(LabelW + 12, y),
                Size = new Size(PanelW - LabelW - 12 - 100 - 20, CtrlH),
                Font = UiFont()
            };
            // Tallózás: a szerver adja a listát, nem kell kézzel azonosítót gépelni.
            btnPickEntity = new Button
            {
                Text = "Tallózás…",
                Location = new Point(PanelW - 100 - 12, y - 1),
                Size = new Size(100, CtrlH + 2),
                Font = UiFont(),
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface,
                Cursor = Cursors.Hand
            };
            btnPickEntity.FlatAppearance.BorderColor = Rule;
            btnPickEntity.Click += new System.EventHandler(this.BtnPickEntity_Click);

            y += RowH + 8;
            btnLoad = MakePrimaryButton("Beolvasás", y, AccentSrc);
            btnLoad.Click += new System.EventHandler(this.BtnLoad_Click);

            grpSource.Height = y + 40 + 12;
            grpSource.Controls.AddRange(new Control[]
            {
                lblServerLoad, cmbServerLoad, lblLoadUsername, txtLoadUsername,
                lblLoadEntityType, cmbLoadEntityType, lblLoadEntityId, txtLoadEntityId,
                btnPickEntity, btnLoad
            });
            this.Controls.Add(grpSource);
        }

        // ------------------------------------------------------------------ cél

        private void BuildTargetPanel()
        {
            grpTarget = MakeGroup("Cél", Margin_ * 2 + PanelW, HeaderH + Margin_, AccentDst);

            int y = 26;
            lblServerSave = MakeLabel("Szerver", y);
            cmbServerSave = MakeCombo(y);

            y += RowH;
            lblSaveUsername = MakeLabel("Belépve", y);
            txtSaveUsername = MakeReadOnlyBox(y);

            y += RowH;
            lblSaveName = MakeLabel("Új név", y);
            txtSaveName = new TextBox
            {
                Location = new Point(LabelW + 12, y),
                Size = new Size(PanelW - LabelW - 12 - 12, CtrlH),
                Font = UiFont()
            };

            y += RowH + 2;
            // Száraz futtatás: minden lépés lefut a szerverre íráson kívül, így a küldendő
            // JSON és az összes figyelmeztetés ellenőrizhető, mielőtt bármi megváltozna.
            chkDryRun = new CheckBox
            {
                Text = "Száraz futtatás — nem ír a szerverre",
                Location = new Point(LabelW + 10, y),
                Size = new Size(PanelW - LabelW - 20, 22),
                Font = UiFont(),
                ForeColor = DangerTint,
                Cursor = Cursors.Hand
            };

            y += RowH + 4;
            btnSave = MakePrimaryButton("Mentés", y, AccentDst);
            btnSave.Click += new System.EventHandler(this.BtnSave_Click);

            grpTarget.Height = y + 40 + 12;
            grpTarget.Controls.AddRange(new Control[]
            {
                lblServerSave, cmbServerSave, lblSaveUsername, txtSaveUsername,
                lblSaveName, txtSaveName, chkDryRun, btnSave
            });
            this.Controls.Add(grpTarget);
        }

        // ------------------------------------------------------------------ kimenet

        private void BuildOutputArea()
        {
            int top = HeaderH + Margin_ + grpSource.Height + Margin_;
            int fullW = this.ClientSize.Width - Margin_ * 2;

            lblJsonCaption = MakeCaption("JSON előnézet", Margin_, top);
            txtJson = new TextBox
            {
                Location = new Point(Margin_, top + 20),
                Size = new Size(fullW, 300),
                Multiline = true,
                ReadOnly = true,
                BackColor = Surface,
                ForeColor = Ink,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Cascadia Mono", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                // A TextBox alapértelmezett felső határa többsoros módban is 32 767 karakter, és
                // a levágás nem látszik. Egyetlen layout-elem tartalma ennél nagyobb is lehet.
                MaxLength = int.MaxValue,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            int statusTop = top + 20 + 300 + Margin_;
            lblStatusCaption = MakeCaption("Napló", Margin_, statusTop);
            lblStatusCaption.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

            txtStatus = new RichTextBox
            {
                Location = new Point(Margin_, statusTop + 20),
                Size = new Size(fullW, this.ClientSize.Height - (statusTop + 20) - Margin_),
                ReadOnly = true,
                BackColor = Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFont(9.5F),
                ScrollBars = RichTextBoxScrollBars.Both,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            this.Controls.AddRange(new Control[] { lblJsonCaption, txtJson, lblStatusCaption, txtStatus });
        }

        // ------------------------------------------------------------------ építőelemek

        /// <summary>Csoportdoboz színes jelzősávval - a forrás és a cél így ránézésre elkülönül.</summary>
        private GroupBox MakeGroup(string title, int x, int y, Color accent)
        {
            var box = new GroupBox
            {
                Text = "  " + title + "  ",
                Location = new Point(x, y),
                Size = new Size(PanelW, 200),
                Font = UiFont(9.5F, FontStyle.Bold),
                ForeColor = accent,
                BackColor = Surface,
                Padding = new Padding(12, 6, 12, 12)
            };
            box.Paint += (s, e) =>
            {
                using var brush = new SolidBrush(accent);
                e.Graphics.FillRectangle(brush, 0, 0, box.Width, 3);
            };
            return box;
        }

        private Label MakeLabel(string text, int y) => new Label
        {
            Text = text,
            Location = new Point(12, y + 4),
            Size = new Size(LabelW - 6, 18),
            Font = UiFont(),
            ForeColor = InkSoft
        };

        private Label MakeCaption(string text, int x, int y) => new Label
        {
            Text = text.ToUpperInvariant(),
            Location = new Point(x + 2, y),
            Size = new Size(300, 16),
            Font = UiFont(7.5F, FontStyle.Bold),
            ForeColor = InkSoft
        };

        private ComboBox MakeCombo(int y) => new ComboBox
        {
            Location = new Point(LabelW + 12, y),
            Size = new Size(PanelW - LabelW - 12 - 12, CtrlH),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiFont(),
            FlatStyle = FlatStyle.Standard
        };

        private TextBox MakeReadOnlyBox(int y) => new TextBox
        {
            Location = new Point(LabelW + 12, y),
            Size = new Size(PanelW - LabelW - 12 - 12, CtrlH),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Ground,
            ForeColor = InkSoft,
            Font = UiFont()
        };

        private Button MakePrimaryButton(string text, int y, Color accent)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(12, y),
                Size = new Size(PanelW - 24, 38),
                Font = UiFont(10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.1f);
            return button;
        }

        #endregion

        private Panel pnlHeader;
        private Label lblAppName;
        private Label lblAppSubtitle;
        private Label lblVersion;

        private GroupBox grpSource;
        private Label lblServerLoad;
        private ComboBox cmbServerLoad;
        private Label lblLoadUsername;
        private TextBox txtLoadUsername;
        private Label lblLoadEntityType;
        private ComboBox cmbLoadEntityType;
        private Label lblLoadEntityId;
        private TextBox txtLoadEntityId;
        private Button btnPickEntity;
        private Button btnLoad;

        private GroupBox grpTarget;
        private Label lblServerSave;
        private ComboBox cmbServerSave;
        private Label lblSaveUsername;
        private TextBox txtSaveUsername;
        private Label lblSaveName;
        private TextBox txtSaveName;
        private CheckBox chkDryRun;
        private Button btnSave;

        private Label lblJsonCaption;
        private TextBox txtJson;
        private Label lblStatusCaption;
        private RichTextBox txtStatus;
    }
}
