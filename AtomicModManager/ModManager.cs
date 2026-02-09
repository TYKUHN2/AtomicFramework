namespace AtomicFramework
{
    public partial class ModManager : Form
    {
        private Plugin[]? disabled;

        public ModManager()
        {
            InitializeComponent();

            Plugin[]? plugins = PluginHelper.GetPlugins();
            if (plugins == null)
                return;

            PluginList.Controls.AddRange([.. plugins.Select(p => new PluginControl(PluginList, p))]);

            PluginList_SizeChanged(this, new());
        }

        private void PluginList_SizeChanged(object sender, EventArgs e)
        {
            foreach (Control control in PluginList.Controls)
            {
                control.Width = PluginList.Width;
            }
        }

        private void ModManager_FormClosed(object sender, FormClosedEventArgs e)
        {
            disabled = [.. PluginList.Controls.OfType<PluginControl>()
                .Where(c => c.IsDisabled())
                .Select(c => c.data)];
        }

        internal Plugin[] GetDisabled()
        {
            if (disabled != null)
                return disabled;

            return [..PluginList.Controls.OfType<PluginControl>()
                .Where(c => c.IsDisabled())
                .Select(c => c.data)] ;
        }
    }
}
