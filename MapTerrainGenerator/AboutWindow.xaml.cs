using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace MapTerrainGeneratorWPF
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        // This method catches clicks on all Hyperlinks in the window and opens the default browser
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true // Required in .NET Core / .NET 5+ to open URLs
                });
            }
            catch
            {
                MessageBox.Show("Unable to open link.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                e.Handled = true;
            }
        }
    }
}