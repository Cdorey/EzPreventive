using AdonisUI.Controls;
using EzASD.ViewModels;
using System.Windows;

namespace EzASD
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel((Application.Current as App)!.DbContext);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AdonisUI.Controls.MessageBox.Show(this, "这只是一个辅助小工具，其实主要还是靠你自己", "EzASD", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information, AdonisUI.Controls.MessageBoxResult.OK);
        }
    }
}
