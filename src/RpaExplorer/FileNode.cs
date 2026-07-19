using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace RpaExplorer
{
    // View-model for a node in the archive tree. Replaces the WinForms TreeNode.
    public class FileNode : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;

        // Archive tree path (uses '/'), i.e. the key into Archive.Index for files.
        public string FullPath { get; set; } = string.Empty;

        public bool IsFolder { get; set; }

        // True when the entry is already stored in the loaded archive; false when newly added.
        public bool InArchive { get; set; } = true;

        public FileNode Parent { get; set; }

        public ObservableCollection<FileNode> Children { get; } = new();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        // Green marks entries that are new / unsaved, matching the original.
        public IBrush Foreground => IsChanged ? Brushes.Green : Brushes.Black;

        // Folders get the SandyBrown highlight the original used.
        public IBrush Background => IsFolder
            ? new SolidColorBrush(Color.FromRgb(244, 164, 96))
            : Brushes.Transparent;

        private bool _isChanged;
        public bool IsChanged
        {
            get => _isChanged;
            set { _isChanged = value; OnPropertyChanged(); OnPropertyChanged(nameof(Foreground)); }
        }

        public Bitmap Icon
        {
            get
            {
                if (IsFolder)
                {
                    return Images.FolderIcon;
                }

                return IsChanged ? Images.FileChangedIcon : Images.FileIcon;
            }
        }

        private bool? _isChecked = false;
        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, true, true);
        }

        private void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked)
            {
                return;
            }

            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Children)
                {
                    child.SetIsChecked(_isChecked, true, false);
                }
            }

            if (updateParent)
            {
                Parent?.VerifyCheckState();
            }

            OnPropertyChanged(nameof(IsChecked));
        }

        private void VerifyCheckState()
        {
            bool? state = null;
            for (var i = 0; i < Children.Count; i++)
            {
                var current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }

            SetIsChecked(state, false, true);
        }

        // Depth-first enumeration of this node and all descendants.
        public IEnumerable<FileNode> All()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var sub in child.All())
                {
                    yield return sub;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
