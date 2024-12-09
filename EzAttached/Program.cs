using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace EzAttached
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("把第一期数据的excel文件拖入本窗口，然后按回车");
            var path = Console.ReadLine();
            AttachableTable table;
            using (var wb = new Workbook(path!))
            {
                Console.WriteLine("请选择一列作为数据的唯一索引列，追加新数据时如果这一列相同，新追加的行将会覆盖旧的行");
                for (int i = 0; i < wb.Headers.Count; i++)
                {
                    Console.WriteLine($"{i}   {wb.Headers[i]}");
                }
                Console.WriteLine("请输入数字，或者直接回车只汇总数据而不依据索引来使用新数据覆盖旧数据：");
                var index = Console.ReadLine();
                if (int.TryParse(index, out var indexValue))
                {
                    table = new AttachableTable(wb.Headers[int.Parse(index!)]);
                }
                else
                {
                    table = [];
                }
                table.Attach(wb.Headers, wb.Rows);
            }
            GC.Collect();
            Console.WriteLine("请继续拖入后续文件，或者直接按回车以结束汇总");
            path = Console.ReadLine();
            while (!string.IsNullOrEmpty(path))
            {
                using (var successor = new Workbook(path!))
                {
                    table.Attach(successor.Headers, successor.Rows);
                }
                GC.Collect();
                Console.WriteLine("请继续拖入后续文件，或者直接按回车以结束汇总");
                path = Console.ReadLine();
            }
            Console.WriteLine("正在写文件，可能要消耗大量的内存");
            table.ToExcel(@"C:\Users\cdorey\OneDrive\桌面\新建 Microsoft Word 文档.xlsx");
            Console.ReadLine();
        }
    }
}

