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
            components = new System.ComponentModel.Container();
            tabMain = new TabControl();
            tabRecorder = new TabPage();
            lblTitle = new Label();
            grpProcess = new GroupBox();
            btnRefresh = new Button();
            cmbProcess = new ComboBox();
            lblProcess = new Label();
            grpCapture = new GroupBox();
            lblPrefix = new Label();
            txtPrefix = new TextBox();
            lblCarName = new Label();
            txtCarName = new TextBox();
            btnOpenOutput = new Button();
            btnBrowseOutput = new Button();
            txtOutputDir = new TextBox();
            lblOutputDir = new Label();
            grpRpm = new GroupBox();
            btnPreset1k = new Button();
            btnPreset2k = new Button();
            btnPreset3k = new Button();
            btnPreset4k = new Button();
            btnPreset5k = new Button();
            btnPreset6k = new Button();
            btnPreset7k = new Button();
            btnPreset8k = new Button();
            numRpmList = new NumericUpDown();
            lstTargetRpms = new ListBox();
            ctxRpm = new ContextMenuStrip(components);
            ctxRpmRemove = new ToolStripMenuItem();
            btnRemoveRpm = new Button();
            btnAddRpm = new Button();
            btnEditRpm = new Button();
            btnSortRpm = new Button();
            btnClearRpm = new Button();
            lblTargetRpms = new Label();
            grpStatus = new GroupBox();
            pbarProgress = new ProgressBar();
            lblCurrentRpm = new Label();
            lblStatus = new Label();
            txtLog = new TextBox();
            btnStart = new Button();
            btnStop = new Button();
            chkStayOnTop = new CheckBox();
            tabSettings = new TabPage();
            grpAudio = new GroupBox();
            lblSampleRate = new Label();
            cmbSampleRate = new ComboBox();
            lblChannels = new Label();
            cmbChannels = new ComboBox();
            tabMain.SuspendLayout();
            tabRecorder.SuspendLayout();
            grpProcess.SuspendLayout();
            grpCapture.SuspendLayout();
            grpRpm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numRpmList).BeginInit();
            ctxRpm.SuspendLayout();
            grpStatus.SuspendLayout();
            tabSettings.SuspendLayout();
            grpAudio.SuspendLayout();
            SuspendLayout();
            // 
            // tabMain
            // 
            tabMain.Controls.Add(tabRecorder);
            tabMain.Controls.Add(tabSettings);
            tabMain.Location = new Point(0, 0);
            tabMain.Margin = new Padding(2, 2, 2, 2);
            tabMain.Name = "tabMain";
            tabMain.SelectedIndex = 0;
            tabMain.Size = new Size(667, 823);
            tabMain.TabIndex = 0;
            // 
            // tabRecorder
            // 
            tabRecorder.Controls.Add(lblTitle);
            tabRecorder.Controls.Add(grpProcess);
            tabRecorder.Controls.Add(grpCapture);
            tabRecorder.Controls.Add(grpRpm);
            tabRecorder.Controls.Add(grpStatus);
            tabRecorder.Controls.Add(txtLog);
            tabRecorder.Controls.Add(btnStart);
            tabRecorder.Controls.Add(btnStop);
            tabRecorder.Controls.Add(chkStayOnTop);
            tabRecorder.Location = new Point(4, 29);
            tabRecorder.Margin = new Padding(2, 2, 2, 2);
            tabRecorder.Name = "tabRecorder";
            tabRecorder.Padding = new Padding(2, 2, 2, 2);
            tabRecorder.Size = new Size(659, 790);
            tabRecorder.TabIndex = 0;
            tabRecorder.Text = "Recorder";
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblTitle.Location = new Point(10, 7);
            lblTitle.Margin = new Padding(2, 0, 2, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(386, 32);
            lblTitle.TabIndex = 5;
            lblTitle.Text = "Engine Simulator Auto-Recorder";
            // 
            // grpProcess
            // 
            grpProcess.Controls.Add(btnRefresh);
            grpProcess.Controls.Add(cmbProcess);
            grpProcess.Controls.Add(lblProcess);
            grpProcess.Location = new Point(14, 44);
            grpProcess.Margin = new Padding(2, 2, 2, 2);
            grpProcess.Name = "grpProcess";
            grpProcess.Padding = new Padding(2, 2, 2, 2);
            grpProcess.Size = new Size(632, 68);
            grpProcess.TabIndex = 1;
            grpProcess.TabStop = false;
            grpProcess.Text = "Engine Simulator Process";
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(537, 24);
            btnRefresh.Margin = new Padding(2, 2, 2, 2);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(86, 30);
            btnRefresh.TabIndex = 0;
            btnRefresh.Text = "Refresh";
            btnRefresh.Click += btnRefresh_Click;
            // 
            // cmbProcess
            // 
            cmbProcess.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProcess.Location = new Point(80, 26);
            cmbProcess.Margin = new Padding(2, 2, 2, 2);
            cmbProcess.Name = "cmbProcess";
            cmbProcess.Size = new Size(445, 28);
            cmbProcess.TabIndex = 1;
            // 
            // lblProcess
            // 
            lblProcess.AutoSize = true;
            lblProcess.Location = new Point(7, 28);
            lblProcess.Margin = new Padding(2, 0, 2, 0);
            lblProcess.Name = "lblProcess";
            lblProcess.Size = new Size(61, 20);
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
            grpCapture.Location = new Point(14, 118);
            grpCapture.Margin = new Padding(2, 2, 2, 2);
            grpCapture.Name = "grpCapture";
            grpCapture.Padding = new Padding(2, 2, 2, 2);
            grpCapture.Size = new Size(632, 132);
            grpCapture.TabIndex = 2;
            grpCapture.TabStop = false;
            grpCapture.Text = "Output";
            // 
            // lblPrefix
            // 
            lblPrefix.AutoSize = true;
            lblPrefix.Location = new Point(7, 94);
            lblPrefix.Margin = new Padding(2, 0, 2, 0);
            lblPrefix.Name = "lblPrefix";
            lblPrefix.Size = new Size(49, 20);
            lblPrefix.TabIndex = 7;
            lblPrefix.Text = "Prefix:";
            // 
            // txtPrefix
            // 
            txtPrefix.Location = new Point(91, 92);
            txtPrefix.Margin = new Padding(2, 2, 2, 2);
            txtPrefix.Name = "txtPrefix";
            txtPrefix.PlaceholderText = "e.g. ext/int";
            txtPrefix.Size = new Size(383, 27);
            txtPrefix.TabIndex = 6;
            // 
            // lblCarName
            // 
            lblCarName.AutoSize = true;
            lblCarName.Location = new Point(7, 62);
            lblCarName.Margin = new Padding(2, 0, 2, 0);
            lblCarName.Name = "lblCarName";
            lblCarName.Size = new Size(78, 20);
            lblCarName.TabIndex = 5;
            lblCarName.Text = "Car Name:";
            // 
            // txtCarName
            // 
            txtCarName.Location = new Point(91, 60);
            txtCarName.Margin = new Padding(2, 2, 2, 2);
            txtCarName.Name = "txtCarName";
            txtCarName.PlaceholderText = "e.g. supra_mk4";
            txtCarName.Size = new Size(383, 27);
            txtCarName.TabIndex = 4;
            // 
            // btnOpenOutput
            // 
            btnOpenOutput.Font = new Font("Segoe UI", 12F);
            btnOpenOutput.Location = new Point(576, 24);
            btnOpenOutput.Margin = new Padding(2, 2, 2, 2);
            btnOpenOutput.Name = "btnOpenOutput";
            btnOpenOutput.Size = new Size(48, 30);
            btnOpenOutput.TabIndex = 3;
            btnOpenOutput.Text = "📂";
            btnOpenOutput.Click += btnOpenOutput_Click;
            // 
            // btnBrowseOutput
            // 
            btnBrowseOutput.Location = new Point(480, 24);
            btnBrowseOutput.Margin = new Padding(2, 2, 2, 2);
            btnBrowseOutput.Name = "btnBrowseOutput";
            btnBrowseOutput.Size = new Size(86, 30);
            btnBrowseOutput.TabIndex = 0;
            btnBrowseOutput.Text = "Browse…";
            btnBrowseOutput.Click += btnBrowseOutput_Click;
            // 
            // txtOutputDir
            // 
            txtOutputDir.Location = new Point(91, 26);
            txtOutputDir.Margin = new Padding(2, 2, 2, 2);
            txtOutputDir.Name = "txtOutputDir";
            txtOutputDir.Size = new Size(383, 27);
            txtOutputDir.TabIndex = 1;
            txtOutputDir.Text = "recordings";
            // 
            // lblOutputDir
            // 
            lblOutputDir.AutoSize = true;
            lblOutputDir.Location = new Point(7, 29);
            lblOutputDir.Margin = new Padding(2, 0, 2, 0);
            lblOutputDir.Name = "lblOutputDir";
            lblOutputDir.Size = new Size(82, 20);
            lblOutputDir.TabIndex = 2;
            lblOutputDir.Text = "Output Dir:";
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
            grpRpm.Controls.Add(btnEditRpm);
            grpRpm.Controls.Add(btnSortRpm);
            grpRpm.Controls.Add(btnClearRpm);
            grpRpm.Controls.Add(lblTargetRpms);
            grpRpm.Location = new Point(14, 256);
            grpRpm.Margin = new Padding(2, 2, 2, 2);
            grpRpm.Name = "grpRpm";
            grpRpm.Padding = new Padding(2, 2, 2, 2);
            grpRpm.Size = new Size(632, 184);
            grpRpm.TabIndex = 3;
            grpRpm.TabStop = false;
            grpRpm.Text = "RPM Targets";
            // 
            // btnPreset1k
            // 
            btnPreset1k.Location = new Point(7, 24);
            btnPreset1k.Margin = new Padding(2, 2, 2, 2);
            btnPreset1k.Name = "btnPreset1k";
            btnPreset1k.Size = new Size(51, 26);
            btnPreset1k.TabIndex = 10;
            btnPreset1k.Text = "1K";
            btnPreset1k.Click += btnPreset_Click;
            // 
            // btnPreset2k
            // 
            btnPreset2k.Location = new Point(63, 24);
            btnPreset2k.Margin = new Padding(2, 2, 2, 2);
            btnPreset2k.Name = "btnPreset2k";
            btnPreset2k.Size = new Size(51, 26);
            btnPreset2k.TabIndex = 11;
            btnPreset2k.Text = "2K";
            btnPreset2k.Click += btnPreset_Click;
            // 
            // btnPreset3k
            // 
            btnPreset3k.Location = new Point(119, 24);
            btnPreset3k.Margin = new Padding(2, 2, 2, 2);
            btnPreset3k.Name = "btnPreset3k";
            btnPreset3k.Size = new Size(51, 26);
            btnPreset3k.TabIndex = 12;
            btnPreset3k.Text = "3K";
            btnPreset3k.Click += btnPreset_Click;
            // 
            // btnPreset4k
            // 
            btnPreset4k.Location = new Point(175, 24);
            btnPreset4k.Margin = new Padding(2, 2, 2, 2);
            btnPreset4k.Name = "btnPreset4k";
            btnPreset4k.Size = new Size(51, 26);
            btnPreset4k.TabIndex = 13;
            btnPreset4k.Text = "4K";
            btnPreset4k.Click += btnPreset_Click;
            // 
            // btnPreset5k
            // 
            btnPreset5k.Location = new Point(231, 24);
            btnPreset5k.Margin = new Padding(2, 2, 2, 2);
            btnPreset5k.Name = "btnPreset5k";
            btnPreset5k.Size = new Size(51, 26);
            btnPreset5k.TabIndex = 14;
            btnPreset5k.Text = "5K";
            btnPreset5k.Click += btnPreset_Click;
            // 
            // btnPreset6k
            // 
            btnPreset6k.Location = new Point(287, 24);
            btnPreset6k.Margin = new Padding(2, 2, 2, 2);
            btnPreset6k.Name = "btnPreset6k";
            btnPreset6k.Size = new Size(51, 26);
            btnPreset6k.TabIndex = 15;
            btnPreset6k.Text = "6K";
            btnPreset6k.Click += btnPreset_Click;
            // 
            // btnPreset7k
            // 
            btnPreset7k.Location = new Point(343, 24);
            btnPreset7k.Margin = new Padding(2, 2, 2, 2);
            btnPreset7k.Name = "btnPreset7k";
            btnPreset7k.Size = new Size(51, 26);
            btnPreset7k.TabIndex = 16;
            btnPreset7k.Text = "7K";
            btnPreset7k.Click += btnPreset_Click;
            // 
            // btnPreset8k
            // 
            btnPreset8k.Location = new Point(399, 24);
            btnPreset8k.Margin = new Padding(2, 2, 2, 2);
            btnPreset8k.Name = "btnPreset8k";
            btnPreset8k.Size = new Size(51, 26);
            btnPreset8k.TabIndex = 17;
            btnPreset8k.Text = "8K";
            btnPreset8k.Click += btnPreset_Click;
            // 
            // numRpmList
            // 
            numRpmList.Increment = new decimal(new int[] { 500, 0, 0, 0 });
            numRpmList.Location = new Point(408, 60);
            numRpmList.Margin = new Padding(2, 2, 2, 2);
            numRpmList.Maximum = new decimal(new int[] { 30000, 0, 0, 0 });
            numRpmList.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            numRpmList.Name = "numRpmList";
            numRpmList.Size = new Size(103, 27);
            numRpmList.TabIndex = 0;
            numRpmList.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            // 
            // lstTargetRpms
            // 
            lstTargetRpms.ContextMenuStrip = ctxRpm;
            lstTargetRpms.Location = new Point(7, 56);
            lstTargetRpms.Margin = new Padding(2, 2, 2, 2);
            lstTargetRpms.Name = "lstTargetRpms";
            lstTargetRpms.Size = new Size(393, 104);
            lstTargetRpms.TabIndex = 1;
            lstTargetRpms.SelectedIndexChanged += lstTargetRpms_SelectedIndexChanged;
            lstTargetRpms.DoubleClick += btnEditRpm_Click;
            // 
            // ctxRpm
            // 
            ctxRpm.ImageScalingSize = new Size(20, 20);
            ctxRpm.Items.AddRange(new ToolStripItem[] { ctxRpmRemove });
            ctxRpm.Name = "ctxRpm";
            ctxRpm.Size = new Size(133, 28);
            // 
            // ctxRpmRemove
            // 
            ctxRpmRemove.Name = "ctxRpmRemove";
            ctxRpmRemove.Size = new Size(132, 24);
            ctxRpmRemove.Text = "Remove";
            ctxRpmRemove.Click += btnRemoveRpm_Click;
            // 
            // btnRemoveRpm
            // 
            btnRemoveRpm.Location = new Point(576, 56);
            btnRemoveRpm.Margin = new Padding(2, 2, 2, 2);
            btnRemoveRpm.Name = "btnRemoveRpm";
            btnRemoveRpm.Size = new Size(48, 30);
            btnRemoveRpm.TabIndex = 2;
            btnRemoveRpm.Text = "Del";
            btnRemoveRpm.Click += btnRemoveRpm_Click;
            // 
            // btnAddRpm
            // 
            btnAddRpm.Location = new Point(519, 56);
            btnAddRpm.Margin = new Padding(2, 2, 2, 2);
            btnAddRpm.Name = "btnAddRpm";
            btnAddRpm.Size = new Size(52, 30);
            btnAddRpm.TabIndex = 3;
            btnAddRpm.Text = "Add";
            btnAddRpm.Click += btnAddRpm_Click;
            // 
            // btnEditRpm
            // 
            btnEditRpm.Location = new Point(519, 92);
            btnEditRpm.Margin = new Padding(2, 2, 2, 2);
            btnEditRpm.Name = "btnEditRpm";
            btnEditRpm.Size = new Size(105, 30);
            btnEditRpm.TabIndex = 18;
            btnEditRpm.Text = "Edit";
            btnEditRpm.Click += btnEditRpm_Click;
            // 
            // btnSortRpm
            // 
            btnSortRpm.Location = new Point(408, 92);
            btnSortRpm.Margin = new Padding(2, 2, 2, 2);
            btnSortRpm.Name = "btnSortRpm";
            btnSortRpm.Size = new Size(103, 30);
            btnSortRpm.TabIndex = 5;
            btnSortRpm.Text = "Sort ↑";
            btnSortRpm.Click += btnSortRpm_Click;
            // 
            // btnClearRpm
            // 
            btnClearRpm.Location = new Point(408, 128);
            btnClearRpm.Margin = new Padding(2, 2, 2, 2);
            btnClearRpm.Name = "btnClearRpm";
            btnClearRpm.Size = new Size(103, 30);
            btnClearRpm.TabIndex = 6;
            btnClearRpm.Text = "Clear All";
            btnClearRpm.Click += btnClearRpm_Click;
            // 
            // lblTargetRpms
            // 
            lblTargetRpms.AutoSize = true;
            lblTargetRpms.Location = new Point(7, 28);
            lblTargetRpms.Margin = new Padding(2, 0, 2, 0);
            lblTargetRpms.Name = "lblTargetRpms";
            lblTargetRpms.Size = new Size(0, 20);
            lblTargetRpms.TabIndex = 4;
            // 
            // grpStatus
            // 
            grpStatus.Controls.Add(pbarProgress);
            grpStatus.Controls.Add(lblCurrentRpm);
            grpStatus.Controls.Add(lblStatus);
            grpStatus.Location = new Point(14, 446);
            grpStatus.Margin = new Padding(2, 2, 2, 2);
            grpStatus.Name = "grpStatus";
            grpStatus.Padding = new Padding(2, 2, 2, 2);
            grpStatus.Size = new Size(632, 80);
            grpStatus.TabIndex = 4;
            grpStatus.TabStop = false;
            grpStatus.Text = "Status";
            // 
            // pbarProgress
            // 
            pbarProgress.Location = new Point(7, 48);
            pbarProgress.Margin = new Padding(2, 2, 2, 2);
            pbarProgress.Name = "pbarProgress";
            pbarProgress.Size = new Size(618, 22);
            pbarProgress.TabIndex = 0;
            // 
            // lblCurrentRpm
            // 
            lblCurrentRpm.AutoSize = true;
            lblCurrentRpm.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCurrentRpm.Location = new Point(457, 24);
            lblCurrentRpm.Margin = new Padding(2, 0, 2, 0);
            lblCurrentRpm.Name = "lblCurrentRpm";
            lblCurrentRpm.Size = new Size(68, 20);
            lblCurrentRpm.TabIndex = 1;
            lblCurrentRpm.Text = "RPM: ---";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(7, 24);
            lblStatus.Margin = new Padding(2, 0, 2, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(43, 20);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Idle...";
            // 
            // txtLog
            // 
            txtLog.BackColor = Color.Black;
            txtLog.Font = new Font("Consolas", 8.25F);
            txtLog.ForeColor = Color.LimeGreen;
            txtLog.Location = new Point(14, 534);
            txtLog.Margin = new Padding(2, 2, 2, 2);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(633, 169);
            txtLog.TabIndex = 2;
            // 
            // btnStart
            // 
            btnStart.BackColor = Color.ForestGreen;
            btnStart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.Location = new Point(14, 712);
            btnStart.Margin = new Padding(2, 2, 2, 2);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(200, 40);
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
            btnStop.Location = new Point(224, 712);
            btnStop.Margin = new Padding(2, 2, 2, 2);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(200, 40);
            btnStop.TabIndex = 0;
            btnStop.Text = "■  Stop";
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Click += btnStop_Click;
            // 
            // chkStayOnTop
            // 
            chkStayOnTop.AutoSize = true;
            chkStayOnTop.Location = new Point(480, 728);
            chkStayOnTop.Margin = new Padding(2, 2, 2, 2);
            chkStayOnTop.Name = "chkStayOnTop";
            chkStayOnTop.Size = new Size(109, 24);
            chkStayOnTop.TabIndex = 6;
            chkStayOnTop.Text = "Stay on Top";
            chkStayOnTop.UseVisualStyleBackColor = false;
            chkStayOnTop.CheckedChanged += chkStayOnTop_CheckedChanged;
            // 
            // tabSettings
            // 
            tabSettings.Controls.Add(grpAudio);
            tabSettings.Location = new Point(4, 29);
            tabSettings.Margin = new Padding(2, 2, 2, 2);
            tabSettings.Name = "tabSettings";
            tabSettings.Padding = new Padding(2, 2, 2, 2);
            tabSettings.Size = new Size(659, 790);
            tabSettings.TabIndex = 1;
            tabSettings.Text = "Settings";
            // 
            // grpAudio
            // 
            grpAudio.Controls.Add(lblSampleRate);
            grpAudio.Controls.Add(cmbSampleRate);
            grpAudio.Controls.Add(lblChannels);
            grpAudio.Controls.Add(cmbChannels);
            grpAudio.Location = new Point(14, 14);
            grpAudio.Margin = new Padding(2, 2, 2, 2);
            grpAudio.Name = "grpAudio";
            grpAudio.Padding = new Padding(2, 2, 2, 2);
            grpAudio.Size = new Size(632, 104);
            grpAudio.TabIndex = 1;
            grpAudio.TabStop = false;
            grpAudio.Text = "Audio Quality";
            // 
            // lblSampleRate
            // 
            lblSampleRate.AutoSize = true;
            lblSampleRate.Location = new Point(16, 32);
            lblSampleRate.Margin = new Padding(2, 0, 2, 0);
            lblSampleRate.Name = "lblSampleRate";
            lblSampleRate.Size = new Size(96, 20);
            lblSampleRate.TabIndex = 0;
            lblSampleRate.Text = "Sample Rate:";
            // 
            // cmbSampleRate
            // 
            cmbSampleRate.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSampleRate.Items.AddRange(new object[] { "44100 Hz", "48000 Hz" });
            cmbSampleRate.Location = new Point(128, 30);
            cmbSampleRate.Margin = new Padding(2, 2, 2, 2);
            cmbSampleRate.Name = "cmbSampleRate";
            cmbSampleRate.Size = new Size(145, 28);
            cmbSampleRate.TabIndex = 1;
            // 
            // lblChannels
            // 
            lblChannels.AutoSize = true;
            lblChannels.Location = new Point(16, 64);
            lblChannels.Margin = new Padding(2, 0, 2, 0);
            lblChannels.Name = "lblChannels";
            lblChannels.Size = new Size(71, 20);
            lblChannels.TabIndex = 2;
            lblChannels.Text = "Channels:";
            // 
            // cmbChannels
            // 
            cmbChannels.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbChannels.Items.AddRange(new object[] { "Stereo", "Mono" });
            cmbChannels.Location = new Point(128, 62);
            cmbChannels.Margin = new Padding(2, 2, 2, 2);
            cmbChannels.Name = "cmbChannels";
            cmbChannels.Size = new Size(145, 28);
            cmbChannels.TabIndex = 3;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(667, 790);
            Controls.Add(tabMain);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(2, 2, 2, 2);
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Engine Simulator Auto-Recorder";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            tabMain.ResumeLayout(false);
            tabRecorder.ResumeLayout(false);
            tabRecorder.PerformLayout();
            grpProcess.ResumeLayout(false);
            grpProcess.PerformLayout();
            grpCapture.ResumeLayout(false);
            grpCapture.PerformLayout();
            grpRpm.ResumeLayout(false);
            grpRpm.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numRpmList).EndInit();
            ctxRpm.ResumeLayout(false);
            grpStatus.ResumeLayout(false);
            grpStatus.PerformLayout();
            tabSettings.ResumeLayout(false);
            grpAudio.ResumeLayout(false);
            grpAudio.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabRecorder;
        private System.Windows.Forms.TabPage tabSettings;
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
        private System.Windows.Forms.Button btnEditRpm;
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
        private System.Windows.Forms.GroupBox grpAudio;
        private System.Windows.Forms.Label lblSampleRate;
        private System.Windows.Forms.ComboBox cmbSampleRate;
        private System.Windows.Forms.Label lblChannels;
        private System.Windows.Forms.ComboBox cmbChannels;
    }
}
