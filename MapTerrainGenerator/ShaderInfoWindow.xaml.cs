using System.Windows;

namespace MapTerrainGeneratorWPF
{
    public partial class ShaderInfoWindow : Window
    {
        public ShaderInfoWindow(string shaderName, string rawShaderText)
        {
            InitializeComponent();

            this.Title = $"Shader Information - {shaderName}";
            lblShaderName.Text = $"Shader: {shaderName}";
            txtShaderCode.Text = rawShaderText.Trim(); 
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtShaderCode.Text))
            {
                Clipboard.SetText(txtShaderCode.Text);
                MessageBox.Show("Shader code copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}