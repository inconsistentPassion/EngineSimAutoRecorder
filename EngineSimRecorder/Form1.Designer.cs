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
            this.grpProcess = new System.Windows.Forms.GroupBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.cmbProcess = new System.Windows.Forms.ComboBox();
            this.lblProcess = new System.Windows.Forms.Label();
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

            this.grpProcess.SuspendLayout();
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
            // grpProcess
            //
            this.grpProcess.Controls.Add(this.btnRefresh);
            this.grpProcess.Controls.Add(this.cmbProcess);
            this.grpProcess.Controls.Add(this.lblProcess);
            this.grpProcess.Location = new System.Drawing.Point(12, 40);
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
            this.lblProcess.Name = "lblProcess";
            this.lblProcess.Size = new System.Drawing.Size(52, 15);
            this.lblProcess.Text = "Process:";
            //
            // cmbProcess
            //
            this.cmbProcess.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProcess.Location = new System.Drawing.Point(70, 21);
            this.cmbProcess.Name = "cmbProcess";
            this.cmbProcess.Size = new System.Drawing.Size(390, 23);
            //
            // btnRefresh
            //
            this.btnRefresh.Location = new System.Drawing.Point(470, 20);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(75, 25);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            //
            // grpCapture
            //
            this.grpCapture.Controls.Add(this.btnBrowseOutput);
            this.grpCapture.Controls.Add(this.txtOutputDir);
            this.grpCapture.Controls.Add(this.lblOutputDir);
            this.grpCapture.Location = new System.Drawing.Point(12, 108);
            this.grpCapture.Name = "grpCapture";
            this.grpCapture.Size = new System.Drawing.Size(560, 60);
            this.grpCapture.TabIndex = 2;
            this.grpCapture.TabStop = false;
            this.grpCapture.Text = "Output";
            //
            // lblOutputDir
            //
            this.lblOutputDir.AutoSize = true;
            this.lblOutputDir.Location = new System.Drawing.Point(6, 24);
            this.lblOutputDir.Name = "lblOutputDir";
            this.lblOutputDir.Size = new System.Drawing.Size(68, 15);
            this.lblOutputDir.Text = "Output Dir:";
            //
            // txtOutputDir
            //
            this.txtOutputDir.Location = new System.Drawing.Point(80, 21);
            this.txtOutputDir.Name = "txtOutputDir";
            this.txtOutputDir.Size = new System.Drawing.Size(380, 23);
            this.txtOutputDir.Text = "recordings";
            //
            // btnBrowseOutput
            //
            this.btnBrowseOutput.Location = new System.Drawing.Point(470, 20);
            this.btnBrowseOutput.Name = "btnBrowseOutput";
            this.btnBrowseOutput.Size = new System.Drawing.Size(75, 25);
            this.btnBrowseOutput.Text = "Browse\u2026";
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
            this.grpRpm.Location = new System.Drawing.Point(12, 176);
            this.grpRpm.Name = "grpRpm";
            this.grpRpm.Size = new System.Drawing.Size(560, 155);
            this.grpRpm.TabIndex = 3;
            this.grpRpm.TabStop = false;
            this.grpRpm.Text = "RPM Targets";
            //
            // lblTargetRpms
            //
            this.lblTargetRpms.AutoSize = true;
            this.lblTargetRpms.Location = new System.Drawing.Point(6, 22);
            this.lblTargetRpms.Name = "lblTargetRpms";
            this.lblTargetRpms.Size = new System.Drawing.Size(72, 15);
            this.lblTargetRpms.Text = "Target RPMs:";
            //
            // lstTargetRpms
            //
            this.lstTargetRpms.ItemHeight = 15;
            this.lstTargetRpms.Location = new System.Drawing.Point(6, 40);
            this.lstTargetRpms.Name = "lstTargetRpms";
            this.lstTargetRpms.Size = new System.Drawing.Size(180, 109);
            //
            // numRpmList
            //
            this.numRpmList.Increment = 500;
            this.numRpmList.Location = new System.Drawing.Point(200, 40);
            this.numRpmList.Maximum = 30000;
            this.numRpmList.Minimum = 100;
            this.numRpmList.Name = "numRpmList";
            this.numRpmList.Size = new System.Drawing.Size(90, 23);
            this.numRpmList.Value = 1000;
            //
            // btnAddRpm
            //
            this.btnAddRpm.Location = new System.Drawing.Point(300, 40);
            this.btnAddRpm.Name = "btnAddRpm";
            this.btnAddRpm.Size = new System.Drawing.Size(60, 25);
            this.btnAddRpm.Text = "Add";
            this.btnAddRpm.Click += new System.EventHandler(this.btnAddRpm_Click);
            //
            // btnRemoveRpm
            //
            this.btnRemoveRpm.Location = new System.Drawing.Point(370, 40);
            this.btnRemoveRpm.Name = "btnRemoveRpm";
            this.btnRemoveRpm.Size = new System.Drawing.Size(70, 25);
            this.btnRemoveRpm.Text = "Remove";
            this.btnRemoveRpm.Click += new System.EventHandler(this.btnRemoveRpm_Click);
            //
            // lblRpmTol
            //
            this.lblRpmTol.AutoSize = true;
            this.lblRpmTol.Location = new System.Drawing.Point(200, 80);
            this.lblRpmTol.Name = "lblRpmTol";
            this.lblRpmTol.Size = new System.Drawing.Size(88, 15);
            this.lblRpmTol.Text = "RPM Tolerance:";
            //
            // numRpmTol
            //
            this.numRpmTol.Increment = 10;
            this.numRpmTol.Location = new System.Drawing.Point(310, 77);
            this.numRpmTol.Maximum = 1000;
            this.numRpmTol.Minimum = 1;
            this.numRpmTol.Name = "numRpmTol";
            this.numRpmTol.Size = new System.Drawing.Size(70, 23);
            this.numRpmTol.Value = 50;
            //
            // lblHoldSec
            //
            this.lblHoldSec.AutoSize = true;
            this.lblHoldSec.Location = new System.Drawing.Point(200, 110);
            this.lblHoldSec.Name = "lblHoldSec";
            this.lblHoldSec.Size = new System.Drawing.Size(92, 15);
            this.lblHoldSec.Text = "Hold (seconds):";
            //
            // numHoldSec
            //
            this.numHoldSec.Location = new System.Drawing.Point(310, 107);
            this.numHoldSec.Maximum = 120;
            this.numHoldSec.Minimum = 1;
            this.numHoldSec.Name = "numHoldSec";
            this.numHoldSec.Size = new System.Drawing.Size(70, 23);
            this.numHoldSec.Value = 3;
            //
            // lblRecordSec
            //
            this.lblRecordSec.AutoSize = true;
            this.lblRecordSec.Location = new System.Drawing.Point(400, 80);
            this.lblRecordSec.Name = "lblRecordSec";
            this.lblRecordSec.Size = new System.Drawing.Size(103, 15);
            this.lblRecordSec.Text = "Record (seconds):";
            //
            // numRecordSec
            //
            this.numRecordSec.Location = new System.Drawing.Point(400, 100);
            this.numRecordSec.Maximum = 300;
            this.numRecordSec.Minimum = 1;
            this.numRecordSec.Name = "numRecordSec";
            this.numRecordSec.Size = new System.Drawing.Size(70, 23);
            this.numRecordSec.Value = 6;
            //
            // grpPid
            //
            this.grpPid.Controls.Add(this.numKd);
            this.grpPid.Controls.Add(this.numKi);
            this.grpPid.Controls.Add(this.numKp);
            this.grpPid.Controls.Add(this.lblKd);
            this.grpPid.Controls.Add(this.lblKi);
            this.grpPid.Controls.Add(this.lblKp);
            this.grpPid.Location = new System.Drawing.Point(12, 340);
            this.grpPid.Name = "grpPid";
            this.grpPid.Size = new System.Drawing.Size(560, 60);
            this.grpPid.TabIndex = 4;
            this.grpPid.TabStop = false;
            this.grpPid.Text = "PID Controller";
            //
            // lblKp
            //
            this.lblKp.AutoSize = true;
            this.lblKp.Location = new System.Drawing.Point(6, 27);
            this.lblKp.Name = "lblKp";
            this.lblKp.Size = new System.Drawing.Size(20, 15);
            this.lblKp.Text = "Kp";
            //
            // numKp
            //
            this.numKp.DecimalPlaces = 4;
            this.numKp.Increment = 0.01m;
            this.numKp.Location = new System.Drawing.Point(28, 24);
            this.numKp.Maximum = 10;
            this.numKp.Name = "numKp";
            this.numKp.Size = new System.Drawing.Size(100, 23);
            this.numKp.Value = 0.005m;
            //
            // lblKi
            //
            this.lblKi.AutoSize = true;
            this.lblKi.Location = new System.Drawing.Point(148, 27);
            this.lblKi.Name = "lblKi";
            this.lblKi.Size = new System.Drawing.Size(16, 15);
            this.lblKi.Text = "Ki";
            //
            // numKi
            //
            this.numKi.DecimalPlaces = 5;
            this.numKi.Increment = 0.001m;
            this.numKi.Location = new System.Drawing.Point(170, 24);
            this.numKi.Maximum = 1;
            this.numKi.Name = "numKi";
            this.numKi.Size = new System.Drawing.Size(100, 23);
            this.numKi.Value = 0.0001m;
            //
            // lblKd
            //
            this.lblKd.AutoSize = true;
            this.lblKd.Location = new System.Drawing.Point(290, 27);
            this.lblKd.Name = "lblKd";
            this.lblKd.Size = new System.Drawing.Size(20, 15);
            this.lblKd.Text = "Kd";
            //
            // numKd
            //
            this.numKd.DecimalPlaces = 4;
            this.numKd.Increment = 0.01m;
            this.numKd.Location = new System.Drawing.Point(312, 24);
            this.numKd.Maximum = 5;
            this.numKd.Name = "numKd";
            this.numKd.Size = new System.Drawing.Size(100, 23);
            this.numKd.Value = 0.005m;
            //
            // grpStatus
            //
            this.grpStatus.Controls.Add(this.pbarProgress);
            this.grpStatus.Controls.Add(this.lblCurrentRpm);
            this.grpStatus.Controls.Add(this.lblStatus);
            this.grpStatus.Location = new System.Drawing.Point(12, 410);
            this.grpStatus.Name = "grpStatus";
            this.grpStatus.Size = new System.Drawing.Size(560, 70);
            this.grpStatus.TabIndex = 5;
            this.grpStatus.TabStop = false;
            this.grpStatus.Text = "Status";
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(6, 22);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(43, 15);
            this.lblStatus.Text = "Idle...";
            //
            // lblCurrentRpm
            //
            this.lblCurrentRpm.AutoSize = true;
            this.lblCurrentRpm.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCurrentRpm.Location = new System.Drawing.Point(400, 22);
            this.lblCurrentRpm.Name = "lblCurrentRpm";
            this.lblCurrentRpm.Size = new System.Drawing.Size(60, 15);
            this.lblCurrentRpm.Text = "RPM: ---";
            //
            // pbarProgress
            //
            this.pbarProgress.Location = new System.Drawing.Point(6, 42);
            this.pbarProgress.Name = "pbarProgress";
            this.pbarProgress.Size = new System.Drawing.Size(548, 20);
            //
            // txtLog
            //
            this.txtLog.BackColor = System.Drawing.Color.Black;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.txtLog.ForeColor = System.Drawing.Color.LimeGreen;
            this.txtLog.Location = new System.Drawing.Point(12, 490);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(560, 120);
            //
            // btnStart
            //
            this.btnStart.BackColor = System.Drawing.Color.ForestGreen;
            this.btnStart.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.ForeColor = System.Drawing.Color.White;
            this.btnStart.Location = new System.Drawing.Point(12, 620);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(120, 36);
            this.btnStart.Text = "\u25B6  Start";
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            //
            // btnStop
            //
            this.btnStop.BackColor = System.Drawing.Color.Firebrick;
            this.btnStop.Enabled = false;
            this.btnStop.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnStop.ForeColor = System.Drawing.Color.White;
            this.btnStop.Location = new System.Drawing.Point(145, 620);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(120, 36);
            this.btnStop.Text = "\u25A0  Stop";
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 670);
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.btnStop, this.btnStart, this.txtLog, this.grpStatus,
                this.grpPid, this.grpRpm, this.grpCapture,
                this.grpProcess, this.lblTitle
            });
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Engine Simulator Auto-Recorder";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);

            this.grpProcess.ResumeLayout(false); this.grpProcess.PerformLayout();
            this.grpCapture.ResumeLayout(false); this.grpCapture.PerformLayout();
            this.grpRpm.ResumeLayout(false); this.grpRpm.PerformLayout();
            this.grpPid.ResumeLayout(false); this.grpPid.PerformLayout();
            this.grpStatus.ResumeLayout(false); this.grpStatus.PerformLayout();
            this.ResumeLayout(false); this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox grpProcess;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.ComboBox cmbProcess;
        private System.Windows.Forms.Label lblProcess;
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
