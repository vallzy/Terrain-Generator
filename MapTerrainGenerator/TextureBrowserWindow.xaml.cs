using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MapTerrainGeneratorWPF
{
    public partial class TextureBrowserWindow : Window
    {
        private string _gameDataPath;
        private List<object> _allSets;

        public ConfigSettings Settings { get; private set; }
        public string SelectedTexturePath { get; private set; }
        private List<TextureItem> _allFlatTextures = new List<TextureItem>();

        public TextureBrowserWindow(ConfigSettings settings)
        {
            InitializeComponent();
            Settings = settings;
            _gameDataPath = Settings.GameDataPath;
            LoadData();
        }

        private async void LoadData()
        {
            if (string.IsNullOrWhiteSpace(_gameDataPath)) return;

            var rawSets = await Task.Run(() => TextureManager.LoadTextureSets(_gameDataPath));

            _allFlatTextures.Clear();
            foreach (var item in rawSets)
            {
                if (item is TextureGroup group) foreach (var set in group.Sets) _allFlatTextures.AddRange(set.Textures);
                else if (item is TextureSet set) _allFlatTextures.AddRange(set.Textures);
            }

            TextureSet favoritesSet = new TextureSet { Name = "★ Favorites" };
            foreach (string favName in Settings.FavoriteTextures)
            {
                var tex = _allFlatTextures.Find(t => t.Name.Equals(favName, System.StringComparison.OrdinalIgnoreCase));
                if (tex != null) favoritesSet.Textures.Add(tex);
            }

            rawSets.Insert(0, favoritesSet);
            _allSets = rawSets;
            treeTextureSets.ItemsSource = _allSets;

            if (favoritesSet.Textures.Count > 0)
            {
                _ = Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (treeTextureSets.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem firstNode)
                    {
                        firstNode.IsSelected = true;
                        firstNode.BringIntoView();
                    }
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu && menu.PlacementTarget is FrameworkElement target)
            {
                if (target.DataContext is TextureItem item)
                {
                    foreach (var child in menu.Items)
                    {
                        if (child is MenuItem menuItem && menuItem.Tag?.ToString() == "FavMenuItem")
                        {
                            if (Settings.FavoriteTextures.Contains(item.Name))
                            {
                                menuItem.Header = "Remove from favorites";
                            }
                            else
                            {
                                menuItem.Header = "Add to favorites";
                            }
                            break; 
                        }
                    }
                }
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (lstTextures.SelectedItem is TextureItem item)
            {
                SelectedTexturePath = GetCleanTextureName(item.Name); 
                this.DialogResult = true;
            }
            else MessageBox.Show("Please select a texture first.", "No Selection");
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;

        private void LstTextures_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstTextures.SelectedItem is TextureItem item)
            {
                SelectedTexturePath = GetCleanTextureName(item.Name); 
                this.DialogResult = true;
            }
        }

        private void MenuToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.DataContext is TextureItem item)
            {
                if (Settings.FavoriteTextures.Contains(item.Name))
                {
                    Settings.FavoriteTextures.Remove(item.Name);
                    MessageBox.Show("Removed from Favorites.");
                }
                else
                {
                    Settings.FavoriteTextures.Add(item.Name);
                    MessageBox.Show("Added to Favorites.");
                }
                Settings.Save();
                LoadData(); 
            }
        }

        private void MenuSetDefault_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.DataContext is TextureItem item)
            {
                Settings.DefaultTexture = GetCleanTextureName(item.Name); 
                Settings.Save();
                MessageBox.Show($"{Settings.DefaultTexture} set as Default Texture.");
            }
        }

        private void MenuShaderInfo_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.DataContext is TextureItem item)
            {
                if (item.IsShader && !string.IsNullOrWhiteSpace(item.RawShaderText))
                {
                    var infoWin = new ShaderInfoWindow(item.Name, item.RawShaderText);
                    infoWin.Owner = this; 
                    infoWin.ShowDialog();
                }
                else
                {
                    MessageBox.Show("This texture is not a shader or has no shader text available.", "No Shader Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void TreeTextureSets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RefreshTextureList();
        }

        private void ChkShowShaders_Click(object sender, RoutedEventArgs e)
        {
            RefreshTextureList();
        }

        private void LstTextureSets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshTextureList();
        }

        private string GetCleanTextureName(string fullName)
        {
            if (fullName.StartsWith("textures/", System.StringComparison.OrdinalIgnoreCase))
                return fullName.Substring(9); 
            return fullName;
        }


        private async void RefreshTextureList()
        {
            var selectedItem = treeTextureSets.SelectedItem;
            if (selectedItem == null) return;

            lstTextures.ItemsSource = null; 
            List<TextureItem> texturesToDisplay = new List<TextureItem>();

            if (selectedItem is TextureSet selectedSet)
            {
                texturesToDisplay.AddRange(selectedSet.Textures);
            }
            else if (selectedItem is TextureGroup selectedGroup)
            {
                foreach (var set in selectedGroup.Sets)
                {
                    texturesToDisplay.AddRange(set.Textures);
                }
            }

            bool shadersOnly = chkShowShaders.IsChecked == true;
            var filteredTextures = texturesToDisplay.FindAll(t => !shadersOnly || t.IsShader);

            await Task.Run(() =>
            {
                foreach (var tex in filteredTextures)
                {
                    if (tex.Thumbnail == null)
                    {
                        tex.Thumbnail = TextureManager.LoadImage(tex, _gameDataPath);
                    }
                }
            });
            lstTextures.ItemsSource = filteredTextures;
        }
    }

    public class ShaderBorderConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isShader && isShader)
            {
                return new SolidColorBrush(Color.FromRgb(100, 255, 100));
            }
            return new SolidColorBrush(Colors.DarkGray);
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}