using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SvgShow.Models
{
    /// <summary>
    /// 比对面板的视图模型
    /// </summary>
    public class ComparisonViewModel : INotifyPropertyChanged
    {
        /// <summary>右侧候选文件池（按文件夹分组）</summary>
        public ObservableCollection<TreeItem> RightFolders { get; set; } = new();

        /// <summary>左侧已选文件池（按文件夹分组）</summary>
        public ObservableCollection<TreeItem> LeftFolders { get; set; } = new();

        /// <summary>左侧预览队列：所有被选中的文件按加入顺序排列</summary>
        public ObservableCollection<SvgFileItem> PreviewQueue { get; set; } = new();

        private SvgFileItem? _currentPreviewFile;
        /// <summary>当前在 WebView2 中预览的文件（始终是 PreviewQueue 的第一个）</summary>
        public SvgFileItem? CurrentPreviewFile
        {
            get => _currentPreviewFile;
            set
            {
                if (_currentPreviewFile != value)
                {
                    _currentPreviewFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 根据当前 LeftFolders 重建 PreviewQueue，
        /// 并把第一个文件设为 CurrentPreviewFile
        /// </summary>
        public void RefreshPreview()
        {
            PreviewQueue.Clear();
            foreach (var folder in LeftFolders.OfType<FolderItem>())
            {
                foreach (var child in folder.Children.OfType<SvgFileItem>())
                {
                    PreviewQueue.Add(child);
                }
            }
            CurrentPreviewFile = PreviewQueue.FirstOrDefault();
        }
    }
}
