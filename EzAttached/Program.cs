using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace EzAttached
{
    internal class Program
    {
        static string GetValidFilePath()
        {
            string path = Console.ReadLine()?.Trim('\"') ?? string.Empty;

            while (!File.Exists(path) ||
                   (!path.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("无效的文件路径，请重新拖入 Excel 文件：");
                path = Console.ReadLine()?.Trim('\"') ?? string.Empty;
            }

            return path;
        }
        static void DisplayHeaders(List<string> headers)
        {
            Console.WriteLine($"索引号\t列表头");
            for (int i = 0; i < headers.Count; i++)
            {
                Console.WriteLine($"{i}\t{headers[i]}");
            }
        }
        static int? GetIndexColumn(int headerCount)
        {
            string? input = Console.ReadLine();

            if (int.TryParse(input, out int index) && index >= 0 && index < headerCount)
            {
                return index;
            }

            Console.WriteLine("未指定有效的索引列，将默认仅汇总数据。");
            return null;
        }
        static void SaveTableToFile(AttachableTable table)
        {
            Console.WriteLine("请输入保存的路径，或直接按回车使用默认路径：");
            string defaultPath = Path.Combine(Syroot.Windows.IO.KnownFolders.Desktop.Path,
                $"数据拼接任务{DateTime.Now:yyyy-MM-dd_hhmm}.xlsx");
            Console.WriteLine($"默认路径为：{defaultPath}");
            Console.Write("路径: ");
            string? inputPath = Console.ReadLine();
            string savedPath = string.IsNullOrWhiteSpace(inputPath) ? defaultPath : inputPath;

            try
            {
                Console.WriteLine("正在写文件，这可能会消耗大量的内存...");
                table.ToExcel(savedPath);
                Console.WriteLine($"文件已成功保存到：{savedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存文件时出错：{ex.Message}");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("本程序源代码发布于：https://github.com/Cdorey/EzPreventive/tree/Version2/EzAttached\n");

            Console.WriteLine("版权所有 (c) 2024 Cdorey。");
            Console.WriteLine("本程序基于 GNU Affero General Public License v3 (AGPL-3.0) 授权发布。");
            Console.WriteLine("您可以自由复制、修改和分发本程序，但需遵循以下条件：");
            Console.WriteLine("- 必须保留原始作者的版权声明；");
            Console.WriteLine("- 必须将任何修改后的程序也基于 AGPL v3 授权开源；");
            Console.WriteLine("- 如果通过网络使用本程序，必须公开其源代码。\n");

            Console.WriteLine("有关 AGPL v3 的详细信息，请访问：https://www.gnu.org/licenses/agpl-3.0.html\n");

            Console.WriteLine("**免责声明**：");
            Console.WriteLine("本程序按“原样”提供，不附带任何明示或暗示的保证，包括但不限于适销性或适用于特定用途的保证。");
            Console.WriteLine("使用本程序所产生的任何后果，作者不承担责任。\n");

            Console.WriteLine("使用说明：");
            Console.WriteLine("可以使用xls或者xlsx文件");
            Console.WriteLine("脚本将尝试解析第一个sheet");
            Console.WriteLine("要求第一行必须是列表头");
            Console.WriteLine("从第二行开始有且只有数据，且不应该有任何合并单元格");
            Console.WriteLine("请将第一期数据的 Excel 文件拖入窗口，然后按回车：");
            string? path = GetValidFilePath();

            AttachableTable table;
            using (var wb = new Workbook(path))
            {
                Console.Clear();
                DisplayHeaders(wb.Headers);
                Console.WriteLine("请指定索引列，后续附加新的数据集时具有相同索引的新记录可以覆盖先前旧的记录");
                Console.WriteLine("例如第一批数据包含{姓名:张三，身高:166，体重:61，胸围:94}");
                Console.WriteLine("第二批数据包含{姓名:张三，身高:166.7，体重:64，腰围:87，臀围:105.6}");
                Console.WriteLine("如果指定了姓名列作为索引，两批数据中张三这一行将会自动合并{姓名:张三，身高:166.7，体重:64，胸围:94，腰围:87，臀围:105.6}");
                Console.WriteLine("如果不指定索引列，将仅垂直追加记录");
                Console.WriteLine("请选择一列作为数据的唯一索引列（输入列号），若无索引列请直接按回车：");
                int? indexColumn = GetIndexColumn(wb.Headers.Count);

                table = indexColumn.HasValue
                    ? new AttachableTable(wb.Headers[indexColumn.Value])
                    : [];

                table.Attach(wb.Headers, wb.Rows);
            }

            // 汇总后续文件
            while (true)
            {
                Console.WriteLine("请继续拖入后续文件，或直接按回车以结束汇总：");
                path = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(path))
                    break;

                try
                {
                    using var successor = new Workbook(path);
                    table.Attach(successor.Headers, successor.Rows);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理文件时出错：{ex.Message}");
                }
            }

            // 保存文件
            SaveTableToFile(table);

            Console.WriteLine("汇总完成，按任意键退出...");
            Console.ReadKey();
        }
    }
}

