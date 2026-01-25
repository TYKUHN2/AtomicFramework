namespace AtomicFramework
{
    internal partial class PluginControl : UserControl
    {
        internal readonly Plugin data;
        private readonly FlowLayoutPanel list;

        internal PluginControl(FlowLayoutPanel list, Plugin data)
        {
            this.data = data;
            this.list = list;

            InitializeComponent();

            PluginName.Text = data.display_name;
            PluginVersion.Text = data.version.ToString();

            if (data.guid == "BepInEx")
                Enable.Visible = false;

            if (!data.hasBep)
                SetError("BepInEx version incompatible!");
            else if (!data.hasAtomic)
                SetError("AtomicFramework version incompatible!");
            else if (!data.foundDependencies)
                SetError("Dependencies not found!");
        }

        internal void SetError(string err)
        {
            Error.Text = err;
            Error.Visible = true;

            Enable.Checked = false;
        }

        private void Enable_CheckedChanged(object sender, EventArgs e)
        {
            if (Enable.Checked == false)
                foreach (Control control in list.Controls)
                {
                    if (control is not PluginControl pControl)
                        continue;

                    if (data.guid == "AtomicFramework")
                    {
                        if (pControl.data.atomicVersion != null)
                            pControl.Enable.Checked = false;
                    }
                    else if (pControl.data.dependencies.Any(d => d.guid == data.guid))
                        pControl.Enable.Checked = false;
                }
        }

        internal bool IsDisabled()
        {
            return !Enable.Checked;
        }
    }
}
