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

            txtOutputFolder.Text = Settings.OutputFolder;
            txtGamePath.Text = Settings.GameDataPath;
            txtDefTexture.Text = Settings.DefaultTexture;
            cmbDefTarget.SelectedIndex = Settings.DefaultTargetMode;
            cmbDefNoise.SelectedIndex = Settings.DefaultNoiseType;
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Default Export Folder",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                txtOutputFolder.Text = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void BtnBrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Game Data Folder (e.g., etmain)",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                txtGamePath.Text = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void BtnBrowseDefTexture_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtGamePath.Text))
            {
                MessageBox.Show("Please set a valid Game Data Path first.", "Missing Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings.GameDataPath = txtGamePath.Text;

            var browserWin = new TextureBrowserWindow(Settings);
            browserWin.Owner = this;
            if (browserWin.ShowDialog() == true)
            {
                txtDefTexture.Text = browserWin.SelectedTexturePath;
                Settings = browserWin.Settings;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.OutputFolder = txtOutputFolder.Text;
            Settings.GameDataPath = txtGamePath.Text;
            Settings.DefaultTexture = txtDefTexture.Text;
            Settings.DefaultTargetMode = cmbDefTarget.SelectedIndex;
            Settings.DefaultNoiseType = cmbDefNoise.SelectedIndex;

            Settings.Save();
            this.DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}