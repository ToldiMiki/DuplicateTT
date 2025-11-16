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
            this.lblLoadAuth = new System.Windows.Forms.Label();
            this.txtLoadAuth = new System.Windows.Forms.TextBox();
            this.lblLoadSession = new System.Windows.Forms.Label();
            this.txtLoadSession = new System.Windows.Forms.TextBox();
            this.lblLoadId = new System.Windows.Forms.Label();
            this.txtLoadId = new System.Windows.Forms.TextBox();
            this.btnLoad = new System.Windows.Forms.Button();

            this.lblServerSave = new System.Windows.Forms.Label();
            this.cmbServerSave = new System.Windows.Forms.ComboBox();
            this.lblSaveAuth = new System.Windows.Forms.Label();
            this.txtSaveAuth = new System.Windows.Forms.TextBox();
            this.lblSaveSession = new System.Windows.Forms.Label();
            this.txtSaveSession = new System.Windows.Forms.TextBox();
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

            this.lblLoadAuth.Text = "Auth token:";
            this.lblLoadAuth.Location = new System.Drawing.Point(leftColX, startY + spacingY);
            this.txtLoadAuth.Location = new System.Drawing.Point(leftColX + labelWidth, startY + spacingY - 3);
            this.txtLoadAuth.Size = new System.Drawing.Size(inputWidth, 23);

            this.lblLoadSession.Text = "Session ID:";
            this.lblLoadSession.Location = new System.Drawing.Point(leftColX, startY + spacingY * 2);
            this.txtLoadSession.Location = new System.Drawing.Point(leftColX + labelWidth, startY + spacingY * 2 - 3);
            this.txtLoadSession.Size = new System.Drawing.Size(inputWidth, 23);

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

            this.lblSaveAuth.Text = "Auth token:";
            this.lblSaveAuth.Location = new System.Drawing.Point(rightColX, startY + spacingY);
            this.txtSaveAuth.Location = new System.Drawing.Point(rightColX + labelWidth, startY + spacingY - 3);
            this.txtSaveAuth.Size = new System.Drawing.Size(inputWidth, 23);

            this.lblSaveSession.Text = "Session ID:";
            this.lblSaveSession.Location = new System.Drawing.Point(rightColX, startY + spacingY * 2);
            this.txtSaveSession.Location = new System.Drawing.Point(rightColX + labelWidth, startY + spacingY * 2 - 3);
            this.txtSaveSession.Size = new System.Drawing.Size(inputWidth, 23);

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
            this.txtJson.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtJson.Location = new System.Drawing.Point(15, 200);
            this.txtJson.Multiline = true;
            this.txtJson.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtJson.Size = new System.Drawing.Size(760, 420);
            this.txtJson.Anchor = (System.Windows.Forms.AnchorStyles.Top
                                 | System.Windows.Forms.AnchorStyles.Bottom
                                 | System.Windows.Forms.AnchorStyles.Left
                                 | System.Windows.Forms.AnchorStyles.Right);

            // Status mező: fix alul, mindig a helyén
            this.txtStatus.Location = new System.Drawing.Point(15, 630);
            this.txtStatus.Multiline = true;
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtStatus.Size = new System.Drawing.Size(760, 80);
            this.txtStatus.Anchor = (System.Windows.Forms.AnchorStyles.Bottom
                                   | System.Windows.Forms.AnchorStyles.Left
                                   | System.Windows.Forms.AnchorStyles.Right);

            // --- FORM BEÁLLÍTÁSOK ---
            this.ClientSize = new System.Drawing.Size(800, 760);
            this.MinimumSize = new System.Drawing.Size(800, 760);
            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                lblServerLoad, cmbServerLoad, lblLoadAuth, txtLoadAuth, lblLoadSession, txtLoadSession,
                lblLoadId, txtLoadId, btnLoad,
                lblServerSave, cmbServerSave, lblSaveAuth, txtSaveAuth, lblSaveSession, txtSaveSession,
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
        private System.Windows.Forms.Label lblLoadAuth;
        private System.Windows.Forms.TextBox txtLoadAuth;
        private System.Windows.Forms.Label lblLoadSession;
        private System.Windows.Forms.TextBox txtLoadSession;
        private System.Windows.Forms.Label lblLoadId;
        private System.Windows.Forms.TextBox txtLoadId;
        private System.Windows.Forms.Button btnLoad;

        private System.Windows.Forms.Label lblServerSave;
        private System.Windows.Forms.ComboBox cmbServerSave;
        private System.Windows.Forms.Label lblSaveAuth;
        private System.Windows.Forms.TextBox txtSaveAuth;
        private System.Windows.Forms.Label lblSaveSession;
        private System.Windows.Forms.TextBox txtSaveSession;
        private System.Windows.Forms.Label lblSaveName;
        private System.Windows.Forms.TextBox txtSaveName;
        private System.Windows.Forms.Button btnSave;

        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.TextBox txtJson;
    }
}
