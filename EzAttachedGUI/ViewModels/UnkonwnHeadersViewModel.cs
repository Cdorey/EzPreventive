using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EzAttachedGUI.ViewModels
{
    public class UnknownHeadersViewModel : INotifyPropertyChanged
    {
        private int headerIndex = -1;
        private string header;

        public UnknownHeadersViewModel(string header)
        {
            this.header = header ?? throw new ArgumentNullException(nameof(header), "Header cannot be null");
        }

        public string Header
        {
            get => header;
            set
            {
                if (header != value)
                {
                    header = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int HeaderIndex
        {
            get => headerIndex;
            set
            {
                if (headerIndex != value)
                {
                    headerIndex = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(IsNew));
                }
            }
        }

        public bool IsNew
        {
            get => HeaderIndex == -1;
            set
            {
                int newIndex = value ? -1 : 0;
                if (HeaderIndex != newIndex)
                {
                    HeaderIndex = newIndex;
                }
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
