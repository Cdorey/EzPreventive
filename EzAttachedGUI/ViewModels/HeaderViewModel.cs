using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EzAttachedGUI.ViewModels
{
    public class HeaderViewModel : INotifyPropertyChanged
    {
        private string currentHeader;
        private bool isEnableToChangePrimaryKey = false;
        private bool isPrimaryKey = false;

        public bool IsPrimaryKey
        {
            get => isPrimaryKey;
            set
            {
                if (isPrimaryKey != value)
                {
                    isPrimaryKey = value;
                    NotifyPropertyChanged();

                    if (isPrimaryKey)
                    {
                        // 通知其他 HeaderViewModel 实例取消选中
                        OnPrimaryKeySelected?.Invoke(this);
                    }
                }
            }
        }
        public bool CanChangePrimaryKey
        {
            get => isEnableToChangePrimaryKey;
            set
            {
                isEnableToChangePrimaryKey = value;
                NotifyPropertyChanged();
            }
        }

        public string CurrentHeader
        {
            get => currentHeader;
            set
            {
                currentHeader = value;
                NotifyPropertyChanged();
            }
        }

        public ObservableCollection<string> HeaderAlias { get; }

        public HeaderViewModel(IEnumerable<string> headerAlias)
        {
            HeaderAlias = new ObservableCollection<string>(headerAlias);
            currentHeader = HeaderAlias.Last();
        }

        public HeaderViewModel(string header)
        {
            HeaderAlias = [header];
            currentHeader = header;
            OnPrimaryKeySelected += (sender) =>
            {
                if (sender != this)
                {
                    IsPrimaryKey = false; // 取消当前实例的选中状态
                }
            };
        }

        public static event Action<HeaderViewModel>? OnPrimaryKeySelected;


        public event PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
