using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SvgShow.Models
{
    public class TreeItem : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FolderItem : TreeItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public ObservableCollection<TreeItem> Children { get; set; } = new();
        public bool HasSvgFiles { get; set; }
    }

    public class SvgFileItem : TreeItem
    {
        private string _fileName = "";
        private string _fullPath = "";

        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        public string FullPath
        {
            get => _fullPath;
            set
            {
                _fullPath = value;
                OnPropertyChanged(nameof(FullPath));
            }
        }
    }
}