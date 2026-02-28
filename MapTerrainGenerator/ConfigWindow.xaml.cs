using System.Windows;

namespace MapTerrainGeneratorWPF
{
    public partial class ConfigWindow : Window
    {
        public ConfigSettings Settings { get; private set; }

        public ConfigWindow(ConfigSettings currentSettings)
        {
            InitializeComponent();
            Settings = currentSettings;

            // Load current values into UI
            txtOutputFolder.Text = Settings.OutputFolder;
            cmbDefTarget.SelectedIndex = Settings.DefaultTargetMode;
            cmbDefNoise.SelectedIndex = Settings.DefaultNoiseType;
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Default Export Folder",
                InitialDirectory = txtOutputFolder.Text
            };

            if (dialog.ShowDialog() == true)
            {
                txtOutputFolder.Text = dialog.FolderName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.OutputFolder = txtOutputFolder.Text;
            Settings.DefaultTargetMode = cmbDefTarget.SelectedIndex;
            Settings.DefaultNoiseType = cmbDefNoise.SelectedIndex;
            Settings.Save();
            this.DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}