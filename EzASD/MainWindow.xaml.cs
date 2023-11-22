using AdonisUI.Controls;
using EzASD.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            DataContext = new ChildViewModel();

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AdonisUI.Controls.MessageBox.Show(this, "这只是一个辅助小工具，其实主要还是靠你自己", "EzASD", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information, AdonisUI.Controls.MessageBoxResult.OK);
        }
    }
}
