namespace AtomicFramework
{
    partial class PluginControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            PluginName = new Label();
            PluginVersion = new Label();
            Enable = new CheckBox();
            Error = new Label();
            SuspendLayout();
            // 
            // PluginName
            // 
            PluginName.AutoSize = true;
            PluginName.Font = new Font("Segoe UI", 14F);
            PluginName.ForeColor = SystemColors.HighlightText;
            PluginName.Location = new Point(0, 0);
            PluginName.Name = "PluginName";
            PluginName.Size = new Size(62, 25);
            PluginName.TabIndex = 0;
            PluginName.Text = "Name";
            // 
            // PluginVersion
            // 
            PluginVersion.AutoSize = true;
            PluginVersion.Font = new Font("Segoe UI", 12F);
            PluginVersion.ForeColor = SystemColors.ControlLight;
            PluginVersion.Location = new Point(0, 25);
            PluginVersion.Name = "PluginVersion";
            PluginVersion.Size = new Size(62, 21);
            PluginVersion.TabIndex = 1;
            PluginVersion.Text = "Version";
            // 
            // Enable
            // 
            Enable.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Enable.AutoSize = true;
            Enable.Checked = true;
            Enable.CheckState = CheckState.Checked;
            Enable.Font = new Font("Segoe UI", 12F);
            Enable.ForeColor = SystemColors.ControlLight;
            Enable.Location = new Point(277, 0);
            Enable.Name = "Enable";
            Enable.RightToLeft = RightToLeft.Yes;
            Enable.Size = new Size(75, 25);
            Enable.TabIndex = 2;
            Enable.Text = "Enable";
            Enable.UseVisualStyleBackColor = true;
            Enable.CheckedChanged += Enable_CheckedChanged;
            // 
            // Error
            // 
            Error.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Error.AutoSize = true;
            Error.Font = new Font("Segoe UI", 14F);
            Error.ForeColor = Color.Salmon;
            Error.Location = new Point(0, 45);
            Error.Name = "Error";
            Error.Size = new Size(54, 25);
            Error.TabIndex = 3;
            Error.Text = "Error";
            Error.TextAlign = ContentAlignment.BottomLeft;
            Error.Visible = false;
            // 
            // PluginControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.ControlDarkDark;
            Controls.Add(Error);
            Controls.Add(Enable);
            Controls.Add(PluginVersion);
            Controls.Add(PluginName);
            Margin = new Padding(0, 0, 0, 3);
            Name = "PluginControl";
            Size = new Size(352, 70);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label PluginName;
        private Label PluginVersion;
        private CheckBox Enable;
        private Label Error;
    }
}
