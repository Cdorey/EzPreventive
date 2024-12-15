using EzAttachedGUI.Models;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EzAttachedGUI.ViewModels
{
    internal class MainWindowViewModel : INotifyPropertyChanged
    {
        private AttachableTable? attachableTable;
        private string coverLetter;
        private Workbook? currentWorkBook;
        private bool initialized = false;
        private bool canCombineDataRow = false;
        private bool canSaveDataFile = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool CanCombineDataRow
        {
            get => canCombineDataRow;
            set
            {
                canCombineDataRow = value;
                NotifyPropertyChanged();
            }
        }

        public bool CanSaveDataFile
        {
            get => canSaveDataFile;
            set
            {
                canSaveDataFile = value;
                NotifyPropertyChanged();
            }
        }

        public string CoverLetter
        {
            get => coverLetter;
            set
            {
                coverLetter = value;
                NotifyPropertyChanged();
            }
        }

        public ObservableCollection<HeaderViewModel> Headers { get; } = [];

        public ObservableCollection<UnknownHeadersViewModel> UnknownHeaders { get; } = [];

        public async Task LoadCombineFileAsync(string filePath)
        {
            CoverLetter = $"正在解析文件的列表头：{filePath}";
            CanSaveDataFile = false;
            if (initialized)
            {
                await CombineNextFileAsync(filePath);
            }
            else
            {
                await InitialCombineTaskAsync(filePath);
            }
        }

        /// <summary>
        /// 根据当前的Header配置，合并当前Excel文件中的数据行
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task CombineDataRowsAsync()
        {
            //检查PK设置
            if (attachableTable == null)
            {
                var pk = Headers.Where(x => x.IsPrimaryKey);
                if (pk.Count() > 1)
                {
                    throw new InvalidOperationException("非法的PK设置");
                }
                attachableTable = new AttachableTable(pk.FirstOrDefault()?.CurrentHeader);
                foreach (var item in Headers)
                {
                    item.CanChangePrimaryKey = false;
                }
                attachableTable.InitialHeaders(Headers.Select(x => x.CurrentHeader).ToList(), true);

                initialized = true;
            }

            //处理新表头
            if (UnknownHeaders.Count > 0)
            {
                Dictionary<string, int> unHeaders = [];
                foreach (var header in UnknownHeaders)
                {
                    unHeaders[header.Header] = header.HeaderIndex;
                }
                attachableTable.SetHeadersIndex(unHeaders);
                UnknownHeaders.Clear();
            }

            Headers.Clear();
            foreach (var item in attachableTable.PrintHeaders())
            {
                Headers.Add(new HeaderViewModel(item.Value));
            }
            var headerIndex = new List<int>(currentWorkBook!.Headers.Count);

            //载入数据
            await Task.Run(() =>
            {
                foreach (var item in currentWorkBook!.Headers)
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        // ==-1 时丢弃这一列的数据
                        headerIndex.Add(-1);
                    }
                    else
                    {
                        headerIndex.Add(attachableTable[item]);
                    }
                }
                attachableTable.Attach(headerIndex, currentWorkBook!.Rows);
                currentWorkBook?.Dispose();
                currentWorkBook = null;
            });

            CoverLetter = "数据引入成功，请向窗口拖入下一个需要合并的excel文件，或者点击“导出文件”按钮完成本次合并任务";
            CanCombineDataRow = false;
            CanSaveDataFile = true;
        }

        public void CancelCombineCurrentFile()
        {
            UnknownHeaders.Clear();
            Headers.Clear();

            if (attachableTable != default)
            {
                foreach (var item in attachableTable.PrintHeaders())
                {
                    Headers.Add(new HeaderViewModel(item.Value));
                }
            }

            currentWorkBook?.Dispose();
            currentWorkBook = null;

            CoverLetter = "撤销引入文件";
            CanCombineDataRow = false;
            CanSaveDataFile = attachableTable != default;
        }

        public async Task SaveDataFileAsync(string filePath)
        {
            await Task.Run(() => attachableTable?.ToExcel(filePath));
            var builder = new StringBuilder();
            builder
                .AppendLine("文件已成功导出！")
                .AppendLine()
                .AppendLine("**重要提示**")
                .AppendLine()
                .AppendLine("为避免 Excel 自动数据转换可能导致的数据错误，导出的数据均以“文本”格式保存。")
                .AppendLine("在进行统计分析之前，请按照以下步骤操作：")
                .AppendLine()
                .AppendLine("1. 根据实际研究设计和业务逻辑，使用 Excel 的【分列功能】将数据转换为符合研究需要的类型。")
                .AppendLine("2. 若不熟悉分列功能，请参考相关教程（建议搜索“Excel 分列功能使用方法”）。")
                .AppendLine()
                .AppendLine("提示：掌握此功能是提升 Excel 数据处理能力的必备技能，有助于准确分析数据。");
            CoverLetter = builder.ToString();
        }

        /// <summary>
        /// 根据第一个excel文件，初始化合并任务
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private async Task InitialCombineTaskAsync(string filePath)
        {
            await Task.Run(() =>
            {
                currentWorkBook?.Dispose();
                currentWorkBook = new Workbook(filePath);
            });

            var builder = new StringBuilder();
            builder
                .AppendLine("第一个数据集加载成功！")
                .AppendLine("请指定索引列，用于在后续数据合并时判断相同记录。如索引相同，新记录将与旧记录合并。")
                .AppendLine("例如，指定“姓名”作为索引：")
                .AppendLine("第一条记录：{姓名:张三, 身高:166, 体重:61, 胸围:94}")
                .AppendLine("第二条记录：{姓名:张三, 身高:166.7, 体重:64, 腰围:87, 臀围:105.6}")
                .AppendLine("合并后：{姓名:张三, 身高:166.7, 体重:64, 胸围:94, 腰围:87, 臀围:105.6}")
                .AppendLine("如果不指定索引列，数据将按顺序垂直追加。\n")
                .AppendLine("确认无误后，请点击“引用数据”按钮。");

            CoverLetter = builder.ToString();

            foreach (var header in currentWorkBook!.Headers)
            {
                //跳过空的表头，这是不允许的
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                Headers.Add(new HeaderViewModel(header)
                {
                    CanChangePrimaryKey = true
                });
            }

            CanCombineDataRow = true;
        }

        /// <summary>
        /// 载入后续Excel文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private async Task CombineNextFileAsync(string filePath)
        {
            await Task.Run(() =>
            {
                currentWorkBook?.Dispose();
                currentWorkBook = new Workbook(filePath);
            });
            var newHeaders = attachableTable!.InitialHeaders(currentWorkBook!.Headers);
            if (newHeaders.Any())
            {
                var builder = new StringBuilder();
                builder
                    .AppendLine("发现新文件中包含未知的列表头：")
                    .AppendLine("如果这些表头是已知表头的别名，请从下拉框中选择对应项；")
                    .AppendLine("如果是新的表头，请留空，系统将自动创建。")
                    .AppendLine("确认无误后，请点击“引用数据”按钮。");
                foreach (var item in newHeaders)
                {
                    UnknownHeaders.Add(new UnknownHeadersViewModel(item));
                }
                CoverLetter = builder.ToString();
                CanCombineDataRow = true;
            }
            else
            {
                CoverLetter = "解析成功，自动引入数据";
                await CombineDataRowsAsync();
            }

        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindowViewModel()
        {
            var builder = new StringBuilder();
            builder.AppendLine("**使用说明**：");
            builder.AppendLine("1. 本程序支持 .xls 或 .xlsx 格式的文件。");
            builder.AppendLine("2. 脚本将默认解析第一个工作表 (Sheet)。");
            builder.AppendLine("3. 文件要求：");
            builder.AppendLine("   - 表格应为原始数据，不应包含公式，不应包含任何合并单元格。");
            builder.AppendLine("   - 第一行必须是表头，且每列的列名不可为空。");
            builder.AppendLine("   - 列名不可重复，否则会导致数据合并冲突。");
            builder.AppendLine("   - 从第二行开始必须为数据区域。");
            builder.AppendLine();
            builder.AppendLine("请将您的 Excel 文件拖入窗口以进行处理。\n");

            builder.AppendLine("**开源声明**：");
            builder.AppendLine("本程序源代码托管于 GitHub：");
            builder.AppendLine("https://github.com/Cdorey/EzPreventive/tree/Version2/EzAttachedGUI");
            builder.AppendLine();
            builder.AppendLine("版权所有 (c) 2024 Cdorey。");
            builder.AppendLine("本程序根据 GNU Affero General Public License v3 (AGPL-3.0) 授权发布。");
            builder.AppendLine("使用本程序时，您需遵守以下规则：");
            builder.AppendLine("1. 必须保留原始作者的版权声明；");
            builder.AppendLine("2. 修改后的程序也必须基于 AGPL v3 开源发布；");
            builder.AppendLine("3. 如果通过网络部署本程序，必须公开其修改后的源代码。");
            builder.AppendLine();
            builder.AppendLine("关于 AGPL v3 的详细信息，请访问：");
            builder.AppendLine("https://www.gnu.org/licenses/agpl-3.0.html\n");

            builder.AppendLine("**免责声明**：");
            builder.AppendLine("本程序按“原样”提供，不附带任何明示或暗示的保证，包括但不限于适销性或特定用途适用性的保证。");
            builder.AppendLine("作者对使用本程序所产生的任何后果概不负责。\n");
            coverLetter = builder.ToString();
        }
    }
}
