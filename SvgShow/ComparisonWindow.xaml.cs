using Microsoft.Win32;
using SvgShow.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SvgShow
{
    public partial class ComparisonWindow : Window
    {
        /// <summary>主窗口的目录项（用于同步新打开的目录到主窗口）</summary>
        private readonly ObservableCollection<TreeItem> _mainTreeItems;

        /// <summary>主窗口引用（用于新建SVG后加载到主界面）</summary>
        private readonly MainWindow _mainWindow;

        /// <summary>比对面板数据模型</summary>
        private readonly ComparisonViewModel _vm = new();

        public ComparisonWindow(MainWindow mainWindow, ObservableCollection<TreeItem> mainTreeItems)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _mainTreeItems = mainTreeItems;

            // 预加载主窗口已有的目录（深拷贝为新的 FolderItem 树）
            foreach (var item in mainTreeItems)
            {
                if (item is FolderItem folder)
                {
                    _vm.RightFolders.Add(CloneFolder(folder));
                }
            }

            rightTreeView.ItemsSource = _vm.RightFolders;
            leftTreeView.ItemsSource = _vm.LeftFolders;

            _vm.PreviewQueue.CollectionChanged += PreviewQueue_CollectionChanged;
        }

        private FolderItem CloneFolder(FolderItem src)
        {
            var folder = new FolderItem
            {
                Name = src.Name,
                Path = src.Path,
                IsExpanded = src.IsExpanded,
                HasSvgFiles = src.HasSvgFiles
            };
            foreach (var child in src.Children)
            {
                if (child is SvgFileItem file)
                {
                    folder.Children.Add(new SvgFileItem
                    {
                        FileName = file.FileName,
                        FullPath = file.FullPath
                    });
                }
            }
            return folder;
        }

        private void PreviewQueue_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _vm.CurrentPreviewFile = _vm.PreviewQueue.FirstOrDefault();
                if (_vm.CurrentPreviewFile != null)
                {
                    txtCurrentPreview.Text = $"首个文件: {_vm.CurrentPreviewFile.FileName}";
                }
                else
                {
                    txtCurrentPreview.Text = "(无)";
                }
            });
        }

        /// <summary>
        /// 双击右侧文件：移到左侧
        /// </summary>
        private void RightTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var file = GetHitSvgFile(e, rightTreeView);
            if (file == null) return;

            // 从右侧移除
            RemoveFileFromFolders(_vm.RightFolders, file.FullPath);

            // 添加到左侧（按文件夹分组）
            AddFileToFolders(_vm.LeftFolders, file);
            _vm.RefreshPreview();

            e.Handled = true;
        }

        /// <summary>
        /// 双击左侧文件：移回右侧
        /// </summary>
        private void LeftTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var file = GetHitSvgFile(e, leftTreeView);
            if (file == null) return;

            // 从左侧移除
            RemoveFileFromFolders(_vm.LeftFolders, file.FullPath);

            // 添加回右侧
            AddFileToFolders(_vm.RightFolders, file);
            _vm.RefreshPreview();

            e.Handled = true;
        }

        /// <summary>
        /// 通过鼠标双击位置获取命中的 SvgFileItem
        /// </summary>
        private SvgFileItem? GetHitSvgFile(MouseButtonEventArgs e, TreeView tree)
        {
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null)
            {
                if (hit is FrameworkElement fe && fe.DataContext is SvgFileItem file)
                {
                    return file;
                }
                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            }
            return null;
        }

        private void RemoveFileFromFolders(ObservableCollection<TreeItem> folders, string fullPath)
        {
            foreach (var folder in folders.OfType<FolderItem>())
            {
                var match = folder.Children.OfType<SvgFileItem>()
                    .FirstOrDefault(f => f.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    folder.Children.Remove(match);
                    return;
                }
            }
        }

        private void AddFileToFolders(ObservableCollection<TreeItem> folders, SvgFileItem file)
        {
            var dir = Path.GetDirectoryName(file.FullPath);
            if (string.IsNullOrEmpty(dir)) return;

            FolderItem? folder = folders.OfType<FolderItem>()
                .FirstOrDefault(f => f.Path.Equals(dir, StringComparison.OrdinalIgnoreCase));
            if (folder == null)
            {
                folder = new FolderItem
                {
                    Name = Path.GetFileName(dir),
                    Path = dir,
                    IsExpanded = true
                };
                folders.Add(folder);
            }

            // 避免重复
            if (folder.Children.OfType<SvgFileItem>().Any(f =>
                f.FullPath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            folder.Children.Add(new SvgFileItem
            {
                FileName = file.FileName,
                FullPath = file.FullPath
            });
        }

        /// <summary>
        /// 打开 SVG 按钮：选择文件夹，把该目录下的所有 svg 加入右侧目录；
        /// 同步把目录加入主窗口。
        /// </summary>
        private void OpenSvg_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "SVG文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
                Multiselect = false,
                Title = "选择SVG文件（将加载同目录下所有svg）"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var selectedFile = openFileDialog.FileName;
                var directory = Path.GetDirectoryName(selectedFile);
                if (string.IsNullOrEmpty(directory)) return;

                var svgFiles = Directory.GetFiles(directory, "*.svg", SearchOption.TopDirectoryOnly);

                // 同步到主窗口目录
                MainWindow.AddDirectoryToFolders(_mainTreeItems, svgFiles);

                // 加载到比对面板右侧
                MainWindow.AddDirectoryToFolders(_vm.RightFolders, svgFiles);
            }
        }

        /// <summary>
        /// 开始比对：在主界面新建一个SVG文件，内容为左侧目录中的第一个SVG文件。
        /// </summary>
        private void StartCompare_Click(object sender, RoutedEventArgs e)
        {
            var firstFile = _vm.PreviewQueue.FirstOrDefault();
            if (firstFile == null)
            {
                MessageBox.Show("左侧目录中没有文件，请先双击右侧文件移入左侧。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "SVG文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
                Title = "新建比对SVG文件",
                FileName = $"compare_{firstFile.FileName}",
                DefaultExt = ".svg"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 读取左侧第一个文件的内容
                    var svgContent = File.ReadAllText(firstFile.FullPath);
                    // 写入新文件
                    File.WriteAllText(saveFileDialog.FileName, svgContent);

                    // 把目录加入主窗口树
                    var directory = Path.GetDirectoryName(saveFileDialog.FileName);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        var svgFiles = Directory.GetFiles(directory, "*.svg", SearchOption.TopDirectoryOnly);
                        MainWindow.AddDirectoryToFolders(_mainTreeItems, svgFiles);
                    }

                    // 在主窗口加载新文件
                    _mainWindow.LoadSvgFile(saveFileDialog.FileName);

                    MessageBox.Show($"比对SVG文件已创建：{saveFileDialog.FileName}", "创建成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建比对文件失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportResult_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导出功能尚未实现。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
