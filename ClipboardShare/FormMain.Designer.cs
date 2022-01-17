namespace ClipboardShare
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.btnMonitor = new System.Windows.Forms.Button();
            this.cbxWithoutSync = new System.Windows.Forms.CheckBox();
            this.labState = new System.Windows.Forms.Label();
            this.tbxGroupId = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.stripExit = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.stripExit.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnMonitor
            // 
            this.btnMonitor.Location = new System.Drawing.Point(11, 79);
            this.btnMonitor.Name = "btnMonitor";
            this.btnMonitor.Size = new System.Drawing.Size(91, 27);
            this.btnMonitor.TabIndex = 0;
            this.btnMonitor.Text = "启动同步";
            this.btnMonitor.UseVisualStyleBackColor = true;
            this.btnMonitor.Click += new System.EventHandler(this.btnMonitor_Click);
            // 
            // cbxWithoutSync
            // 
            this.cbxWithoutSync.AutoSize = true;
            this.cbxWithoutSync.Location = new System.Drawing.Point(108, 83);
            this.cbxWithoutSync.Name = "cbxWithoutSync";
            this.cbxWithoutSync.Size = new System.Drawing.Size(149, 19);
            this.cbxWithoutSync.TabIndex = 1;
            this.cbxWithoutSync.Text = "本机不同步剪贴板";
            this.cbxWithoutSync.UseVisualStyleBackColor = true;
            // 
            // labState
            // 
            this.labState.AutoSize = true;
            this.labState.ForeColor = System.Drawing.Color.Red;
            this.labState.Location = new System.Drawing.Point(82, 9);
            this.labState.Name = "labState";
            this.labState.Size = new System.Drawing.Size(105, 15);
            this.labState.TabIndex = 2;
            this.labState.Text = "● 未启动同步";
            // 
            // tbxGroupId
            // 
            this.tbxGroupId.Location = new System.Drawing.Point(108, 40);
            this.tbxGroupId.Name = "tbxGroupId";
            this.tbxGroupId.Size = new System.Drawing.Size(140, 25);
            this.tbxGroupId.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(64, 43);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(38, 15);
            this.label1.TabIndex = 4;
            this.label1.Text = "组id";
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.stripExit;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseDoubleClick);
            // 
            // stripExit
            // 
            this.stripExit.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.stripExit.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuExit});
            this.stripExit.Name = "stripExit";
            this.stripExit.Size = new System.Drawing.Size(109, 28);
            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            this.menuExit.Size = new System.Drawing.Size(108, 24);
            this.menuExit.Text = "退出";
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(268, 115);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tbxGroupId);
            this.Controls.Add(this.labState);
            this.Controls.Add(this.cbxWithoutSync);
            this.Controls.Add(this.btnMonitor);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "剪贴板同步";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.stripExit.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnMonitor;
        private System.Windows.Forms.CheckBox cbxWithoutSync;
        private System.Windows.Forms.Label labState;
        private System.Windows.Forms.TextBox tbxGroupId;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip stripExit;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
    }
}