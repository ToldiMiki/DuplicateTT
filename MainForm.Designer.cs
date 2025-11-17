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
            this.lblLoadId = new System.Windows.Forms.Label();
            this.txtLoadId = new System.Windows.Forms.TextBox();
            this.btnLoad = new System.Windows.Forms.Button();

            this.lblServerSave = new System.Windows.Forms.Label();
            this.cmbServerSave = new System.Windows.Forms.ComboBox();
            
            this.lblSaveName = new System.Windows.Forms.Label();
            this.txtSaveName = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();

            this.txtStatus = new System.Windows.Forms.TextBox();
            this.txtJson = new System.Windows.Forms.TextBox();

            this.SuspendLayout();

            // --- Koordináták és méretek ---
            int leftColX = 15;
            int rightColX = 400;
            int labelWidth = 100;
            int inputWidth = 220;
            int spacingY = 30;
            int startY = 15;

            // --- LOAD OSZLOP ---
            this.lblServerLoad.Text = "Load szerver:";
            this.lblServerLoad.Location = new System.Drawing.Point(leftColX, startY);
            this.cmbServerLoad.Location = new System.Drawing.Point(leftColX + labelWidth, startY - 3);
            this.cmbServerLoad.Size = new System.Drawing.Size(inputWidth, 23);
            this.cmbServerLoad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // auth/session controls removed from Load column

            this.lblLoadId.Text = "Elem ID:";
            this.lblLoadId.Location = new System.Drawing.Point(leftColX, startY + spacingY * 3);
            this.txtLoadId.Location = new System.Drawing.Point(leftColX + labelWidth, startY + spacingY * 3 - 3);
            this.txtLoadId.Size = new System.Drawing.Size(inputWidth, 23);

            this.btnLoad.Text = "Elem beolvasása";
            this.btnLoad.Location = new System.Drawing.Point(leftColX, startY + spacingY * 4 + 5);
            this.btnLoad.Size = new System.Drawing.Size(labelWidth + inputWidth, 35);
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);

            // --- SAVE OSZLOP ---
            this.lblServerSave.Text = "Save szerver:";
            this.lblServerSave.Location = new System.Drawing.Point(rightColX, startY);
            this.cmbServerSave.Location = new System.Drawing.Point(rightColX + labelWidth, startY - 3);
            this.cmbServerSave.Size = new System.Drawing.Size(inputWidth, 23);
            this.cmbServerSave.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // auth/session controls removed from Save column

            this.lblSaveName.Text = "Új név:";
            this.lblSaveName.Location = new System.Drawing.Point(rightColX, startY + spacingY * 3);
            this.txtSaveName.Location = new System.Drawing.Point(rightColX + labelWidth, startY + spacingY * 3 - 3);
            this.txtSaveName.Size = new System.Drawing.Size(inputWidth, 23);

            this.btnSave.Text = "Elem mentése";
            this.btnSave.Location = new System.Drawing.Point(rightColX, startY + spacingY * 4 + 5);
            this.btnSave.Size = new System.Drawing.Size(labelWidth + inputWidth, 35);
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            // --- ALSÓ RÉSZ ---
            // JSON mező: az ablak aljáig méretezhető
            this.txtJson.Location = new System.Drawing.Point(15, 200);
            this.txtJson.Size = new System.Drawing.Size(760, 420);
            this.txtJson.Multiline = true;
            this.txtJson.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtJson.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtStatus.Anchor = (System.Windows.Forms.AnchorStyles.Left
                                   | System.Windows.Forms.AnchorStyles.Right
                                   | System.Windows.Forms.AnchorStyles.Top
                                   | System.Windows.Forms.AnchorStyles.Bottom);

            // Status mező: fix alul, mindig a helyén
            this.txtStatus.Location = new System.Drawing.Point(15, 630);
            this.txtStatus.Size = new System.Drawing.Size(760, 170);
            this.txtStatus.Multiline = true;
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtStatus.Anchor = (System.Windows.Forms.AnchorStyles.Top
                                   | System.Windows.Forms.AnchorStyles.Bottom
                                   | System.Windows.Forms.AnchorStyles.Left
                                   | System.Windows.Forms.AnchorStyles.Right);

            // --- FORM BEÁLLÍTÁSOK ---
            this.ClientSize = new System.Drawing.Size(800, 810);
            this.MinimumSize = new System.Drawing.Size(800, 810);
            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                lblServerLoad, cmbServerLoad,
                lblLoadId, txtLoadId, btnLoad,
                lblServerSave, cmbServerSave,
                lblSaveName, txtSaveName, btnSave,
                txtJson, txtStatus
            });

            this.Text = "Smartpage Timetable Duplicate V2";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblServerLoad;
        private System.Windows.Forms.ComboBox cmbServerLoad;
        // auth/session controls removed
        private System.Windows.Forms.Label lblLoadId;
        private System.Windows.Forms.TextBox txtLoadId;
        private System.Windows.Forms.Button btnLoad;

        private System.Windows.Forms.Label lblServerSave;
        private System.Windows.Forms.ComboBox cmbServerSave;
        // auth/session controls removed
        private System.Windows.Forms.Label lblSaveName;
        private System.Windows.Forms.TextBox txtSaveName;
        private System.Windows.Forms.Button btnSave;

        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.TextBox txtJson;
    }
}
