using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SvgShow.Models;
using SvgShow.Services;
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
        private bool _webViewReady = false;
        private string _currentSvgPath = "";
        private string? _pendingLoadFile = null;
        private string? _pendingSvgContent = null;

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
                    case "svgContent":
                        HandleSvgContent(root);
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

        private void HandleSvgContent(JsonElement root)
        {
            if (!root.TryGetProperty("content", out var contentProp)) return;
            var svgContent = contentProp.GetString();
            if (string.IsNullOrEmpty(svgContent)) return;

            _pendingSvgContent = svgContent;

            Dispatcher.Invoke(() =>
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "SVG文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
                    Title = "另存为SVG文件",
                    FileName = $"{Path.GetFileNameWithoutExtension(_currentSvgPath)}_modified.svg"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, svgContent);
                        MessageBox.Show($"SVG文件已保存：{saveFileDialog.FileName}", "保存成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败: {ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                _pendingSvgContent = null;
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

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new { action = "resetView" });
        }

        private void StrokeWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtStrokeWidth != null)
                txtStrokeWidth.Text = e.NewValue.ToString("F1");
        }

        private void TxtStrokeWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sliderStrokeWidth != null && double.TryParse(txtStrokeWidth.Text, out var val))
            {
                sliderStrokeWidth.Value = val;
            }
        }

        private void FontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtFontSize != null)
                txtFontSize.Text = e.NewValue.ToString("F1");
        }

        private void TxtFontSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sliderFontSize != null && double.TryParse(txtFontSize.Text, out var val))
            {
                val = Math.Max(0.5, Math.Min(10, val));
                sliderFontSize.Value = val;
            }
        }

        private void Override_CheckedChanged(object sender, RoutedEventArgs e)
        {
            PostMessage(new
            {
                action = "setStyleConfig",
                enableOverride = chkEnableOverride.IsChecked == true,
                strokeWidth = sliderStrokeWidth.Value,
                fontScale = sliderFontSize.Value,
                reapply = true
            });
        }

        private void ApplyStyle_Click(object sender, RoutedEventArgs e)
        {
            PostMessage(new
            {
                action = "setStyleConfig",
                enableOverride = chkEnableOverride.IsChecked == true,
                strokeWidth = sliderStrokeWidth.Value,
                fontScale = sliderFontSize.Value,
                reapply = true
            });
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

        private void NewSvg_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "SVG文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
                Title = "新建SVG文件",
                FileName = "new.svg",
                DefaultExt = ".svg"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var svgContent = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 800 600""></svg>";
                File.WriteAllText(saveFileDialog.FileName, svgContent);
                
                var directory = Path.GetDirectoryName(saveFileDialog.FileName);
                if (!string.IsNullOrEmpty(directory))
                {
                    var svgFiles = Directory.GetFiles(directory, "*.svg", SearchOption.TopDirectoryOnly);
                    AddDirectoryToTree(svgFiles, saveFileDialog.FileName);
                }
                
                LoadSvgFile(saveFileDialog.FileName);
                
                MessageBox.Show($"SVG文件已创建：{saveFileDialog.FileName}", "创建成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportJson_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSvgPath))
            {
                MessageBox.Show("请先打开一个SVG文件，然后再导入JSON数据", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            else
            {
                MessageBox.Show("导入的JSON数据将显示在当前打开的工作区中！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入JSON数据"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var jsonContent = File.ReadAllText(openFileDialog.FileName);
                    var elements = DataTool.ParseJsonGeometry(jsonContent);

                    if (elements.Count == 0)
                    {
                        // 解析失败时回退到原始JSON导入方式
                        PostMessage(new
                        {
                            action = "importJsonToSvg",
                            jsonData = jsonContent,
                            fileName = Path.GetFileName(openFileDialog.FileName)
                        });
                    }
                    else
                    {
                        PostMessage(new
                        {
                            action = "importJsonToSvg",
                            jsonData = elements.Select(el => new
                            {
                                type = el.Type,
                                cx = el.Cx,
                                cy = el.Cy,
                                r = el.R,
                                x1 = el.X1,
                                y1 = el.Y1,
                                x2 = el.X2,
                                y2 = el.Y2,
                                d = el.D,
                                fill = el.Fill,
                                stroke = el.Stroke,
                                strokeWidth = el.StrokeWidth
                            }).ToList(),
                            fileName = Path.GetFileName(openFileDialog.FileName)
                        });
                    }

                    MessageBox.Show($"JSON数据已导入：{openFileDialog.FileName}，共解析 {elements.Count} 个几何元素", "导入成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportTxt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSvgPath))
            {
                MessageBox.Show("请先打开一个SVG文件，然后再导入TXT数据", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "TXT文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                Title = "导入TXT数据"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var txtContent = File.ReadAllText(openFileDialog.FileName);
                    var elements = DataTool.ParseTxtGeometry(txtContent);

                    if (elements.Count == 0)
                    {
                        MessageBox.Show("未从TXT文件中解析到有效的几何数据", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    PostMessage(new
                    {
                        action = "importTxtToSvg",
                        elements = elements.Select(el => new
                        {
                            type = el.Type,
                            cx = el.Cx,
                            cy = el.Cy,
                            r = el.R,
                            x1 = el.X1,
                            y1 = el.Y1,
                            x2 = el.X2,
                            y2 = el.Y2,
                            d = el.D,
                            fill = el.Fill,
                            stroke = el.Stroke,
                            strokeWidth = el.StrokeWidth
                        }).ToList(),
                        fileName = Path.GetFileName(openFileDialog.FileName)
                    });

                    MessageBox.Show($"TXT数据已导入：{openFileDialog.FileName}，共解析 {elements.Count} 个几何元素", "导入成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsSvg_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSvgPath))
            {
                MessageBox.Show("请先打开一个SVG文件", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PostMessage(new { action = "getSvgContent" });
        }
    }
}