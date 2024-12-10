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
            for (int i = 0; i < headers.Count; i++)
            {
                Console.WriteLine($"{i}: {headers[i]}");
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
            Console.WriteLine("请将第一期数据的 Excel 文件拖入窗口，然后按回车：");
            string? path = GetValidFilePath();

            AttachableTable table;
            using (var wb = new Workbook(path))
            {
                Console.WriteLine("请选择一列作为数据的唯一索引列（输入列号），若无索引列请直接按回车：");
                DisplayHeaders(wb.Headers);
                int? indexColumn = GetIndexColumn(wb.Headers.Count);

                table = indexColumn.HasValue
                    ? new AttachableTable(wb.Headers[indexColumn.Value])
                    : new AttachableTable();

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
                    using (var successor = new Workbook(path))
                    {
                        table.Attach(successor.Headers, successor.Rows);
                    }
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

