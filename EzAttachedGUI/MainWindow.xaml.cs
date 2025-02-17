using EzAttachedGUI.ViewModels;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EzAttachedGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel MainWindowViewModel { get; } = new MainWindowViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = MainWindowViewModel;
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            // 检查拖入的数据是否包含文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy; // 改变鼠标指针样式为复制
            }
            else
            {
                e.Effects = DragDropEffects.None; // 不允许的拖放
            }
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (MainWindowViewModel.CanCombineDataRow)
            {
                MessageBox.Show("有未完成的合并任务，请完成合并或撤销后再拖入新文件", "不能新建", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 检查拖入的数据是否包含文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 获取文件路径数组
                string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (File.Exists(filePaths[0]) &&
                   (filePaths[0].EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
                    filePaths[0].EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
                {
                    await MainWindowViewModel.LoadCombineFileAsync(filePaths[0]);
                }
                else
                {
                    MessageBox.Show($"路径“{filePaths[0]}”似乎不是一个正确的文件，本魔法仅支持xls和xlsx", "不能新建", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private async void LoadRowsData(object sender, RoutedEventArgs e)
        {
            try
            {
                await MainWindowViewModel.CombineDataRowsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"合并数据错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OutputData(object sender, RoutedEventArgs e)
        {
            // 创建保存文件对话框
            var saveFileDialog = new SaveFileDialog
            {
                Title = "选择保存文件的位置",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"输出数据{DateTime.Now:yyyy-MM-dd_HHmm}",
                OverwritePrompt = true
            };

            // 打开保存文件对话框
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                string filePath = saveFileDialog.FileName;

                try
                {
                    await MainWindowViewModel.SaveDataFileAsync(filePath);
                    MessageBox.Show($"文件已成功保存到：{filePath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"保存文件时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show($"无权访问文件路径：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelCombineCurrentFile(object sender, RoutedEventArgs e)
        {
            MainWindowViewModel.CancelCombineCurrentFile();
        }
    }
}