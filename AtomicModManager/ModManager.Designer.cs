using AtomicFramework;

namespace AtomicFramework
{
    partial class ModManager
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            PluginList = new FlowLayoutPanel();
            SuspendLayout();
            // 
            // PluginList
            // 
            PluginList.BackColor = SystemColors.ControlDark;
            PluginList.Dock = DockStyle.Fill;
            PluginList.FlowDirection = FlowDirection.TopDown;
            PluginList.Location = new Point(0, 0);
            PluginList.Margin = new Padding(0);
            PluginList.Name = "PluginList";
            PluginList.Size = new Size(800, 450);
            PluginList.TabIndex = 0;
            PluginList.WrapContents = false;
            PluginList.SizeChanged += PluginList_SizeChanged;
            // 
            // ModManager
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.ControlDark;
            ClientSize = new Size(800, 450);
            Controls.Add(PluginList);
            Name = "ModManager";
            ShowIcon = false;
            Text = "AtomicFramework ModManager";
            FormClosed += ModManager_FormClosed;
            ResumeLayout(false);
        }

        #endregion

        private FlowLayoutPanel PluginList;
    }
}
