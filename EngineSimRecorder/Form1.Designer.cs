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
            lblTitle = new Label();
            grpProcess = new GroupBox();
            btnRefresh = new Button();
            cmbProcess = new ComboBox();
            lblProcess = new Label();
            grpCapture = new GroupBox();
            btnBrowseOutput = new Button();
            btnOpenOutput = new Button();
            txtOutputDir = new TextBox();
            lblOutputDir = new Label();
            grpRpm = new GroupBox();
            numRpmList = new NumericUpDown();
            lstTargetRpms = new ListBox();
            btnRemoveRpm = new Button();
            btnAddRpm = new Button();
            lblTargetRpms = new Label();
            grpStatus = new GroupBox();
            pbarProgress = new ProgressBar();
            lblCurrentRpm = new Label();
            lblStatus = new Label();
            txtLog = new TextBox();
            btnStart = new Button();
            btnStop = new Button();
            chkStayOnTop = new CheckBox();
            grpProcess.SuspendLayout();
            grpCapture.SuspendLayout();
            grpRpm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numRpmList).BeginInit();
            grpStatus.SuspendLayout();
            SuspendLayout();
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblTitle.Location = new Point(13, 9);
            lblTitle.Margin = new Padding(4, 0, 4, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(443, 38);
            lblTitle.TabIndex = 5;
            lblTitle.Text = "Engine Simulator Auto-Recorder";
            // 
            // grpProcess
            // 
            grpProcess.Controls.Add(btnRefresh);
            grpProcess.Controls.Add(cmbProcess);
            grpProcess.Controls.Add(lblProcess);
            grpProcess.Location = new Point(17, 67);
            grpProcess.Margin = new Padding(4, 5, 4, 5);
            grpProcess.Name = "grpProcess";
            grpProcess.Padding = new Padding(4, 5, 4, 5);
            grpProcess.Size = new Size(800, 100);
            grpProcess.TabIndex = 1;
            grpProcess.TabStop = false;
            grpProcess.Text = "Engine Simulator Process";
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(671, 33);
            btnRefresh.Margin = new Padding(4, 5, 4, 5);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(107, 42);
            btnRefresh.TabIndex = 0;
            btnRefresh.Text = "Refresh";
            btnRefresh.Click += btnRefresh_Click;
            // 
            // cmbProcess
            // 
            cmbProcess.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProcess.Location = new Point(100, 35);
            cmbProcess.Margin = new Padding(4, 5, 4, 5);
            cmbProcess.Name = "cmbProcess";
            cmbProcess.Size = new Size(555, 33);
            cmbProcess.TabIndex = 1;
            // 
            // lblProcess
            // 
            lblProcess.AutoSize = true;
            lblProcess.Location = new Point(9, 40);
            lblProcess.Margin = new Padding(4, 0, 4, 0);
            lblProcess.Name = "lblProcess";
            lblProcess.Size = new Size(76, 25);
            lblProcess.TabIndex = 2;
            lblProcess.Text = "Process:";
            // 
            // grpCapture
            // 
            grpCapture.Controls.Add(btnOpenOutput);
            grpCapture.Controls.Add(btnBrowseOutput);
            grpCapture.Controls.Add(txtOutputDir);
            grpCapture.Controls.Add(lblOutputDir);
            grpCapture.Location = new Point(17, 180);
            grpCapture.Margin = new Padding(4, 5, 4, 5);
            grpCapture.Name = "grpCapture";
            grpCapture.Padding = new Padding(4, 5, 4, 5);
            grpCapture.Size = new Size(800, 100);
            grpCapture.TabIndex = 2;
            grpCapture.TabStop = false;
            grpCapture.Text = "Output";
            // 
            // btnBrowseOutput
            // 
            btnBrowseOutput.Location = new Point(600, 33);
            btnBrowseOutput.Margin = new Padding(4, 5, 4, 5);
            btnBrowseOutput.Name = "btnBrowseOutput";
            btnBrowseOutput.Size = new Size(107, 42);
            btnBrowseOutput.TabIndex = 0;
            btnBrowseOutput.Text = "Browse…";
            btnBrowseOutput.Click += btnBrowseOutput_Click;
            // 
            // btnOpenOutput
            // 
            btnOpenOutput.Location = new Point(720, 33);
            btnOpenOutput.Margin = new Padding(4, 5, 4, 5);
            btnOpenOutput.Name = "btnOpenOutput";
            btnOpenOutput.Size = new Size(60, 42);
            btnOpenOutput.TabIndex = 3;
            btnOpenOutput.Text = "📂";
            btnOpenOutput.Font = new Font("Segoe UI", 12F);
            btnOpenOutput.Click += btnOpenOutput_Click;
            // 
            // txtOutputDir
            // 
            txtOutputDir.Location = new Point(114, 35);
            txtOutputDir.Margin = new Padding(4, 5, 4, 5);
            txtOutputDir.Name = "txtOutputDir";
            txtOutputDir.Size = new Size(541, 31);
            txtOutputDir.TabIndex = 1;
            txtOutputDir.Text = "recordings";
            // 
            // lblOutputDir
            // 
            lblOutputDir.AutoSize = true;
            lblOutputDir.Location = new Point(9, 40);
            lblOutputDir.Margin = new Padding(4, 0, 4, 0);
            lblOutputDir.Name = "lblOutputDir";
            lblOutputDir.Size = new Size(101, 25);
            lblOutputDir.TabIndex = 2;
            lblOutputDir.Text = "Output Dir:";
            // 
            // grpRpm
            // 
            grpRpm.Controls.Add(numRpmList);
            grpRpm.Controls.Add(lstTargetRpms);
            grpRpm.Controls.Add(btnRemoveRpm);
            grpRpm.Controls.Add(btnAddRpm);
            grpRpm.Controls.Add(lblTargetRpms);
            grpRpm.Location = new Point(17, 293);
            grpRpm.Margin = new Padding(4, 5, 4, 5);
            grpRpm.Name = "grpRpm";
            grpRpm.Padding = new Padding(4, 5, 4, 5);
            grpRpm.Size = new Size(800, 167);
            grpRpm.TabIndex = 3;
            grpRpm.TabStop = false;
            grpRpm.Text = "RPM Targets";
            // 
            // numRpmList
            // 
            numRpmList.Increment = new decimal(new int[] { 500, 0, 0, 0 });
            numRpmList.Location = new Point(286, 67);
            numRpmList.Margin = new Padding(4, 5, 4, 5);
            numRpmList.Maximum = new decimal(new int[] { 30000, 0, 0, 0 });
            numRpmList.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            numRpmList.Name = "numRpmList";
            numRpmList.Size = new Size(129, 31);
            numRpmList.TabIndex = 0;
            numRpmList.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            // 
            // lstTargetRpms
            // 
            lstTargetRpms.ItemHeight = 25;
            lstTargetRpms.Location = new Point(9, 67);
            lstTargetRpms.Margin = new Padding(4, 5, 4, 5);
            lstTargetRpms.Name = "lstTargetRpms";
            lstTargetRpms.Size = new Size(255, 79);
            lstTargetRpms.TabIndex = 1;
            // 
            // btnRemoveRpm
            // 
            btnRemoveRpm.Location = new Point(529, 67);
            btnRemoveRpm.Margin = new Padding(4, 5, 4, 5);
            btnRemoveRpm.Name = "btnRemoveRpm";
            btnRemoveRpm.Size = new Size(100, 42);
            btnRemoveRpm.TabIndex = 2;
            btnRemoveRpm.Text = "Remove";
            btnRemoveRpm.Click += btnRemoveRpm_Click;
            // 
            // btnAddRpm
            // 
            btnAddRpm.Location = new Point(429, 67);
            btnAddRpm.Margin = new Padding(4, 5, 4, 5);
            btnAddRpm.Name = "btnAddRpm";
            btnAddRpm.Size = new Size(86, 42);
            btnAddRpm.TabIndex = 3;
            btnAddRpm.Text = "Add";
            btnAddRpm.Click += btnAddRpm_Click;
            // 
            // lblTargetRpms
            // 
            lblTargetRpms.AutoSize = true;
            lblTargetRpms.Location = new Point(9, 37);
            lblTargetRpms.Margin = new Padding(4, 0, 4, 0);
            lblTargetRpms.Name = "lblTargetRpms";
            lblTargetRpms.Size = new Size(114, 25);
            lblTargetRpms.TabIndex = 4;
            lblTargetRpms.Text = "Target RPMs:";
            // 
            // grpStatus
            // 
            grpStatus.Controls.Add(pbarProgress);
            grpStatus.Controls.Add(lblCurrentRpm);
            grpStatus.Controls.Add(lblStatus);
            grpStatus.Location = new Point(17, 473);
            grpStatus.Margin = new Padding(4, 5, 4, 5);
            grpStatus.Name = "grpStatus";
            grpStatus.Padding = new Padding(4, 5, 4, 5);
            grpStatus.Size = new Size(800, 117);
            grpStatus.TabIndex = 4;
            grpStatus.TabStop = false;
            grpStatus.Text = "Status";
            // 
            // pbarProgress
            // 
            pbarProgress.Location = new Point(9, 70);
            pbarProgress.Margin = new Padding(4, 5, 4, 5);
            pbarProgress.Name = "pbarProgress";
            pbarProgress.Size = new Size(783, 33);
            pbarProgress.TabIndex = 0;
            // 
            // lblCurrentRpm
            // 
            lblCurrentRpm.AutoSize = true;
            lblCurrentRpm.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCurrentRpm.Location = new Point(571, 37);
            lblCurrentRpm.Margin = new Padding(4, 0, 4, 0);
            lblCurrentRpm.Name = "lblCurrentRpm";
            lblCurrentRpm.Size = new Size(83, 25);
            lblCurrentRpm.TabIndex = 1;
            lblCurrentRpm.Text = "RPM: ---";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(9, 37);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(53, 25);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Idle...";
            // 
            // txtLog
            // 
            txtLog.BackColor = Color.Black;
            txtLog.Font = new Font("Consolas", 8.25F);
            txtLog.ForeColor = Color.LimeGreen;
            txtLog.Location = new Point(17, 607);
            txtLog.Margin = new Padding(4, 5, 4, 5);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(798, 247);
            txtLog.TabIndex = 2;
            // 
            // btnStart
            // 
            btnStart.BackColor = Color.ForestGreen;
            btnStart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.Location = new Point(17, 873);
            btnStart.Margin = new Padding(4, 5, 4, 5);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(264, 60);
            btnStart.TabIndex = 1;
            btnStart.Text = "▶  Start";
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.BackColor = Color.Firebrick;
            btnStop.Enabled = false;
            btnStop.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnStop.ForeColor = Color.White;
            btnStop.Location = new Point(546, 873);
            btnStop.Margin = new Padding(4, 5, 4, 5);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(249, 60);
            btnStop.TabIndex = 0;
            btnStop.Text = "■  Stop";
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Click += btnStop_Click;
            // 
            // chkStayOnTop
            // 
            chkStayOnTop.AutoSize = true;
            chkStayOnTop.Location = new Point(300, 885);
            chkStayOnTop.Margin = new Padding(4, 5, 4, 5);
            chkStayOnTop.Name = "chkStayOnTop";
            chkStayOnTop.Size = new Size(212, 29);
            chkStayOnTop.TabIndex = 6;
            chkStayOnTop.Text = "Stay on Top";
            chkStayOnTop.UseVisualStyleBackColor = true;
            chkStayOnTop.CheckedChanged += chkStayOnTop_CheckedChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(834, 953);
            Controls.Add(chkStayOnTop);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Controls.Add(txtLog);
            Controls.Add(grpStatus);
            Controls.Add(grpRpm);
            Controls.Add(grpCapture);
            Controls.Add(grpProcess);
            Controls.Add(lblTitle);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4, 5, 4, 5);
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Engine Simulator Auto-Recorder";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            grpProcess.ResumeLayout(false);
            grpProcess.PerformLayout();
            grpCapture.ResumeLayout(false);
            grpCapture.PerformLayout();
            grpRpm.ResumeLayout(false);
            grpRpm.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numRpmList).EndInit();
            grpStatus.ResumeLayout(false);
            grpStatus.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox grpProcess;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.ComboBox cmbProcess;
        private System.Windows.Forms.Label lblProcess;
        private System.Windows.Forms.GroupBox grpCapture;
        private System.Windows.Forms.Button btnBrowseOutput;
        private System.Windows.Forms.Button btnOpenOutput;
        private System.Windows.Forms.TextBox txtOutputDir;
        private System.Windows.Forms.Label lblOutputDir;
        private System.Windows.Forms.GroupBox grpRpm;
        private System.Windows.Forms.NumericUpDown numRpmList;
        private System.Windows.Forms.ListBox lstTargetRpms;
        private System.Windows.Forms.Button btnRemoveRpm;
        private System.Windows.Forms.Button btnAddRpm;
        private System.Windows.Forms.Label lblTargetRpms;
        private System.Windows.Forms.GroupBox grpStatus;
        private System.Windows.Forms.ProgressBar pbarProgress;
        private System.Windows.Forms.Label lblCurrentRpm;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.CheckBox chkStayOnTop;
    }
}
