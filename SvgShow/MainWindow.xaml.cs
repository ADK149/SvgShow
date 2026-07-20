using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SvgShow.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace SvgShow
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<TreeItem> _treeItems = new();
        private bool _isMeasureMode = false;
        private bool _webViewReady = false;
        private string _currentSvgPath = "";
        private string? _pendingLoadFile = null;
        private List<string> _allSvgFiles = new();

        public MainWindow()
        {
            InitializeComponent();
            treeView.ItemsSource = _treeItems;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();

            if (!string.IsNullOrEmpty(App.StartupSvgFile))
            {
                var file = App.StartupSvgFile;
                var directory = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(directory))
                {
                    var svgFiles = Directory.GetFiles(directory, "*.svg", SearchOption.TopDirectoryOnly);
                    AddDirectoryToTree(svgFiles, file);
                }
            }
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
                if (!string.IsNullOrEmpty(_pendingLoadFile))
                {
                    var file = _pendingLoadFile;
                    _pendingLoadFile = null;
                    LoadSvgFile(file);
                }
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

        private void OpenSvg_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "SVG文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
                Multiselect = false,
                Title = "选择SVG文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var selectedFile = openFileDialog.FileName;
                var directory = Path.GetDirectoryName(selectedFile);
                
                if (!string.IsNullOrEmpty(directory))
                {
                    var svgFiles = Directory.GetFiles(directory, "*.svg", SearchOption.TopDirectoryOnly);
                    AddDirectoryToTree(svgFiles, selectedFile);
                }
            }
        }

        private void AddDirectoryToTree(string[] filePaths, string? initialFile = null)
        {
            if (filePaths.Length == 0) return;
            
            var directory = Path.GetDirectoryName(filePaths[0]);
            if (string.IsNullOrEmpty(directory)) return;
            
            var existingFolder = _treeItems.FirstOrDefault(item => item is FolderItem folder && folder.Path == directory) as FolderItem;
            
            if (existingFolder != null)
            {
                var existingFiles = existingFolder.Children.Cast<SvgFileItem>().Select(s => s.FullPath).ToHashSet();
                
                foreach (var filePath in filePaths)
                {
                    if (!existingFiles.Contains(filePath))
                    {
                        existingFolder.Children.Add(new SvgFileItem
                        {
                            FileName = Path.GetFileName(filePath),
                            FullPath = filePath
                        });
                    }
                }
                
                existingFolder.Children = new ObservableCollection<TreeItem>(
                    existingFolder.Children.OrderBy(c => c is SvgFileItem s ? s.FileName : ""));
            }
            else
            {
                var folder = new FolderItem
                {
                    Name = Path.GetFileName(directory),
                    Path = directory,
                    IsExpanded = true
                };
                
                foreach (var filePath in filePaths.OrderBy(f => Path.GetFileName(f)))
                {
                    folder.Children.Add(new SvgFileItem
                    {
                        FileName = Path.GetFileName(filePath),
                        FullPath = filePath
                    });
                }
                folder.HasSvgFiles = folder.Children.Count > 0;
                
                _treeItems.Add(folder);
            }
            
            foreach (var filePath in filePaths)
            {
                if (!_allSvgFiles.Contains(filePath))
                {
                    _allSvgFiles.Add(filePath);
                }
            }
            
            if (!string.IsNullOrEmpty(initialFile))
            {
                LoadSvgFile(initialFile);
            }
        }

        private async void LoadSvgFile(string filePath)
        {
            if (_currentSvgPath == filePath) return;

            if (!_webViewReady)
            {
                _pendingLoadFile = filePath;
                return;
            }

            try
            {
                _currentSvgPath = filePath;
                var svgContent = await File.ReadAllTextAsync(filePath);

                txtCurrentFile.Text = Path.GetFileName(filePath);
                txtFilePath.Text = filePath;

                SelectTreeViewItem(filePath);

                PostMessage(new { action = "clearAll" });
                await Task.Delay(50);
                PostMessage(new
                {
                    action = "addSvg",
                    id = "current",
                    svgContent = svgContent,
                    fileName = Path.GetFileName(filePath)
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载SVG文件失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTreeViewItem(string filePath)
        {
            foreach (var item in _treeItems)
            {
                if (item is not FolderItem folder) continue;

                foreach (var child in folder.Children)
                {
                    if (child is SvgFileItem fileItem && fileItem.FullPath == filePath && fileItem.IsSelected)
                    {
                        return;
                    }
                }
            }

            foreach (var item in _treeItems)
            {
                if (item is not FolderItem folder) continue;

                foreach (var child in folder.Children)
                {
                    if (child is SvgFileItem fileItem)
                    {
                        fileItem.IsSelected = fileItem.FullPath == filePath;
                        if (fileItem.FullPath == filePath)
                        {
                            folder.IsExpanded = true;
                        }
                    }
                }
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as SvgFileItem;
            if (selectedItem != null)
            {
                LoadSvgFile(selectedItem.FullPath);
            }
        }

        private void TreeView_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (treeScrollViewer != null)
            {
                treeScrollViewer.ScrollToVerticalOffset(
                    treeScrollViewer.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
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
            
            PostMessage(new
            {
                action = "setMeasureMode",
                enabled = _isMeasureMode
            });
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "resetView" });
        }

        private void PlayAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_allSvgFiles.Count < 2)
            {
                MessageBox.Show("至少需要加载2个SVG文件才能播放动画", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var interval = (int)(1000.0 / sliderSpeed.Value);
            
            PostMessage(new
            {
                action = "startAnimation",
                interval = interval,
                files = _allSvgFiles.Select(Path.GetFileName).ToList()
            });
        }

        private void PauseAnimation_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "pauseAnimation" });
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "stopAnimation" });
            
            if (!string.IsNullOrEmpty(_currentSvgPath))
            {
                LoadSvgFile(_currentSvgPath);
            }
        }

        private void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_webViewReady) return;
            var interval = (int)(1000.0 / e.NewValue);
            PostMessage(new { action = "setAnimationSpeed", speed = interval });
        }

        private void AssociateSvg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    MessageBox.Show("无法获取当前程序路径", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var progId = "SvgShow.Application";
                var friendlyName = "SvgShow 可视化工具";

                using (var classesRoot = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true))
                {
                    using (var progIdKey = classesRoot.CreateSubKey(progId))
                    {
                        progIdKey.SetValue("", friendlyName);
                        using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                        {
                            iconKey.SetValue("", $"\"{exePath}\",0");
                        }
                        using (var shellKey = progIdKey.CreateSubKey(@"shell\open\command"))
                        {
                            shellKey.SetValue("", $"\"{exePath}\" \"%1\"");
                        }
                    }

                    using (var svgKey = classesRoot.CreateSubKey(@".svg"))
                    {
                        svgKey.SetValue("", progId);
                    }
                }

                NotifyShell();

                MessageBox.Show($"已成功将.svg文件关联到本程序！\n程序路径：{exePath}\n\n现在双击.svg文件将自动使用本程序打开。",
                    "关联成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("没有足够的权限写入注册表。\n请尝试以管理员身份运行本程序后再进行关联。",
                    "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"关联失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NotifyShell()
        {
            try
            {
                var shChangeNotify = Win32.NativeMethods.SHChangeNotifyRegister(
                    Win32.NativeMethods.HWND_BROADCAST,
                    Win32.NativeMethods.SHCNF_IDLIST,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    1,
                    new[] { new Win32.NativeMethods.SHChangeNotifyEntry { } });
                
                Win32.NativeMethods.SHChangeNotify(
                    Win32.NativeMethods.SHCNE_ASSOCCHANGED,
                    Win32.NativeMethods.SHCNF_IDLIST,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            catch
            {
            }
        }
    }
}