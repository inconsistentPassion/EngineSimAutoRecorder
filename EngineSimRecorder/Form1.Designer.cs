namespace EngineSimRecorder
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.grpMode = new System.Windows.Forms.GroupBox();
            this.rbOcr = new System.Windows.Forms.RadioButton();
            this.rbInjection = new System.Windows.Forms.RadioButton();
            this.grpProcess = new System.Windows.Forms.GroupBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.cmbProcess = new System.Windows.Forms.ComboBox();
            this.lblProcess = new System.Windows.Forms.Label();
            this.grpOcrRegion = new System.Windows.Forms.GroupBox();
            this.lblOcrHint = new System.Windows.Forms.Label();
            this.numOcrH = new System.Windows.Forms.NumericUpDown();
            this.numOcrW = new System.Windows.Forms.NumericUpDown();
            this.numOcrY = new System.Windows.Forms.NumericUpDown();
            this.numOcrX = new System.Windows.Forms.NumericUpDown();
            this.lblOcrH = new System.Windows.Forms.Label();
            this.lblOcrW = new System.Windows.Forms.Label();
            this.lblOcrY = new System.Windows.Forms.Label();
            this.lblOcrX = new System.Windows.Forms.Label();
            this.grpCapture = new System.Windows.Forms.GroupBox();
            this.btnBrowseOutput = new System.Windows.Forms.Button();
            this.txtOutputDir = new System.Windows.Forms.TextBox();
            this.lblOutputDir = new System.Windows.Forms.Label();
            this.grpRpm = new System.Windows.Forms.GroupBox();
            this.numRpmList = new System.Windows.Forms.NumericUpDown();
            this.lstTargetRpms = new System.Windows.Forms.ListBox();
            this.btnRemoveRpm = new System.Windows.Forms.Button();
            this.btnAddRpm = new System.Windows.Forms.Button();
            this.lblTargetRpms = new System.Windows.Forms.Label();
            this.numRecordSec = new System.Windows.Forms.NumericUpDown();
            this.lblRecordSec = new System.Windows.Forms.Label();
            this.numHoldSec = new System.Windows.Forms.NumericUpDown();
            this.lblHoldSec = new System.Windows.Forms.Label();
            this.numRpmTol = new System.Windows.Forms.NumericUpDown();
            this.lblRpmTol = new System.Windows.Forms.Label();
            this.grpPid = new System.Windows.Forms.GroupBox();
            this.numKd = new System.Windows.Forms.NumericUpDown();
            this.numKi = new System.Windows.Forms.NumericUpDown();
            this.numKp = new System.Windows.Forms.NumericUpDown();
            this.lblKd = new System.Windows.Forms.Label();
            this.lblKi = new System.Windows.Forms.Label();
            this.lblKp = new System.Windows.Forms.Label();
            this.grpStatus = new System.Windows.Forms.GroupBox();
            this.pbarProgress = new System.Windows.Forms.ProgressBar();
            this.lblCurrentRpm = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();

            this.grpMode.SuspendLayout();
            this.grpProcess.SuspendLayout();
            this.grpOcrRegion.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numOcrH)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numOcrW)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numOcrY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numOcrX)).BeginInit();
            this.grpCapture.SuspendLayout();
            this.grpRpm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRpmList)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRecordSec)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHoldSec)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRpmTol)).BeginInit();
            this.grpPid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numKd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numKi)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numKp)).BeginInit();
            this.grpStatus.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(12, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(283, 25);
            this.lblTitle.Text = "Engine Simulator Auto-Recorder";
            // 
            // grpMode
            // 
            this.grpMode.Controls.Add(this.rbOcr);
            this.grpMode.Controls.Add(this.rbInjection);
            this.grpMode.Location = new System.Drawing.Point(12, 40);
            this.grpMode.Name = "grpMode";
            this.grpMode.Size = new System.Drawing.Size(560, 50);
            this.grpMode.TabIndex = 10;
            this.grpMode.TabStop = false;
            this.grpMode.Text = "Mode";
            // 
            // rbInjection
            // 
            this.rbInjection.AutoSize = true;
            this.rbInjection.Checked = true;
            this.rbInjection.Location = new System.Drawing.Point(20, 22);
            this.rbInjection.Name = "rbInjection";
            this.rbInjection.Size = new System.Drawing.Size(200, 19);
            this.rbInjection.Text = "Injection (DLL + Memory) — precise";
            this.rbInjection.TabIndex = 0;
            // 
            // rbOcr
            // 
            this.rbOcr.AutoSize = true;
            this.rbOcr.Location = new System.Drawing.Point(280, 22);
            this.rbOcr.Name = "rbOcr";
            this.rbOcr.Size = new System.Drawing.Size(220, 19);
            this.rbOcr.Text = "OCR (PaddleOCR + Keys) — no admin";
            this.rbOcr.TabIndex = 1;
            // 
            // grpProcess
            // 
            this.grpProcess.Controls.Add(this.btnRefresh);
            this.grpProcess.Controls.Add(this.cmbProcess);
            this.grpProcess.Controls.Add(this.lblProcess);
            this.grpProcess.Location = new System.Drawing.Point(12, 98);
            this.grpProcess.Name = "grpProcess";
            this.grpProcess.Size = new System.Drawing.Size(560, 60);
            this.grpProcess.TabIndex = 1;
            this.grpProcess.TabStop = false;
            this.grpProcess.Text = "Engine Simulator Process";
            // 
            // lblProcess
            // 
            this.lblProcess.AutoSize = true;
            this.lblProcess.Location = new System.Drawing.Point(6, 24);
            this.lblProcess.Size = new System.Drawing.Size(52, 15);
            this.lblProcess.Text = "Process:";
            // 
            // cmbProcess
            // 
            this.cmbProcess.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProcess.Location = new System.Drawing.Point(70, 21);
            this.cmbProcess.Size = new System.Drawing.Size(390, 23);
            this.cmbProcess.Name = "cmbProcess";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(470, 20);
            this.btnRefresh.Size = new System.Drawing.Size(75, 25);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // grpOcrRegion
            // 
            this.grpOcrRegion.Controls.Add(this.lblOcrHint);
            this.grpOcrRegion.Controls.Add(this.numOcrH);
            this.grpOcrRegion.Controls.Add(this.numOcrW);
            this.grpOcrRegion.Controls.Add(this.numOcrY);
            this.grpOcrRegion.Controls.Add(this.numOcrX);
            this.grpOcrRegion.Controls.Add(this.lblOcrH);
            this.grpOcrRegion.Controls.Add(this.lblOcrW);
            this.grpOcrRegion.Controls.Add(this.lblOcrY);
            this.grpOcrRegion.Controls.Add(this.lblOcrX);
            this.grpOcrRegion.Location = new System.Drawing.Point(12, 98);
            this.grpOcrRegion.Name = "grpOcrRegion";
            this.grpOcrRegion.Size = new System.Drawing.Size(560, 80);
            this.grpOcrRegion.TabIndex = 11;
            this.grpOcrRegion.TabStop = false;
            this.grpOcrRegion.Text = "OCR Region (RPM display, pixels)";
            this.grpOcrRegion.Visible = false;
            // 
            // lblOcrX, numOcrX, etc.
            // 
            this.lblOcrX.AutoSize = true; this.lblOcrX.Location = new System.Drawing.Point(6, 28); this.lblOcrX.Text = "X";
            this.numOcrX.Location = new System.Drawing.Point(24, 25); this.numOcrX.Size = new System.Drawing.Size(80, 23); this.numOcrX.Maximum = 7680; this.numOcrX.Value = 860;
            this.lblOcrY.AutoSize = true; this.lblOcrY.Location = new System.Drawing.Point(115, 28); this.lblOcrY.Text = "Y";
            this.numOcrY.Location = new System.Drawing.Point(133, 25); this.numOcrY.Size = new System.Drawing.Size(80, 23); this.numOcrY.Maximum = 4320; this.numOcrY.Value = 45;
            this.lblOcrW.AutoSize = true; this.lblOcrW.Location = new System.Drawing.Point(224, 28); this.lblOcrW.Text = "W";
            this.numOcrW.Location = new System.Drawing.Point(245, 25); this.numOcrW.Size = new System.Drawing.Size(80, 23); this.numOcrW.Maximum = 7680; this.numOcrW.Minimum = 10; this.numOcrW.Value = 160;
            this.lblOcrH.AutoSize = true; this.lblOcrH.Location = new System.Drawing.Point(336, 28); this.lblOcrH.Text = "H";
            this.numOcrH.Location = new System.Drawing.Point(356, 25); this.numOcrH.Size = new System.Drawing.Size(80, 23); this.numOcrH.Maximum = 4320; this.numOcrH.Minimum = 10; this.numOcrH.Value = 40;
            this.lblOcrHint.AutoSize = true; this.lblOcrHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblOcrHint.Location = new System.Drawing.Point(6, 55); this.lblOcrHint.Text = "Use a screen ruler to find the RPM digits bounding box.";
            // 
            // grpCapture
            // 
            this.grpCapture.Controls.Add(this.btnBrowseOutput);
            this.grpCapture.Controls.Add(this.txtOutputDir);
            this.grpCapture.Controls.Add(this.lblOutputDir);
            this.grpCapture.Location = new System.Drawing.Point(12, 166);
            this.grpCapture.Name = "grpCapture";
            this.grpCapture.Size = new System.Drawing.Size(560, 60);
            this.grpCapture.TabIndex = 2;
            this.grpCapture.TabStop = false;
            this.grpCapture.Text = "Output";
            this.lblOutputDir.AutoSize = true; this.lblOutputDir.Location = new System.Drawing.Point(6, 24); this.lblOutputDir.Text = "Output Dir:";
            this.txtOutputDir.Location = new System.Drawing.Point(80, 21); this.txtOutputDir.Size = new System.Drawing.Size(380, 23); this.txtOutputDir.Text = "recordings";
            this.btnBrowseOutput.Location = new System.Drawing.Point(470, 20); this.btnBrowseOutput.Size = new System.Drawing.Size(75, 25); this.btnBrowseOutput.Text = "Browse…";
            this.btnBrowseOutput.Click += new System.EventHandler(this.btnBrowseOutput_Click);
            // 
            // grpRpm
            // 
            this.grpRpm.Controls.Add(this.numRpmList);
            this.grpRpm.Controls.Add(this.lstTargetRpms);
            this.grpRpm.Controls.Add(this.btnRemoveRpm);
            this.grpRpm.Controls.Add(this.btnAddRpm);
            this.grpRpm.Controls.Add(this.lblTargetRpms);
            this.grpRpm.Controls.Add(this.numRecordSec);
            this.grpRpm.Controls.Add(this.lblRecordSec);
            this.grpRpm.Controls.Add(this.numHoldSec);
            this.grpRpm.Controls.Add(this.lblHoldSec);
            this.grpRpm.Controls.Add(this.numRpmTol);
            this.grpRpm.Controls.Add(this.lblRpmTol);
            this.grpRpm.Location = new System.Drawing.Point(12, 234);
            this.grpRpm.Name = "grpRpm";
            this.grpRpm.Size = new System.Drawing.Size(560, 155);
            this.grpRpm.TabIndex = 3;
            this.grpRpm.TabStop = false;
            this.grpRpm.Text = "RPM Targets";
            this.lblTargetRpms.AutoSize = true; this.lblTargetRpms.Location = new System.Drawing.Point(6, 22); this.lblTargetRpms.Text = "Target RPMs:";
            this.lstTargetRpms.Location = new System.Drawing.Point(6, 40); this.lstTargetRpms.Size = new System.Drawing.Size(180, 109);
            this.numRpmList.Location = new System.Drawing.Point(200, 40); this.numRpmList.Size = new System.Drawing.Size(90, 23); this.numRpmList.Maximum = 30000; this.numRpmList.Minimum = 100; this.numRpmList.Value = 1000; this.numRpmList.Increment = 500;
            this.btnAddRpm.Location = new System.Drawing.Point(300, 40); this.btnAddRpm.Size = new System.Drawing.Size(60, 25); this.btnAddRpm.Text = "Add";
            this.btnAddRpm.Click += new System.EventHandler(this.btnAddRpm_Click);
            this.btnRemoveRpm.Location = new System.Drawing.Point(370, 40); this.btnRemoveRpm.Size = new System.Drawing.Size(70, 25); this.btnRemoveRpm.Text = "Remove";
            this.btnRemoveRpm.Click += new System.EventHandler(this.btnRemoveRpm_Click);
            this.lblRpmTol.AutoSize = true; this.lblRpmTol.Location = new System.Drawing.Point(200, 80); this.lblRpmTol.Text = "RPM Tolerance:";
            this.numRpmTol.Location = new System.Drawing.Point(310, 77); this.numRpmTol.Size = new System.Drawing.Size(70, 23); this.numRpmTol.Maximum = 1000; this.numRpmTol.Minimum = 1; this.numRpmTol.Value = 50; this.numRpmTol.Increment = 10;
            this.lblHoldSec.AutoSize = true; this.lblHoldSec.Location = new System.Drawing.Point(200, 110); this.lblHoldSec.Text = "Hold (seconds):";
            this.numHoldSec.Location = new System.Drawing.Point(310, 107); this.numHoldSec.Size = new System.Drawing.Size(70, 23); this.numHoldSec.Maximum = 120; this.numHoldSec.Minimum = 1; this.numHoldSec.Value = 3;
            this.lblRecordSec.AutoSize = true; this.lblRecordSec.Location = new System.Drawing.Point(400, 80); this.lblRecordSec.Text = "Record (seconds):";
            this.numRecordSec.Location = new System.Drawing.Point(400, 100); this.numRecordSec.Size = new System.Drawing.Size(70, 23); this.numRecordSec.Maximum = 300; this.numRecordSec.Minimum = 1; this.numRecordSec.Value = 6;
            // 
            // grpPid
            // 
            this.grpPid.Controls.Add(this.numKd);
            this.grpPid.Controls.Add(this.numKi);
            this.grpPid.Controls.Add(this.numKp);
            this.grpPid.Controls.Add(this.lblKd);
            this.grpPid.Controls.Add(this.lblKi);
            this.grpPid.Controls.Add(this.lblKp);
            this.grpPid.Location = new System.Drawing.Point(12, 398);
            this.grpPid.Name = "grpPid";
            this.grpPid.Size = new System.Drawing.Size(560, 60);
            this.grpPid.TabIndex = 4;
            this.grpPid.TabStop = false;
            this.grpPid.Text = "PID Controller";
            this.lblKp.AutoSize = true; this.lblKp.Location = new System.Drawing.Point(6, 27); this.lblKp.Text = "Kp";
            this.numKp.DecimalPlaces = 4; this.numKp.Increment = 0.01m; this.numKp.Location = new System.Drawing.Point(28, 24); this.numKp.Size = new System.Drawing.Size(100, 23); this.numKp.Maximum = 10; this.numKp.Value = 0.005m;
            this.lblKi.AutoSize = true; this.lblKi.Location = new System.Drawing.Point(148, 27); this.lblKi.Text = "Ki";
            this.numKi.DecimalPlaces = 5; this.numKi.Increment = 0.001m; this.numKi.Location = new System.Drawing.Point(170, 24); this.numKi.Size = new System.Drawing.Size(100, 23); this.numKi.Maximum = 1; this.numKi.Value = 0.0001m;
            this.lblKd.AutoSize = true; this.lblKd.Location = new System.Drawing.Point(290, 27); this.lblKd.Text = "Kd";
            this.numKd.DecimalPlaces = 4; this.numKd.Increment = 0.01m; this.numKd.Location = new System.Drawing.Point(312, 24); this.numKd.Size = new System.Drawing.Size(100, 23); this.numKd.Maximum = 5; this.numKd.Value = 0.005m;
            // 
            // grpStatus
            // 
            this.grpStatus.Controls.Add(this.pbarProgress);
            this.grpStatus.Controls.Add(this.lblCurrentRpm);
            this.grpStatus.Controls.Add(this.lblStatus);
            this.grpStatus.Location = new System.Drawing.Point(12, 468);
            this.grpStatus.Name = "grpStatus";
            this.grpStatus.Size = new System.Drawing.Size(560, 70);
            this.grpStatus.TabIndex = 5;
            this.grpStatus.TabStop = false;
            this.grpStatus.Text = "Status";
            this.lblStatus.AutoSize = true; this.lblStatus.Location = new System.Drawing.Point(6, 22); this.lblStatus.Text = "Idle...";
            this.lblCurrentRpm.AutoSize = true; this.lblCurrentRpm.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold); this.lblCurrentRpm.Location = new System.Drawing.Point(400, 22); this.lblCurrentRpm.Text = "RPM: ---";
            this.pbarProgress.Location = new System.Drawing.Point(6, 42); this.pbarProgress.Size = new System.Drawing.Size(548, 20);
            // 
            // txtLog
            // 
            this.txtLog.BackColor = System.Drawing.Color.Black; this.txtLog.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.txtLog.ForeColor = System.Drawing.Color.LimeGreen; this.txtLog.Location = new System.Drawing.Point(12, 548);
            this.txtLog.Multiline = true; this.txtLog.ReadOnly = true; this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(560, 120); this.txtLog.Name = "txtLog";
            // 
            // btnStart
            // 
            this.btnStart.BackColor = System.Drawing.Color.ForestGreen; this.btnStart.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.ForeColor = System.Drawing.Color.White; this.btnStart.Location = new System.Drawing.Point(12, 678);
            this.btnStart.Size = new System.Drawing.Size(120, 36); this.btnStart.Text = "▶  Start";
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.BackColor = System.Drawing.Color.Firebrick; this.btnStop.Enabled = false;
            this.btnStop.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnStop.ForeColor = System.Drawing.Color.White; this.btnStop.Location = new System.Drawing.Point(145, 678);
            this.btnStop.Size = new System.Drawing.Size(120, 36); this.btnStop.Text = "■  Stop";
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 726);
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.btnStop, this.btnStart, this.txtLog, this.grpStatus,
                this.grpPid, this.grpRpm, this.grpCapture,
                this.grpOcrRegion, this.grpProcess, this.grpMode, this.lblTitle
            });
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Engine Simulator Auto-Recorder";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);

            this.grpMode.ResumeLayout(false); this.grpMode.PerformLayout();
            this.grpProcess.ResumeLayout(false); this.grpProcess.PerformLayout();
            this.grpOcrRegion.ResumeLayout(false); this.grpOcrRegion.PerformLayout();
            this.grpCapture.ResumeLayout(false); this.grpCapture.PerformLayout();
            this.grpRpm.ResumeLayout(false); this.grpRpm.PerformLayout();
            this.grpPid.ResumeLayout(false); this.grpPid.PerformLayout();
            this.grpStatus.ResumeLayout(false); this.grpStatus.PerformLayout();
            this.ResumeLayout(false); this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox grpMode;
        private System.Windows.Forms.RadioButton rbInjection;
        private System.Windows.Forms.RadioButton rbOcr;
        private System.Windows.Forms.GroupBox grpProcess;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.ComboBox cmbProcess;
        private System.Windows.Forms.Label lblProcess;
        private System.Windows.Forms.GroupBox grpOcrRegion;
        private System.Windows.Forms.Label lblOcrHint;
        private System.Windows.Forms.NumericUpDown numOcrH;
        private System.Windows.Forms.NumericUpDown numOcrW;
        private System.Windows.Forms.NumericUpDown numOcrY;
        private System.Windows.Forms.NumericUpDown numOcrX;
        private System.Windows.Forms.Label lblOcrH;
        private System.Windows.Forms.Label lblOcrW;
        private System.Windows.Forms.Label lblOcrY;
        private System.Windows.Forms.Label lblOcrX;
        private System.Windows.Forms.GroupBox grpCapture;
        private System.Windows.Forms.Button btnBrowseOutput;
        private System.Windows.Forms.TextBox txtOutputDir;
        private System.Windows.Forms.Label lblOutputDir;
        private System.Windows.Forms.GroupBox grpRpm;
        private System.Windows.Forms.NumericUpDown numRpmList;
        private System.Windows.Forms.ListBox lstTargetRpms;
        private System.Windows.Forms.Button btnRemoveRpm;
        private System.Windows.Forms.Button btnAddRpm;
        private System.Windows.Forms.Label lblTargetRpms;
        private System.Windows.Forms.NumericUpDown numRecordSec;
        private System.Windows.Forms.Label lblRecordSec;
        private System.Windows.Forms.NumericUpDown numHoldSec;
        private System.Windows.Forms.Label lblHoldSec;
        private System.Windows.Forms.NumericUpDown numRpmTol;
        private System.Windows.Forms.Label lblRpmTol;
        private System.Windows.Forms.GroupBox grpPid;
        private System.Windows.Forms.NumericUpDown numKd;
        private System.Windows.Forms.NumericUpDown numKi;
        private System.Windows.Forms.NumericUpDown numKp;
        private System.Windows.Forms.Label lblKd;
        private System.Windows.Forms.Label lblKi;
        private System.Windows.Forms.Label lblKp;
        private System.Windows.Forms.GroupBox grpStatus;
        private System.Windows.Forms.ProgressBar pbarProgress;
        private System.Windows.Forms.Label lblCurrentRpm;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
    }
}
