namespace SmartpageTimetableDuplicateV1
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

        private void InitializeComponent()
        {
            this.lblServerLoad = new System.Windows.Forms.Label();
            this.cmbServerLoad = new System.Windows.Forms.ComboBox();
            this.txtLoadUsername = new System.Windows.Forms.TextBox();
            this.lblLoadEntityType = new System.Windows.Forms.Label();
            this.cmbLoadEntityType = new System.Windows.Forms.ComboBox();
            this.lblLoadEntityId = new System.Windows.Forms.Label();
            this.txtLoadEntityId = new System.Windows.Forms.TextBox();
            this.btnLoad = new System.Windows.Forms.Button();

            this.lblServerSave = new System.Windows.Forms.Label();
            this.cmbServerSave = new System.Windows.Forms.ComboBox();
            this.txtSaveUsername = new System.Windows.Forms.TextBox();

            this.lblSaveName = new System.Windows.Forms.Label();
            this.txtSaveName = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.chkDryRun = new System.Windows.Forms.CheckBox();

            this.txtStatus = new System.Windows.Forms.RichTextBox();
            this.txtJson = new System.Windows.Forms.TextBox();

            this.SuspendLayout();

            // --- Koordináták és méretek ---
            int leftColX = 15;
            int rightColX = 400;
            int labelWidth = 115;
            int inputWidth = 240;
            int spacingY = 30;
            int startY = 15;

            // --- LOAD OSZLOP ---
            this.lblServerLoad.Text = "Load szerver:";
            this.lblServerLoad.Location = new System.Drawing.Point(leftColX, startY);
            this.cmbServerLoad.Location = new System.Drawing.Point(leftColX + labelWidth, startY - 3);
            this.cmbServerLoad.Size = new System.Drawing.Size(inputWidth, 23);
            this.cmbServerLoad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            this.lblLoadUsername = new System.Windows.Forms.Label();
            this.lblLoadUsername.Text = "Felhasználónév:";
            this.lblLoadUsername.Location = new System.Drawing.Point(leftColX, startY + spacingY);
            this.lblLoadUsername.Size = new System.Drawing.Size(labelWidth, 23);

            this.txtLoadUsername.Location = new System.Drawing.Point(leftColX + labelWidth, startY + spacingY - 3);
            this.txtLoadUsername.Size = new System.Drawing.Size(inputWidth, 23);
            this.txtLoadUsername.ReadOnly = true;

            this.lblLoadEntityType.Text = "Entity type:";
            this.lblLoadEntityType.Location = new System.Drawing.Point(leftColX, startY + spacingY * 2 + 10);
            this.cmbLoadEntityType.Location = new System.Drawing.Point(leftColX + labelWidth, startY + spacingY * 2 + 10 - 3);
            this.cmbLoadEntityType.Size = new System.Drawing.Size(inputWidth, 23);
            this.cmbLoadEntityType.Items.AddRange(new object[] { "Timetable", "Layout" });
            this.cmbLoadEntityType.SelectedIndex = 0;

            this.lblLoadEntityId.Text = "Entity ID:";
            this.lblLoadEntityId.Location = new System.Drawing.Point(leftColX, startY + spacingY * 3 + 10);
            this.txtLoadEntityId.Location = new System.Drawing.Point(leftColX + labelWidth, startY + spacingY * 3 + 10 - 3);
            this.txtLoadEntityId.Size = new System.Drawing.Size(inputWidth - 90, 23);

            // Tallózás: a szerver adja a listát, nem kell kézzel ID-t gépelni.
            this.btnPickEntity = new System.Windows.Forms.Button();
            this.btnPickEntity.Text = "Tallózás…";
            this.btnPickEntity.Location = new System.Drawing.Point(leftColX + labelWidth + inputWidth - 85, startY + spacingY * 3 + 10 - 4);
            this.btnPickEntity.Size = new System.Drawing.Size(85, 25);
            this.btnPickEntity.Click += new System.EventHandler(this.BtnPickEntity_Click);

            this.btnLoad.Text = "Elem beolvasása";
            this.btnLoad.Location = new System.Drawing.Point(leftColX, startY + spacingY * 4 + 10);
            this.btnLoad.Size = new System.Drawing.Size(labelWidth + inputWidth, 35);
            this.btnLoad.Click += new System.EventHandler(this.BtnLoad_Click);

            // --- SAVE OSZLOP ---
            this.lblServerSave.Text = "Save szerver:";
            this.lblServerSave.Location = new System.Drawing.Point(rightColX, startY);
            this.cmbServerSave.Location = new System.Drawing.Point(rightColX + labelWidth, startY - 3);
            this.cmbServerSave.Size = new System.Drawing.Size(inputWidth, 23);
            this.cmbServerSave.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            this.lblSaveUsername = new System.Windows.Forms.Label();
            this.lblSaveUsername.Text = "Felhasználónév:";
            this.lblSaveUsername.Location = new System.Drawing.Point(rightColX, startY + spacingY);
            this.lblSaveUsername.Size = new System.Drawing.Size(labelWidth, 23);

            this.txtSaveUsername.Location = new System.Drawing.Point(rightColX + labelWidth, startY + spacingY - 3);
            this.txtSaveUsername.Size = new System.Drawing.Size(inputWidth, 23);
            this.txtSaveUsername.ReadOnly = true;

            this.lblSaveName.Text = "Új név:";
            this.lblSaveName.Location = new System.Drawing.Point(rightColX, startY + spacingY * 2 + 15 + 10);
            this.lblSaveName.Size = new System.Drawing.Size(labelWidth - 60, 23);

            this.txtSaveName.Location = new System.Drawing.Point(rightColX + labelWidth - 60, startY + spacingY * 2 + 15 + 10 - 3);
            this.txtSaveName.Size = new System.Drawing.Size(inputWidth + 60, 23);

            // Száraz futtatás: minden lépés lefut a szerverre íráson kívül, így a küldendő
            // JSON és az összes figyelmeztetés ellenőrizhető, mielőtt bármi megváltozna.
            this.chkDryRun.Text = "Száraz futtatás (nem ír a szerverre)";
            // A névmező alja 120-nál, a Mentés gomb teteje 145-nél van - a jelölőnégyzet ebbe a résbe kerül.
            this.chkDryRun.Location = new System.Drawing.Point(rightColX, startY + spacingY * 3 + 17);
            this.chkDryRun.Size = new System.Drawing.Size(labelWidth + inputWidth, 22);
            this.chkDryRun.ForeColor = System.Drawing.Color.FromArgb(60, 60, 120);

            this.btnSave.Text = "Elem mentése";
            this.btnSave.Location = new System.Drawing.Point(rightColX, startY + spacingY * 4 + 10);
            this.btnSave.Size = new System.Drawing.Size(labelWidth + inputWidth, 35);
            this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);

            // --- ALSÓ RÉSZ ---
            // JSON mező: az ablak aljáig méretezhető
            this.txtJson.Location = new System.Drawing.Point(15, 210);
            this.txtJson.Size = new System.Drawing.Size(760, 380);
            this.txtJson.Multiline = true;
            this.txtJson.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtJson.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            // A TextBox alapértelmezett felső határa többsoros módban is 32 767 karakter, és a
            // levágás nem látszik. Egyetlen layout-elem content mezője ennél nagyobb is lehet.
            this.txtJson.MaxLength = int.MaxValue;
            this.txtStatus.Anchor = (System.Windows.Forms.AnchorStyles.Left
                                   | System.Windows.Forms.AnchorStyles.Right
                                   | System.Windows.Forms.AnchorStyles.Top
                                   | System.Windows.Forms.AnchorStyles.Bottom);

            // Status mező: fix alul, mindig a helyén
            this.txtStatus.Location = new System.Drawing.Point(15, 600);
            this.txtStatus.Size = new System.Drawing.Size(760, 170);
            this.txtStatus.Multiline = true;
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtStatus.Anchor = (System.Windows.Forms.AnchorStyles.Top
                                   | System.Windows.Forms.AnchorStyles.Bottom
                                   | System.Windows.Forms.AnchorStyles.Left
                                   | System.Windows.Forms.AnchorStyles.Right);

            // --- FORM BEÁLLÍTÁSOK ---
            this.ClientSize = new System.Drawing.Size(800, 780);
            this.MinimumSize = new System.Drawing.Size(800, 780);
            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                lblServerLoad, cmbServerLoad, lblLoadUsername, txtLoadUsername,
                lblLoadEntityType, cmbLoadEntityType, lblLoadEntityId, txtLoadEntityId, btnPickEntity, btnLoad,
                lblServerSave, cmbServerSave, lblSaveUsername, txtSaveUsername,
                lblSaveName, txtSaveName, chkDryRun, btnSave,
                txtJson, txtStatus
            });

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = $"Smartpage Timetable or Layout Duplicate v{version?.ToString(3) ?? "?"}";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblServerLoad;
        private System.Windows.Forms.ComboBox cmbServerLoad;
        private System.Windows.Forms.Label lblLoadUsername;
        private System.Windows.Forms.TextBox txtLoadUsername;
        private System.Windows.Forms.Label lblLoadEntityType;
        private System.Windows.Forms.ComboBox cmbLoadEntityType;
        private System.Windows.Forms.Label lblLoadEntityId;
        private System.Windows.Forms.TextBox txtLoadEntityId;
        private System.Windows.Forms.Button btnPickEntity;
        private System.Windows.Forms.Button btnLoad;

        private System.Windows.Forms.Label lblServerSave;
        private System.Windows.Forms.ComboBox cmbServerSave;
        private System.Windows.Forms.Label lblSaveUsername;
        private System.Windows.Forms.TextBox txtSaveUsername;
        private System.Windows.Forms.Label lblSaveName;
        private System.Windows.Forms.TextBox txtSaveName;
        private System.Windows.Forms.CheckBox chkDryRun;
        private System.Windows.Forms.Button btnSave;

        private System.Windows.Forms.RichTextBox txtStatus;
        private System.Windows.Forms.TextBox txtJson;
    }
}
