using System;
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
        private const int LogoW = 86;          // a logóblokk szélessége a fejlécben
        private const int LabelW = 92;
        private const int RowH = 30;
        private const int CtrlH = 25;

        // --- Színek: a HC Linear arculatból (lásd Theme.cs) ---
        // A webes felületen a gombok petróleumkékek, és hoverre mentára váltanak; itt ez a két
        // szín különbözteti meg a forrást (olvasás) a céltól (írás).
        private static readonly Color Ink = Theme.Ink;
        private static readonly Color InkSoft = Theme.InkSoft;
        private static readonly Color Ground = Theme.Ground;
        private static readonly Color Surface = Theme.Surface;
        private static readonly Color Rule = Theme.Rule;
        private static readonly Color AccentSrc = Theme.Brand;    // forrás: mély petróleum
        private static readonly Color AccentDst = Theme.Accent;   // cél: élénk menta
        private static readonly Color DangerTint = Theme.DryRun;

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
            AlignPanels();
            BuildOutputArea();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ------------------------------------------------------------------ fejléc

        private void BuildHeader()
        {
            // A fejléc a márka vezérszínét viseli, ahogy a webes felület is.
            pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, HeaderH),
                BackColor = Theme.Brand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            // Menta csík a fejléc alján - a márka második színe, a háttérkép átmenetét idézve.
            pnlHeader.Paint += (s, e) =>
            {
                using var brush = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(brush, 0, pnlHeader.Height - 3, pnlHeader.Width, 3);
            };

            // A HC Linear jelkép: három négyzet, alatta a cégnév - a logó felépítését követve.
            // Kódból rajzolva, hogy átlátszó maradjon és a kijelzőskálázást is kövesse.
            pnlLogo = new Panel
            {
                Location = new Point(Margin_, 12),
                Size = new Size(LogoW, HeaderH - 24),
                BackColor = Color.Transparent
            };
            pnlLogo.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                const int box = 11, gap = 5;
                Color[] colors = { Theme.MarkGreen, Color.White, Theme.MarkBlue };
                for (int i = 0; i < 3; i++)
                {
                    using var brush = new SolidBrush(colors[i]);
                    e.Graphics.FillRectangle(brush, i * (box + gap), 0, box, box);
                }
                using var font = UiFont(11.5F, FontStyle.Bold);
                using var text = new SolidBrush(Color.White);
                e.Graphics.DrawString("HC Linear", font, text, -2, box + 5);
            };

            lblAppName = new Label
            {
                Text = "SmartPage – másolás / duplikálás",
                Font = UiFont(14F, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                // A magasságnak bőven a betűméret fölött kell lennie: 125%-os kijelzőskálázáson
                // a szoros keret levágja a lelógó szárakat.
                Location = new Point(Margin_ + LogoW + 18, 8),
                Size = new Size(430, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblAppSubtitle = new Label
            {
                Text = "Menetrendi táblák és/vagy Slide layout-ok duplikálása szerveren belül, vagy másolása szerverek között",
                Font = UiFont(8.5F),
                // A fehér 70%-os keveréke a márkaszínnel: olvasható, de nem versenyez a címmel.
                ForeColor = Color.FromArgb(178, 205, 216),
                BackColor = Color.Transparent,
                Location = new Point(Margin_ + LogoW + 20, 40),
                // A teljes alcím elfér: a verziócímke a cím sorába került, nem alá.
                Size = new Size(pnlHeader.Width - (Margin_ + LogoW + 20) - Margin_, 18)
            };

            lblVersion = new Label
            {
                Font = UiFont(8.5F),
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                Size = new Size(120, 18),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblVersion.Location = new Point(pnlHeader.Width - 120 - Margin_, 14);

            pnlHeader.Controls.AddRange(new Control[] { pnlLogo, lblAppName, lblAppSubtitle, lblVersion });
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

        /// <summary>
        /// A két panel eltérő számú sort tartalmaz, ezért magától nem egyforma magas, és a két
        /// főgomb sem kerülne egy vonalba. Az igazítás a nagyobbikhoz történik.
        /// </summary>
        private void AlignPanels()
        {
            int buttonTop = Math.Max(btnLoad.Top, btnSave.Top);
            btnLoad.Top = buttonTop;
            btnSave.Top = buttonTop;

            int panelHeight = Math.Max(grpSource.Height, grpTarget.Height);
            grpSource.Height = panelHeight;
            grpTarget.Height = panelHeight;
        }

        // ------------------------------------------------------------------ kimenet

        private void BuildOutputArea()
        {
            int top = HeaderH + Margin_ + grpSource.Height + Margin_;
            int fullW = this.ClientSize.Width - Margin_ * 2;
            int fullH = this.ClientSize.Height - top - Margin_;

            // A JSON-előnézet és a napló egymás rovására húzható: a köztük lévő csík fogható.
            splitOutput = new SplitContainer
            {
                Location = new Point(Margin_, top),
                Size = new Size(fullW, fullH),
                Orientation = Orientation.Horizontal,
                BackColor = Ground,
                SplitterWidth = 8,
                // Egyik oldal se tűnhessen el teljesen: a felirat és néhány sor mindig maradjon.
                Panel1MinSize = 90,
                Panel2MinSize = 90,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            // Fogópont-jelzés a csíkon, hogy látszódjon: ez húzható.
            splitOutput.Paint += (s, e) =>
            {
                Rectangle r = splitOutput.SplitterRectangle;
                using var line = new SolidBrush(Theme.Rule);
                e.Graphics.FillRectangle(line, r.Left, r.Top + r.Height / 2, r.Width, 1);

                using var dot = new SolidBrush(Theme.InkSoft);
                int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
                for (int i = -2; i <= 2; i++)
                {
                    e.Graphics.FillRectangle(dot, cx + i * 8 - 1, cy - 1, 3, 3);
                }
            };
            // Az egérmutató is jelezze, hogy a csík fogható.
            splitOutput.MouseEnter += (s, e) => splitOutput.Cursor = Cursors.HSplit;

            lblJsonCaption = MakeCaption("JSON előnézet", 0, 0);
            lblJsonCaption.Location = new Point(2, 0);

            txtJson = new TextBox
            {
                Location = new Point(0, 18),
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
            txtJson.Size = new Size(fullW, splitOutput.Panel1.Height - 18);

            lblStatusCaption = MakeCaption("Napló", 0, 0);
            lblStatusCaption.Location = new Point(2, 2);

            txtStatus = new RichTextBox
            {
                Location = new Point(0, 20),
                ReadOnly = true,
                BackColor = Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFont(9.5F),
                ScrollBars = RichTextBoxScrollBars.Both,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            txtStatus.Size = new Size(fullW, splitOutput.Panel2.Height - 20);

            splitOutput.Panel1.Controls.AddRange(new Control[] { lblJsonCaption, txtJson });
            splitOutput.Panel2.Controls.AddRange(new Control[] { lblStatusCaption, txtStatus });
            this.Controls.Add(splitOutput);

            // A kiinduló osztás: a JSON kapja a nagyobb részt. A SplitterDistance csak akkor
            // állítható be helyesen, ha a vezérlő már a formon van és ismeri a méretét.
            splitOutput.SplitterDistance = (int)(fullH * 0.62);
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
                // A cím mindig a sötét márkaszín: a menta fehér alapon nem volna olvasható.
                // A megkülönböztetést a felső sáv színe végzi.
                ForeColor = Theme.Brand,
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
            // A menta akcentuson a fehér felirat olvashatatlan; a világos háttér sötét szöveget kap.
            bool lightBackground = (accent.R * 0.299 + accent.G * 0.587 + accent.B * 0.114) > 150;

            var button = new Button
            {
                Text = text,
                Location = new Point(12, y),
                Size = new Size(PanelW - 24, 38),
                Font = UiFont(10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = lightBackground ? Theme.Brand : Color.White,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.1f);
            return button;
        }

        #endregion

        private Panel pnlHeader;
        private Panel pnlLogo;
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

        private SplitContainer splitOutput;
        private Label lblJsonCaption;
        private TextBox txtJson;
        private Label lblStatusCaption;
        private RichTextBox txtStatus;
    }
}
