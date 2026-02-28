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

        private void BtnBrowseTopTexture_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureGameDataPath()) return;

            var browserWin = new TextureBrowserWindow(_appSettings);
            browserWin.Owner = this;
            if (browserWin.ShowDialog() == true)
            {
                txtTexture.Text = browserWin.SelectedTexturePath;
                _appSettings = browserWin.Settings; 
                _appSettings.Save();
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
                        case 7: lblShapeHeight.Content = "Tunnel Height:"; break;
                        case 8: lblShapeHeight.Content = "Slope Height:"; break;
                        default: lblShapeHeight.Content = "Shape Height:"; break;
                    }
                }
            }
            if (panelTunnelHeight != null)
                panelTunnelHeight.Visibility = cmbShapeType.SelectedIndex == 8 ? Visibility.Visible : Visibility.Collapsed;
            if (panelTerrace != null) panelTerrace.Visibility = cmbShapeType.SelectedIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            InvalidateExport();
        }

        private bool EnsureGameDataPath()
        {
            if (string.IsNullOrWhiteSpace(_appSettings.GameDataPath))
            {
                var result = MessageBox.Show(
                    "The Game Data Path has not been set. Would you like to open Configuration to set it now?",
                    "Missing Game Path",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    MenuConfig_Click(null, null);
                    return !string.IsNullOrWhiteSpace(_appSettings.GameDataPath);
                }
                return false; 
            }
            return true; 
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

        private void MenuTextureBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureGameDataPath()) return;
            var browserWin = new TextureBrowserWindow(_appSettings);
            browserWin.Owner = this;
            browserWin.Show();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear(); btnExport.IsEnabled = false;
            _lastGeneratedFuncGroup = null; _lastTargetBrush = null; _lastOriginalLines = null;

            string mode = cmbTargetMode.SelectedIndex == 0 ? "hint" : "manual";
            string[] originalLines = new string[0];
            if (mode == "hint")
            {
                if (!File.Exists(txtFile.Text)) { Log("Error: Select a valid .map file."); return; }
                originalLines = File.ReadAllLines(txtFile.Text);
            }

            if (!double.TryParse(txtGenWidth.Text, out double w) || !double.TryParse(txtGenLength.Text, out double l) || !double.TryParse(txtGenHeight.Text, out double h)) return;
            if (!TryGetSubSquareSizes(out double stepX, out double stepY)) return;

            string rawFrequency = cmbFrequency.Text.Split(' ')[0];
            double shapeHeight = 0;
            if (cmbShapeType.SelectedIndex > 0 && !double.TryParse(txtShapeHeight.Text, out shapeHeight)) { Log("Error: Invalid Shape Height."); return; }
            if (!double.TryParse(txtTerrace.Text, out double terraceStep) || !double.TryParse(txtVariance.Text, out double variance) || !double.TryParse(rawFrequency, out double frequency)) return;

            string topTexture = string.IsNullOrWhiteSpace(txtTexture.Text) ? "common/caulk" : txtTexture.Text;
            int shapeType = cmbShapeType.SelectedIndex;
            int noiseType = cmbNoiseType.SelectedIndex;

            var target = TerrainEngine.GetTargetBrushData(mode, originalLines, w, l, h, Log);
            if (target == null) return;

            TerrainEngine.AdjustBoundsToFitGrid(target, stepX, stepY, Log);
            Log($"Final Terrain Grid: {target.WidthX}x{target.LengthY} | Step Size: X:{stepX} Y:{stepY}");

            if (shapeType == 7 || shapeType == 8) // Tunnel / Slope Tunnel
            {
                double caveHeight = shapeHeight;
                double slopeHeight = 0;
                if (shapeType == 8)
                {
                    if (!double.TryParse(txtTunnelHeight.Text, out caveHeight)) { Log("Error: Invalid Tunnel Height."); return; }
                    slopeHeight = shapeHeight;
                }
                var (floorMap, ceilingMap, leftWallMap, rightWallMap, stepZ) = TerrainEngine.GenerateTunnelHeightMaps(target, stepX, stepY, caveHeight, slopeHeight, variance, frequency, noiseType, terraceStep);
                _lastGeneratedFuncGroup = TerrainEngine.GenerateTunnelFuncGroups(target, stepX, stepY, stepZ, topTexture, floorMap, ceilingMap, leftWallMap, rightWallMap, caveHeight, slopeHeight);
                _lastTargetBrush = target;
                _lastOriginalLines = originalLines;
                Build3DCaveMesh(target, stepX, stepY, stepZ, floorMap, ceilingMap, leftWallMap, rightWallMap, caveHeight, slopeHeight);
            }
            else
            {
                bool splitDiagonally = variance > 0 || shapeType > 0;
                var heightMap = TerrainEngine.GenerateHeightMap(target, stepX, stepY, shapeType, shapeHeight, variance, frequency, noiseType, terraceStep);
                _lastGeneratedFuncGroup = TerrainEngine.GenerateFuncGroup(target, stepX, stepY, splitDiagonally, topTexture, heightMap);
                _lastTargetBrush = target;
                _lastOriginalLines = originalLines;
                Build3DMesh(target, stepX, stepY, heightMap, shapeHeight);
            }

            Log("Terrain generated successfully. Ready to export.");
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

        private void Build3DCaveMesh(BrushData target, double stepX, double stepY, double stepZ,
            System.Collections.Generic.Dictionary<(double, double), double> floorMap,
            System.Collections.Generic.Dictionary<(double, double), double> ceilingMap,
            System.Collections.Generic.Dictionary<(double y, double z), double> leftWallMap,
            System.Collections.Generic.Dictionary<(double y, double z), double> rightWallMap,
            double caveHeight, double slopeHeight = 0)
        {
            MeshGeometry3D floorMesh = new MeshGeometry3D();
            MeshGeometry3D ceilMesh = new MeshGeometry3D();
            MeshGeometry3D leftMesh = new MeshGeometry3D();
            MeshGeometry3D rightMesh = new MeshGeometry3D();
            int fIdx = 0, cIdx = 0, lIdx = 0, rIdx = 0;

            for (double x = target.MinX; x < target.MaxX - 0.01; x += stepX)
            {
                for (double y = target.MinY; y < target.MaxY - 0.01; y += stepY)
                {
                    double currentMaxX = Math.Min(x + stepX, target.MaxX);
                    double currentMaxY = Math.Min(y + stepY, target.MaxY);

                    double fBL = floorMap[(Math.Round(x, 2), Math.Round(y, 2))];
                    double fTL = floorMap[(Math.Round(x, 2), Math.Round(currentMaxY, 2))];
                    double fBR = floorMap[(Math.Round(currentMaxX, 2), Math.Round(y, 2))];
                    double fTR = floorMap[(Math.Round(currentMaxX, 2), Math.Round(currentMaxY, 2))];

                    floorMesh.Positions.Add(new Point3D(x, y, fBL));
                    floorMesh.Positions.Add(new Point3D(x, currentMaxY, fTL));
                    floorMesh.Positions.Add(new Point3D(currentMaxX, y, fBR));
                    floorMesh.Positions.Add(new Point3D(currentMaxX, currentMaxY, fTR));
                    floorMesh.TriangleIndices.Add(fIdx + 0); floorMesh.TriangleIndices.Add(fIdx + 2); floorMesh.TriangleIndices.Add(fIdx + 1);
                    floorMesh.TriangleIndices.Add(fIdx + 3); floorMesh.TriangleIndices.Add(fIdx + 1); floorMesh.TriangleIndices.Add(fIdx + 2);
                    fIdx += 4;

                    double cBL = ceilingMap[(Math.Round(x, 2), Math.Round(y, 2))];
                    double cTL = ceilingMap[(Math.Round(x, 2), Math.Round(currentMaxY, 2))];
                    double cBR = ceilingMap[(Math.Round(currentMaxX, 2), Math.Round(y, 2))];
                    double cTR = ceilingMap[(Math.Round(currentMaxX, 2), Math.Round(currentMaxY, 2))];

                    ceilMesh.Positions.Add(new Point3D(x, y, cBL));
                    ceilMesh.Positions.Add(new Point3D(x, currentMaxY, cTL));
                    ceilMesh.Positions.Add(new Point3D(currentMaxX, y, cBR));
                    ceilMesh.Positions.Add(new Point3D(currentMaxX, currentMaxY, cTR));
                    ceilMesh.TriangleIndices.Add(cIdx + 0); ceilMesh.TriangleIndices.Add(cIdx + 1); ceilMesh.TriangleIndices.Add(cIdx + 2);
                    ceilMesh.TriangleIndices.Add(cIdx + 3); ceilMesh.TriangleIndices.Add(cIdx + 2); ceilMesh.TriangleIndices.Add(cIdx + 1);
                    cIdx += 4;
                }
            }

            double wallMinZ = target.MaxZ;
            double wallMaxZ = target.MaxZ + caveHeight + slopeHeight;
            for (double gy = target.MinY; gy < target.MaxY - 0.01; gy += stepY)
            {
                for (double gz = wallMinZ; gz < wallMaxZ - 0.01; gz += stepZ)
                {
                    double gmaxY = Math.Min(gy + stepY, target.MaxY);
                    double gmaxZ = Math.Min(gz + stepZ, wallMaxZ);
                    double rgy = Math.Round(gy, 2), rgmaxY = Math.Round(gmaxY, 2);
                    double rgz = Math.Round(gz, 2), rgmaxZ = Math.Round(gmaxZ, 2);

                    double lBL = leftWallMap[(rgy, rgz)];
                    double lTL = leftWallMap[(rgy, rgmaxZ)];
                    double lBR = leftWallMap[(rgmaxY, rgz)];
                    double lTR = leftWallMap[(rgmaxY, rgmaxZ)];

                    leftMesh.Positions.Add(new Point3D(lBL, gy, gz));
                    leftMesh.Positions.Add(new Point3D(lTL, gy, gmaxZ));
                    leftMesh.Positions.Add(new Point3D(lBR, gmaxY, gz));
                    leftMesh.Positions.Add(new Point3D(lTR, gmaxY, gmaxZ));
                    leftMesh.TriangleIndices.Add(lIdx + 0); leftMesh.TriangleIndices.Add(lIdx + 1); leftMesh.TriangleIndices.Add(lIdx + 2);
                    leftMesh.TriangleIndices.Add(lIdx + 3); leftMesh.TriangleIndices.Add(lIdx + 2); leftMesh.TriangleIndices.Add(lIdx + 1);
                    lIdx += 4;

                    double rBL = rightWallMap[(rgy, rgz)];
                    double rTL = rightWallMap[(rgy, rgmaxZ)];
                    double rBR = rightWallMap[(rgmaxY, rgz)];
                    double rTR = rightWallMap[(rgmaxY, rgmaxZ)];

                    rightMesh.Positions.Add(new Point3D(rBL, gy, gz));
                    rightMesh.Positions.Add(new Point3D(rTL, gy, gmaxZ));
                    rightMesh.Positions.Add(new Point3D(rBR, gmaxY, gz));
                    rightMesh.Positions.Add(new Point3D(rTR, gmaxY, gmaxZ));
                    rightMesh.TriangleIndices.Add(rIdx + 0); rightMesh.TriangleIndices.Add(rIdx + 2); rightMesh.TriangleIndices.Add(rIdx + 1);
                    rightMesh.TriangleIndices.Add(rIdx + 3); rightMesh.TriangleIndices.Add(rIdx + 1); rightMesh.TriangleIndices.Add(rIdx + 2);
                    rIdx += 4;
                }
            }

            modelGroup.Children.Clear();
            modelGroup.Children.Add(new AmbientLight(Color.FromRgb(100, 100, 100)));
            modelGroup.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.5, -1, -0.8)));

            Material mat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(40, 150, 60)));
            _previewModel = new GeometryModel3D { Geometry = floorMesh, Material = mat, BackMaterial = mat };
            modelGroup.Children.Add(_previewModel);
            modelGroup.Children.Add(new GeometryModel3D { Geometry = ceilMesh, Material = mat, BackMaterial = mat });
            modelGroup.Children.Add(new GeometryModel3D { Geometry = leftMesh, Material = mat, BackMaterial = mat });
            modelGroup.Children.Add(new GeometryModel3D { Geometry = rightMesh, Material = mat, BackMaterial = mat });

            _camTarget = new Point3D(target.MinX + (target.WidthX / 2), target.MinY + (target.LengthY / 2), target.MaxZ + (caveHeight + slopeHeight) / 2);
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