using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;

namespace MapTerrainGeneratorWPF
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private Point _lastMousePosition;
        private double _camRadius = 1000;
        private double _camTheta = Math.PI / 4;
        private double _camPhi = Math.PI / 3;
        private Point3D _camTarget = new Point3D(0, 0, 0);

        private GeometryModel3D _previewModel;

        private string _lastGeneratedFuncGroup = null;
        private BrushData _lastTargetBrush = null;
        private string[] _lastOriginalLines = null;

        private ConfigSettings _appSettings;

        public MainWindow()
        {
            InitializeComponent();
            _appSettings = ConfigSettings.Load();
            ApplySettings();
        }

        private void Log(string msg)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {msg}\n");
            txtLog.ScrollToEnd();
        }

        private void ApplySettings()
        {
            cmbTargetMode.SelectedIndex = _appSettings.DefaultTargetMode;
            cmbNoiseType.SelectedIndex = _appSettings.DefaultNoiseType;
            UpdateDynamicVisibility();
            UpdateDefaultMapName();
        }

        private void MenuConfig_Click(object sender, RoutedEventArgs e)
        {
            var configWin = new ConfigWindow(_appSettings);
            configWin.Owner = this;
            if (configWin.ShowDialog() == true)
            {
                _appSettings = configWin.Settings;
                ApplySettings();
                Log("Configuration updated and saved.");
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void MenuGithub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/vallzy/Terrain-Generator/issues",
                UseShellExecute = true
            });
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Map Files (*.map)|*.map" };
            if (ofd.ShowDialog() == true) txtFile.Text = ofd.FileName;
        }

        private void InvalidateExport() { if (btnExport != null) btnExport.IsEnabled = false; }
        private void UI_Changed(object sender, RoutedEventArgs e) { InvalidateExport(); }
        private void TxtFile_TextChanged(object sender, TextChangedEventArgs e) { UpdateDefaultMapName(); InvalidateExport(); }

        private void UpdateDynamicVisibility()
        {
            if (cmbTargetMode == null) return;
            bool isManual = cmbTargetMode.SelectedIndex == 1;

            if (panelMapFile != null) panelMapFile.Visibility = isManual ? Visibility.Collapsed : Visibility.Visible;
            if (panelManualSize != null) panelManualSize.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;
            if (chkOverrideMap != null) chkOverrideMap.Visibility = isManual ? Visibility.Collapsed : Visibility.Visible;

            if (panelMapName != null)
            {
                if (isManual) panelMapName.Visibility = Visibility.Visible;
                else panelMapName.Visibility = (chkOverrideMap.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateDefaultMapName()
        {
            if (txtMapName == null) return;
            if (cmbTargetMode.SelectedIndex == 0)
                txtMapName.Text = !string.IsNullOrWhiteSpace(txtFile.Text) ? "output_" + Path.GetFileNameWithoutExtension(txtFile.Text) : "output_terrain";
            else
                if (string.IsNullOrWhiteSpace(txtMapName.Text) || txtMapName.Text.StartsWith("output_")) txtMapName.Text = "terrain_output";
        }

        private void CmbTargetMode_SelectionChanged(object sender, SelectionChangedEventArgs e) { UpdateDynamicVisibility(); UpdateDefaultMapName(); InvalidateExport(); }
        private void ChkOverrideMap_Changed(object sender, RoutedEventArgs e) { UpdateDynamicVisibility(); UpdateDefaultMapName(); }

        private void CmbShapeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelShapeHeight != null && lblShapeHeight != null)
            {
                int shapeType = cmbShapeType.SelectedIndex;
                if (shapeType == 0) panelShapeHeight.Visibility = Visibility.Collapsed;
                else
                {
                    panelShapeHeight.Visibility = Visibility.Visible;
                    switch (shapeType)
                    {
                        case 1: lblShapeHeight.Content = "Peak Height:"; break;
                        case 2: lblShapeHeight.Content = "Crater Depth:"; break;
                        case 3: lblShapeHeight.Content = "Ridge Height:"; break;
                        case 4: lblShapeHeight.Content = "Slope Height:"; break;
                        case 5: lblShapeHeight.Content = "Volcano Height:"; break;
                        case 6: lblShapeHeight.Content = "Valley Depth:"; break;
                        default: lblShapeHeight.Content = "Shape Height:"; break;
                    }
                }
            }
            if (panelTerrace != null) panelTerrace.Visibility = cmbShapeType.SelectedIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            InvalidateExport();
        }

        private void ChkAdvancedSubSquare_Changed(object sender, RoutedEventArgs e)
        {
            bool isAdvanced = chkAdvancedSubSquare.IsChecked == true;
            if (cmbSubSquarePreset != null) cmbSubSquarePreset.IsEnabled = !isAdvanced;
            if (panelAdvancedSubSquare != null) panelAdvancedSubSquare.Visibility = isAdvanced ? Visibility.Visible : Visibility.Collapsed;
            InvalidateExport();
        }

        private bool IsPowerOfTwo(double n) { int intN = (int)n; return (n == intN) && (intN > 0) && ((intN & (intN - 1)) == 0); }

        private bool TryGetSubSquareSizes(out double sizeX, out double sizeY)
        {
            sizeX = 64; sizeY = 64;
            if (chkAdvancedSubSquare.IsChecked == true)
            {
                if (!double.TryParse(txtSizeX.Text, out sizeX) || !double.TryParse(txtSizeY.Text, out sizeY)) { Log("Error: Invalid Advanced Sub-square Sizes."); return false; }
                if (!IsPowerOfTwo(sizeX) || !IsPowerOfTwo(sizeY)) { Log("Error: Sub-square Sizes must be powers of 2."); return false; }
            }
            else
            {
                if (cmbSubSquarePreset.SelectedItem is ComboBoxItem item && double.TryParse(item.Content.ToString(), out double presetVal)) { sizeX = presetVal; sizeY = presetVal; }
            }
            return true;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear(); btnExport.IsEnabled = false;

            string mode = cmbTargetMode.SelectedIndex == 0 ? "hint" : "manual";
            string[] originalLines = mode == "hint" && File.Exists(txtFile.Text) ? File.ReadAllLines(txtFile.Text) : new string[0];

            if (mode == "hint" && originalLines.Length == 0) { Log("Error: Select a valid .map file."); return; }

            if (!double.TryParse(txtGenWidth.Text, out double w) || !double.TryParse(txtGenLength.Text, out double l) || !double.TryParse(txtGenHeight.Text, out double h)) return;

            double stepX = 64, stepY = 64;
            if (chkAdvancedSubSquare.IsChecked == true) { double.TryParse(txtSizeX.Text, out stepX); double.TryParse(txtSizeY.Text, out stepY); }
            else { if (cmbSubSquarePreset.SelectedItem is ComboBoxItem item) double.TryParse(item.Content.ToString(), out stepX); stepY = stepX; }

            double shapeHeight = 0; double.TryParse(txtShapeHeight.Text, out shapeHeight);
            double terrace = 0; double.TryParse(txtTerrace.Text, out terrace);
            double variance = 0; double.TryParse(txtVariance.Text, out variance);
            double frequency = 0.005; double.TryParse(cmbFrequency.Text.Split(' ')[0], out frequency);

            var target = TerrainEngine.GetTargetBrushData(mode, originalLines, w, l, h, Log);
            if (target == null) return;

            TerrainEngine.AdjustBoundsToFitGrid(target, stepX, stepY, Log);
            var heightMap = TerrainEngine.GenerateHeightMap(target, stepX, stepY, cmbShapeType.SelectedIndex, shapeHeight, variance, frequency, cmbNoiseType.SelectedIndex, terrace);

            _lastGeneratedFuncGroup = TerrainEngine.GenerateFuncGroup(target, stepX, stepY, (variance > 0 || cmbShapeType.SelectedIndex > 0), txtTexture.Text, heightMap);
            _lastTargetBrush = target;
            _lastOriginalLines = originalLines;

            Build3DMesh(target, stepX, stepY, heightMap, shapeHeight);
            Log("Terrain generated successfully.");
            btnExport.IsEnabled = true;
        }

        private void Build3DMesh(BrushData target, double stepX, double stepY, System.Collections.Generic.Dictionary<(double, double), double> heightMap, double shapeHeight)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            int index = 0;

            for (double x = target.MinX; x < target.MaxX - 0.01; x += stepX)
            {
                for (double y = target.MinY; y < target.MaxY - 0.01; y += stepY)
                {
                    double currentMaxX = Math.Min(x + stepX, target.MaxX); double currentMaxY = Math.Min(y + stepY, target.MaxY);
                    double zBL = heightMap[(Math.Round(x, 2), Math.Round(y, 2))]; double zTL = heightMap[(Math.Round(x, 2), Math.Round(currentMaxY, 2))];
                    double zBR = heightMap[(Math.Round(currentMaxX, 2), Math.Round(y, 2))]; double zTR = heightMap[(Math.Round(currentMaxX, 2), Math.Round(currentMaxY, 2))];

                    mesh.Positions.Add(new Point3D(x, y, zBL)); mesh.Positions.Add(new Point3D(x, currentMaxY, zTL));
                    mesh.Positions.Add(new Point3D(currentMaxX, y, zBR)); mesh.Positions.Add(new Point3D(currentMaxX, currentMaxY, zTR));
                    mesh.TextureCoordinates.Add(new Point(0, 1)); mesh.TextureCoordinates.Add(new Point(0, 0));
                    mesh.TextureCoordinates.Add(new Point(1, 1)); mesh.TextureCoordinates.Add(new Point(1, 0));
                    mesh.TriangleIndices.Add(index + 0); mesh.TriangleIndices.Add(index + 2); mesh.TriangleIndices.Add(index + 1);
                    mesh.TriangleIndices.Add(index + 3); mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 2);
                    index += 4;
                }
            }

            modelGroup.Children.Clear();
            modelGroup.Children.Add(new AmbientLight(Color.FromRgb(100, 100, 100)));
            modelGroup.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.5, -1, -0.8)));

            _previewModel = new GeometryModel3D { Geometry = mesh };
            UpdateMaterial();
            modelGroup.Children.Add(_previewModel);

            _camTarget = new Point3D(target.MinX + (target.WidthX / 2), target.MinY + (target.LengthY / 2), target.MaxZ + (shapeHeight / 2));
            _camRadius = Math.Max(target.WidthX, target.LengthY) * 1.5;
            UpdateCamera();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastGeneratedFuncGroup == null || _lastTargetBrush == null || _lastOriginalLines == null)
            {
                Log("Error: Nothing to export. Please successfully generate the terrain first.");
                return;
            }

            string mode = cmbTargetMode.SelectedIndex == 0 ? "hint" : "manual";

            TerrainEngine.ExportFile(
                mode,                           
                _lastOriginalLines,             
                _lastTargetBrush,              
                _lastGeneratedFuncGroup,        
                txtFile.Text,                  
                txtMapName.Text,                
                chkOverrideMap.IsChecked == true, 
                _appSettings.OutputFolder,      
                Log                            
            );
        }

        private void ChkWireframe_Changed(object sender, RoutedEventArgs e) { UpdateMaterial(); }

        private void UpdateMaterial()
        {
            if (_previewModel == null) return;
            if (chkWireframe.IsChecked == true)
            {
                GeometryGroup group = new GeometryGroup();
                group.Children.Add(new RectangleGeometry(new Rect(0, 0, 1, 1))); group.Children.Add(new LineGeometry(new Point(0, 0), new Point(1, 1)));
                GeometryDrawing drawing = new GeometryDrawing { Brush = new SolidColorBrush(Color.FromRgb(40, 150, 60)), Pen = new Pen(new SolidColorBrush(Color.FromRgb(150, 255, 150)), 0.03), Geometry = group };
                Material material = new DiffuseMaterial(new DrawingBrush { Drawing = drawing });
                _previewModel.Material = material; _previewModel.BackMaterial = material;
            }
            else
            {
                Material material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(40, 150, 60)));
                _previewModel.Material = material; _previewModel.BackMaterial = material;
            }
        }

        private void Viewport_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (_previewModel != null)
                {
                    Rect3D bounds = _previewModel.Bounds;
                    _camTarget = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
                    _camRadius = Math.Max(bounds.SizeX, bounds.SizeY) * 1.5;
                    UpdateCamera();
                    return;
                }
            }

            _isDragging = true;
            _lastMousePosition = e.GetPosition(this);
            ((UIElement)sender).CaptureMouse();
        }

        private void Viewport_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { _isDragging = false; ((UIElement)sender).ReleaseMouseCapture(); }
        private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPosition = e.GetPosition(this);
            double deltaX = currentPosition.X - _lastMousePosition.X;
            double deltaY = currentPosition.Y - _lastMousePosition.Y;

            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
            {
                Vector3D lookDir = cameraPreview.LookDirection;
                lookDir.Normalize();
                Vector3D upDir = new Vector3D(0, 0, 1);
                Vector3D rightDir = Vector3D.CrossProduct(lookDir, upDir);
                rightDir.Normalize();
                Vector3D trueUp = Vector3D.CrossProduct(rightDir, lookDir);
                trueUp.Normalize();

                double panSpeed = 0.5 + (_camRadius * 0.0015);

                _camTarget -= rightDir * (deltaX * panSpeed);
                _camTarget += trueUp * (deltaY * panSpeed);
            }
            else
            {
                _camTheta += deltaX * 0.005;
                _camPhi += deltaY * 0.005;

                if (_camPhi < 0.01) _camPhi = 0.01;
                if (_camPhi > Math.PI - 0.01) _camPhi = Math.PI - 0.01;
            }

            _lastMousePosition = currentPosition;
            UpdateCamera();
        }
        private void Viewport_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            double zoomFactor = 1.1;
            if (e.Delta > 0)
                _camRadius /= zoomFactor;
            else
                _camRadius *= zoomFactor;

            _camRadius = Math.Max(10, Math.Min(_camRadius, 200000));
            UpdateCamera();
        }
        private void UpdateCamera()
        {
            double x = _camTarget.X + _camRadius * Math.Sin(_camPhi) * Math.Cos(_camTheta);
            double y = _camTarget.Y + _camRadius * Math.Sin(_camPhi) * Math.Sin(_camTheta);
            double z = _camTarget.Z + _camRadius * Math.Cos(_camPhi);
            cameraPreview.Position = new Point3D(x, y, z);
            cameraPreview.LookDirection = new Vector3D(_camTarget.X - x, _camTarget.Y - y, _camTarget.Z - z);
            cameraPreview.UpDirection = new Vector3D(0, 0, 1);
        }
    }
}