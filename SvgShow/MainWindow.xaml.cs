using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace SvgShow
{
    public class LayerItem
    {
        public string Name { get; set; } = "";
        public bool IsVisible { get; set; } = true;
        public string Id { get; set; } = "";
    }

    public class SvgFileItem
    {
        public string FileName { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string Id { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<LayerItem> _layers = new();
        private ObservableCollection<SvgFileItem> _svgFiles = new();
        private bool _isMeasureMode = false;
        private bool _webViewReady = false;
        private int _svgCounter = 0;

        public MainWindow()
        {
            InitializeComponent();
            lstLayers.ItemsSource = _layers;
            lstSvgFiles.ItemsSource = _svgFiles;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            await webView.EnsureCoreWebView2Async();
            
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            
            var viewerPath = Path.Combine(AppContext.BaseDirectory, "Web", "viewer.html");
            webView.Source = new Uri(viewerPath);
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _webViewReady = true;
            }
        }

        private void PostMessage(object message)
        {
            if (!_webViewReady) return;
            try
            {
                var json = JsonSerializer.Serialize(message);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PostMessage error: {ex.Message}");
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (!root.TryGetProperty("type", out var typeProp)) return;
                var type = typeProp.GetString();
                
                switch (type)
                {
                    case "mouseMove":
                        HandleMouseMove(root);
                        break;
                    case "elementSelected":
                        HandleElementSelected(root);
                        break;
                    case "selectionCleared":
                        HandleSelectionCleared();
                        break;
                    case "scaleChanged":
                        HandleScaleChanged(root);
                        break;
                    case "measureStart":
                        HandleMeasureStart(root);
                        break;
                    case "measureComplete":
                        HandleMeasureComplete(root);
                        break;
                    case "animationFrame":
                        HandleAnimationFrame(root);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void HandleMouseMove(JsonElement root)
        {
            var x = root.GetProperty("x").GetDouble();
            var y = root.GetProperty("y").GetDouble();
            var screenX = root.GetProperty("screenX").GetDouble();
            var screenY = root.GetProperty("screenY").GetDouble();
            
            Dispatcher.Invoke(() =>
            {
                txtMousePosition.Text = $"({screenX:F2}, {screenY:F2})";
                txtSvgPosition.Text = $"({x:F2}, {y:F2})";
            });
        }

        private void HandleElementSelected(JsonElement root)
        {
            if (!root.TryGetProperty("element", out var elem)) return;
            
            var type = elem.GetProperty("type").GetString();
            var x = elem.GetProperty("x").GetDouble();
            var y = elem.GetProperty("y").GetDouble();
            var width = elem.GetProperty("width").GetDouble();
            var height = elem.GetProperty("height").GetDouble();
            var centerX = elem.GetProperty("centerX").GetDouble();
            var centerY = elem.GetProperty("centerY").GetDouble();
            
            Dispatcher.Invoke(() =>
            {
                txtShapeType.Text = type;
                txtBounds.Text = $"X:{x:F2}, Y:{y:F2}, W:{width:F2}, H:{height:F2}";
                txtCenter.Text = $"({centerX:F2}, {centerY:F2})";
                txtWidth.Text = $"{width:F2}";
                txtHeight.Text = $"{height:F2}";
            });
        }

        private void HandleSelectionCleared()
        {
            Dispatcher.Invoke(() =>
            {
                txtShapeType.Text = "";
                txtBounds.Text = "";
                txtCenter.Text = "";
                txtWidth.Text = "";
                txtHeight.Text = "";
            });
        }

        private void HandleScaleChanged(JsonElement root)
        {
            var scale = root.GetProperty("scale").GetDouble();
            Dispatcher.Invoke(() =>
            {
                txtScale.Text = $"{scale * 100:F0}%";
            });
        }

        private void HandleMeasureStart(JsonElement root)
        {
            var x = root.GetProperty("x").GetDouble();
            var y = root.GetProperty("y").GetDouble();
            Dispatcher.Invoke(() =>
            {
                txtStartPoint.Text = $"({x:F2}, {y:F2})";
            });
        }

        private void HandleMeasureComplete(JsonElement root)
        {
            var startX = root.GetProperty("startX").GetDouble();
            var startY = root.GetProperty("startY").GetDouble();
            var endX = root.GetProperty("endX").GetDouble();
            var endY = root.GetProperty("endY").GetDouble();
            var distance = root.GetProperty("distance").GetDouble();
            
            Dispatcher.Invoke(() =>
            {
                txtStartPoint.Text = $"({startX:F2}, {startY:F2})";
                txtEndPoint.Text = $"({endX:F2}, {endY:F2})";
                txtDistance.Text = $"{distance:F2}";
            });
        }

        private void HandleAnimationFrame(JsonElement root)
        {
            var frame = root.GetProperty("frame").GetInt32();
            var total = root.GetProperty("total").GetInt32();
            var fileName = root.GetProperty("fileName").GetString();
            
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"动画帧 {frame + 1}/{total}: {fileName}");
            });
        }

        private void OpenSvg_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "SVG文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
                Multiselect = true,
                Title = "选择SVG文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadSvgFiles(openFileDialog.FileNames);
            }
        }

        private async void LoadSvgFiles(string[] filePaths)
        {
            try
            {
                foreach (var filePath in filePaths)
                {
                    var svgContent = await File.ReadAllTextAsync(filePath);
                    var fileName = Path.GetFileName(filePath);
                    var id = $"svg{_svgCounter++}";
                    
                    var svgItem = new SvgFileItem
                    {
                        FileName = fileName,
                        IsActive = true,
                        Id = id
                    };
                    _svgFiles.Add(svgItem);

                    _layers.Add(new LayerItem
                    {
                        Name = fileName,
                        IsVisible = true,
                        Id = id
                    });

                    var message = new
                    {
                        action = "addSvg",
                        id = id,
                        svgContent = svgContent,
                        fileName = fileName
                    };
                    PostMessage(message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载SVG文件失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LayerChecked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var layer = checkbox?.DataContext as LayerItem;
            
            if (layer != null)
            {
                var message = new
                {
                    action = "setLayerVisible",
                    id = layer.Id,
                    visible = layer.IsVisible
                };
                PostMessage(message);
            }
        }

        private void SvgFileChecked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var svgFile = checkbox?.DataContext as SvgFileItem;
            
            if (svgFile != null)
            {
                var layer = _layers.FirstOrDefault(l => l.Id == svgFile.Id);
                if (layer != null)
                {
                    layer.IsVisible = svgFile.IsActive;
                }
                
                var message = new
                {
                    action = "setLayerVisible",
                    id = svgFile.Id,
                    visible = svgFile.IsActive
                };
                PostMessage(message);
            }
        }

        private void MeasureTool_Click(object sender, RoutedEventArgs e)
        {
            _isMeasureMode = !_isMeasureMode;
            
            if (_isMeasureMode)
            {
                btnMeasure.Background = System.Windows.Media.Brushes.LightBlue;
                txtStartPoint.Text = "";
                txtEndPoint.Text = "";
                txtDistance.Text = "";
            }
            else
            {
                btnMeasure.Background = System.Windows.Media.Brushes.LightGray;
            }
            
            var message = new
            {
                action = "setMeasureMode",
                enabled = _isMeasureMode
            };
            PostMessage(message);
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "resetView" });
        }

        private void PlayAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_svgFiles.Count < 2)
            {
                MessageBox.Show("至少需要加载2个SVG文件才能播放动画", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var interval = (int)(1000.0 / sliderSpeed.Value);
            PostMessage(new { action = "startAnimation", interval = interval });
        }

        private void PauseAnimation_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "pauseAnimation" });
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "stopAnimation" });
        }

        private void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_webViewReady) return;
            var interval = (int)(1000.0 / e.NewValue);
            PostMessage(new { action = "setAnimationSpeed", speed = interval });
        }
    }
}