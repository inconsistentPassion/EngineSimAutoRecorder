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
            btnOpenOutput = new Button();
            btnBrowseOutput = new Button();
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
            lblCarName = new Label();
            txtCarName = new TextBox();
            lblPrefix = new Label();
            txtPrefix = new TextBox();
            btnPreset1k = new Button();
            btnPreset2k = new Button();
            btnPreset3k = new Button();
            btnPreset4k = new Button();
            btnPreset5k = new Button();
            btnPreset6k = new Button();
            btnPreset7k = new Button();
            btnPreset8k = new Button();
            btnSortRpm = new Button();
            btnClearRpm = new Button();
            ctxRpm = new ContextMenuStrip();
            ctxRpmRemove = new ToolStripMenuItem();
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
            grpCapture.Controls.Add(lblPrefix);
            grpCapture.Controls.Add(txtPrefix);
            grpCapture.Controls.Add(lblCarName);
            grpCapture.Controls.Add(txtCarName);
            grpCapture.Controls.Add(btnOpenOutput);
            grpCapture.Controls.Add(btnBrowseOutput);
            grpCapture.Controls.Add(txtOutputDir);
            grpCapture.Controls.Add(lblOutputDir);
            grpCapture.Location = new Point(17, 180);
            grpCapture.Margin = new Padding(4, 5, 4, 5);
            grpCapture.Name = "grpCapture";
            grpCapture.Padding = new Padding(4, 5, 4, 5);
            grpCapture.Size = new Size(800, 180);
            grpCapture.TabIndex = 2;
            grpCapture.TabStop = false;
            grpCapture.Text = "Output";
            // 
            // btnOpenOutput
            // 
            btnOpenOutput.Font = new Font("Segoe UI", 12F);
            btnOpenOutput.Location = new Point(720, 33);
            btnOpenOutput.Margin = new Padding(4, 5, 4, 5);
            btnOpenOutput.Name = "btnOpenOutput";
            btnOpenOutput.Size = new Size(60, 42);
            btnOpenOutput.TabIndex = 3;
            btnOpenOutput.Text = "📂";
            btnOpenOutput.Click += btnOpenOutput_Click;
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
            // txtOutputDir
            // 
            txtOutputDir.Location = new Point(114, 37);
            txtOutputDir.Margin = new Padding(4, 5, 4, 5);
            txtOutputDir.Name = "txtOutputDir";
            txtOutputDir.Size = new Size(478, 31);
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
            // lblCarName
            // 
            lblCarName.AutoSize = true;
            lblCarName.Location = new Point(9, 82);
            lblCarName.Margin = new Padding(4, 0, 4, 0);
            lblCarName.Name = "lblCarName";
            lblCarName.Size = new Size(96, 25);
            lblCarName.TabIndex = 5;
            lblCarName.Text = "Car Name:";
            // 
            // txtCarName
            // 
            txtCarName.Location = new Point(114, 79);
            txtCarName.Margin = new Padding(4, 5, 4, 5);
            txtCarName.Name = "txtCarName";
            txtCarName.Size = new Size(478, 31);
            txtCarName.TabIndex = 4;
            txtCarName.PlaceholderText = "e.g. supra_mk4";
            // 
            // lblPrefix
            // 
            lblPrefix.AutoSize = true;
            lblPrefix.Location = new Point(9, 122);
            lblPrefix.Margin = new Padding(4, 0, 4, 0);
            lblPrefix.Name = "lblPrefix";
            lblPrefix.Size = new Size(64, 25);
            lblPrefix.TabIndex = 7;
            lblPrefix.Text = "Prefix:";
            // 
            // txtPrefix
            // 
            txtPrefix.Location = new Point(114, 119);
            txtPrefix.Margin = new Padding(4, 5, 4, 5);
            txtPrefix.Name = "txtPrefix";
            txtPrefix.Size = new Size(478, 31);
            txtPrefix.TabIndex = 6;
            txtPrefix.PlaceholderText = "e.g. ext_ (prepended to filename)";
            // 
            // ctxRpm
            // 
            ctxRpm.Items.AddRange(new ToolStripItem[] { ctxRpmRemove });
            ctxRpm.Name = "ctxRpm";
            ctxRpm.Size = new Size(158, 34);
            // 
            // ctxRpmRemove
            // 
            ctxRpmRemove.Name = "ctxRpmRemove";
            ctxRpmRemove.Size = new Size(157, 34);
            ctxRpmRemove.Text = "Remove";
            ctxRpmRemove.Click += btnRemoveRpm_Click;
            // 
            // grpRpm
            // 
            grpRpm.Controls.Add(btnPreset1k);
            grpRpm.Controls.Add(btnPreset2k);
            grpRpm.Controls.Add(btnPreset3k);
            grpRpm.Controls.Add(btnPreset4k);
            grpRpm.Controls.Add(btnPreset5k);
            grpRpm.Controls.Add(btnPreset6k);
            grpRpm.Controls.Add(btnPreset7k);
            grpRpm.Controls.Add(btnPreset8k);
            grpRpm.Controls.Add(numRpmList);
            grpRpm.Controls.Add(lstTargetRpms);
            grpRpm.Controls.Add(btnRemoveRpm);
            grpRpm.Controls.Add(btnAddRpm);
            grpRpm.Controls.Add(btnSortRpm);
            grpRpm.Controls.Add(btnClearRpm);
            grpRpm.Controls.Add(lblTargetRpms);
            grpRpm.Location = new Point(17, 373);
            grpRpm.Margin = new Padding(4, 5, 4, 5);
            grpRpm.Name = "grpRpm";
            grpRpm.Padding = new Padding(4, 5, 4, 5);
            grpRpm.Size = new Size(800, 240);
            grpRpm.TabIndex = 3;
            grpRpm.TabStop = false;
            grpRpm.Text = "RPM Targets";
            // 
            // btnPreset1k
            // 
            btnPreset1k.Location = new Point(9, 33);
            btnPreset1k.Margin = new Padding(4, 5, 4, 5);
            btnPreset1k.Name = "btnPreset1k";
            btnPreset1k.Size = new Size(64, 35);
            btnPreset1k.TabIndex = 10;
            btnPreset1k.Text = "1K";
            btnPreset1k.Click += btnPreset_Click;
            // 
            // btnPreset2k
            // 
            btnPreset2k.Location = new Point(79, 33);
            btnPreset2k.Margin = new Padding(4, 5, 4, 5);
            btnPreset2k.Name = "btnPreset2k";
            btnPreset2k.Size = new Size(64, 35);
            btnPreset2k.TabIndex = 11;
            btnPreset2k.Text = "2K";
            btnPreset2k.Click += btnPreset_Click;
            // 
            // btnPreset3k
            // 
            btnPreset3k.Location = new Point(149, 33);
            btnPreset3k.Margin = new Padding(4, 5, 4, 5);
            btnPreset3k.Name = "btnPreset3k";
            btnPreset3k.Size = new Size(64, 35);
            btnPreset3k.TabIndex = 12;
            btnPreset3k.Text = "3K";
            btnPreset3k.Click += btnPreset_Click;
            // 
            // btnPreset4k
            // 
            btnPreset4k.Location = new Point(219, 33);
            btnPreset4k.Margin = new Padding(4, 5, 4, 5);
            btnPreset4k.Name = "btnPreset4k";
            btnPreset4k.Size = new Size(64, 35);
            btnPreset4k.TabIndex = 13;
            btnPreset4k.Text = "4K";
            btnPreset4k.Click += btnPreset_Click;
            // 
            // btnPreset5k
            // 
            btnPreset5k.Location = new Point(289, 33);
            btnPreset5k.Margin = new Padding(4, 5, 4, 5);
            btnPreset5k.Name = "btnPreset5k";
            btnPreset5k.Size = new Size(64, 35);
            btnPreset5k.TabIndex = 14;
            btnPreset5k.Text = "5K";
            btnPreset5k.Click += btnPreset_Click;
            // 
            // btnPreset6k
            // 
            btnPreset6k.Location = new Point(359, 33);
            btnPreset6k.Margin = new Padding(4, 5, 4, 5);
            btnPreset6k.Name = "btnPreset6k";
            btnPreset6k.Size = new Size(64, 35);
            btnPreset6k.TabIndex = 15;
            btnPreset6k.Text = "6K";
            btnPreset6k.Click += btnPreset_Click;
            // 
            // btnPreset7k
            // 
            btnPreset7k.Location = new Point(429, 33);
            btnPreset7k.Margin = new Padding(4, 5, 4, 5);
            btnPreset7k.Name = "btnPreset7k";
            btnPreset7k.Size = new Size(64, 35);
            btnPreset7k.TabIndex = 16;
            btnPreset7k.Text = "7K";
            btnPreset7k.Click += btnPreset_Click;
            // 
            // btnPreset8k
            // 
            btnPreset8k.Location = new Point(499, 33);
            btnPreset8k.Margin = new Padding(4, 5, 4, 5);
            btnPreset8k.Name = "btnPreset8k";
            btnPreset8k.Size = new Size(64, 35);
            btnPreset8k.TabIndex = 17;
            btnPreset8k.Text = "8K";
            btnPreset8k.Click += btnPreset_Click;
            // 
            // numRpmList
            // 
            numRpmList.Increment = new decimal(new int[] { 500, 0, 0, 0 });
            numRpmList.Location = new Point(510, 82);
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
            lstTargetRpms.ContextMenuStrip = ctxRpm;
            lstTargetRpms.ItemHeight = 25;
            lstTargetRpms.Location = new Point(9, 80);
            lstTargetRpms.Margin = new Padding(4, 5, 4, 5);
            lstTargetRpms.Name = "lstTargetRpms";
            lstTargetRpms.Size = new Size(490, 129);
            lstTargetRpms.TabIndex = 1;
            // 
            // btnAddRpm
            // 
            btnAddRpm.Location = new Point(649, 75);
            btnAddRpm.Margin = new Padding(4, 5, 4, 5);
            btnAddRpm.Name = "btnAddRpm";
            btnAddRpm.Size = new Size(70, 42);
            btnAddRpm.TabIndex = 3;
            btnAddRpm.Text = "Add";
            btnAddRpm.Click += btnAddRpm_Click;
            // 
            // btnRemoveRpm
            // 
            btnRemoveRpm.Location = new Point(725, 75);
            btnRemoveRpm.Margin = new Padding(4, 5, 4, 5);
            btnRemoveRpm.Name = "btnRemoveRpm";
            btnRemoveRpm.Size = new Size(65, 42);
            btnRemoveRpm.TabIndex = 2;
            btnRemoveRpm.Text = "Del";
            btnRemoveRpm.Click += btnRemoveRpm_Click;
            // 
            // btnSortRpm
            // 
            btnSortRpm.Location = new Point(510, 125);
            btnSortRpm.Margin = new Padding(4, 5, 4, 5);
            btnSortRpm.Name = "btnSortRpm";
            btnSortRpm.Size = new Size(129, 42);
            btnSortRpm.TabIndex = 5;
            btnSortRpm.Text = "Sort ↑";
            btnSortRpm.Click += btnSortRpm_Click;
            // 
            // btnClearRpm
            // 
            btnClearRpm.Location = new Point(649, 125);
            btnClearRpm.Margin = new Padding(4, 5, 4, 5);
            btnClearRpm.Name = "btnClearRpm";
            btnClearRpm.Size = new Size(141, 42);
            btnClearRpm.TabIndex = 6;
            btnClearRpm.Text = "Clear All";
            btnClearRpm.Click += btnClearRpm_Click;
            // 
            // lblTargetRpms
            // 
            lblTargetRpms.AutoSize = true;
            lblTargetRpms.Location = new Point(9, 37);
            lblTargetRpms.Margin = new Padding(4, 0, 4, 0);
            lblTargetRpms.Name = "lblTargetRpms";
            lblTargetRpms.Size = new Size(0, 25);
            lblTargetRpms.TabIndex = 4;
            // 
            // grpStatus
            // 
            grpStatus.Controls.Add(pbarProgress);
            grpStatus.Controls.Add(lblCurrentRpm);
            grpStatus.Controls.Add(lblStatus);
            grpStatus.Location = new Point(17, 626);
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
            txtLog.Location = new Point(17, 760);
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
            btnStart.Location = new Point(17, 1026);
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
            btnStop.Location = new Point(303, 1026);
            btnStop.Margin = new Padding(4, 5, 4, 5);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(264, 60);
            btnStop.TabIndex = 0;
            btnStop.Text = "■  Stop";
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Click += btnStop_Click;
            // 
            // chkStayOnTop
            // 
            chkStayOnTop.AutoSize = true;
            chkStayOnTop.Location = new Point(686, 1057);
            chkStayOnTop.Margin = new Padding(4, 5, 4, 5);
            chkStayOnTop.Name = "chkStayOnTop";
            chkStayOnTop.Size = new Size(131, 29);
            chkStayOnTop.TabIndex = 6;
            chkStayOnTop.Text = "Stay on Top";
            chkStayOnTop.UseVisualStyleBackColor = true;
            chkStayOnTop.CheckedChanged += chkStayOnTop_CheckedChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(834, 1106);
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
        private System.Windows.Forms.Label lblCarName;
        private System.Windows.Forms.TextBox txtCarName;
        private System.Windows.Forms.Label lblPrefix;
        private System.Windows.Forms.TextBox txtPrefix;
        private System.Windows.Forms.Button btnPreset1k;
        private System.Windows.Forms.Button btnPreset2k;
        private System.Windows.Forms.Button btnPreset3k;
        private System.Windows.Forms.Button btnPreset4k;
        private System.Windows.Forms.Button btnPreset5k;
        private System.Windows.Forms.Button btnPreset6k;
        private System.Windows.Forms.Button btnPreset7k;
        private System.Windows.Forms.Button btnPreset8k;
        private System.Windows.Forms.Button btnSortRpm;
        private System.Windows.Forms.Button btnClearRpm;
        private System.Windows.Forms.ContextMenuStrip ctxRpm;
        private System.Windows.Forms.ToolStripMenuItem ctxRpmRemove;
    }
}
