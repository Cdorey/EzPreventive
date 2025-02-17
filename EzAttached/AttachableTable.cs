using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace EzAttached
{
    public class AttachableTable(string? primaryKey = null)
    {
        private readonly Dictionary<string, List<string>> _rows = [];
        private readonly Dictionary<string, int> _headerIndex = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Headers { get; private set; } = [];
        public string? PrimaryKey { get; } = primaryKey;

        private void PrintHeaders()
        {
            var headersIndex = _headerIndex.GroupBy(x => x.Value);
            Console.WriteLine("列号\t列名");
            foreach (var header in headersIndex)
            {
                Console.Write(header.Key);
                foreach (var key in header)
                {
                    Console.Write($"\t{key.Key}");
                }
                Console.WriteLine();
            }
        }

        private int? GetIndexColumn()
        {
            string? input = Console.ReadLine();

            if (int.TryParse(input, out int index) && index >= 0 && index < Headers.Count)
            {
                return index;
            }

            Console.WriteLine("列号为空，新建");
            return null;
        }

        /// <summary>
        /// 导出数据表为 Excel 文件
        /// </summary>
        public void ToExcel(string filePath)
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Sheet1");

            // 写表头
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < Headers.Count; i++)
            {
                headerRow.CreateCell(i).SetCellValue(Headers[i]);
            }

            // 写数据
            int rowIndex = 1;
            foreach (var row in _rows.Values)
            {
                var dataRow = sheet.CreateRow(rowIndex++);
                for (int i = 0; i < row.Count; i++)
                {
                    dataRow.CreateCell(i).SetCellValue(row[i]);
                }
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        public void Attach(List<string> headers, IEnumerable<List<string>> rows, bool isAutoAddHeader = false)
        {
            Console.Clear();
            // 初始化头部索引
            var missedHeaders = headers.Where(x => !_headerIndex.ContainsKey(x));
            if (missedHeaders.Any())
            {
                if (isAutoAddHeader)
                {
                    foreach (var header in missedHeaders)
                    {
                        _headerIndex[header] = Headers.Count;
                        Headers.Add(header);
                    }
                }
                else
                {
                    PrintHeaders();
                    Console.WriteLine("新数据表有未知的表头");
                    Console.WriteLine("可以为这些表头指定列号，这样可以将不同的数据表合并，也直接回车可以新建一列");
                    foreach (var header in missedHeaders)
                    {
                        Console.WriteLine($"{header}应当合并到哪一列？");

                        var x = GetIndexColumn();
                        if (x.HasValue)
                        {
                            _headerIndex[header] = x.Value;
                        }
                        else
                        {
                            _headerIndex[header] = Headers.Count;
                            Headers.Add(header);
                        }
                    }
                }
            }

            var pkIndex = string.IsNullOrEmpty(PrimaryKey) ? -2 : headers.IndexOf(PrimaryKey);
            if (pkIndex == -1)
            {
                throw new InvalidDataException("新附加的数据集缺少预期的唯一索引列");
            }

            int index = 0;
            foreach (var row in rows)
            {
                index++;
                Console.Write($"\r正在载入第{index}行数据");
                string? pk = pkIndex >= 0 ? row[pkIndex] : Guid.NewGuid().ToString();
                var data = new string[Headers.Count];
                if (_rows.TryGetValue(pk, out List<string>? value))
                {
                    value.CopyTo(data);
                }

                for (int i = 0; i < headers.Count; i++)
                {
                    int columnIndex = _headerIndex[headers[i]];
                    if (i < row.Count && !string.IsNullOrWhiteSpace(row[i]))
                    {
                        data[columnIndex] = row[i];
                    }
                }

                _rows[pk] = [.. data];
            }
            Console.WriteLine();
        }
    }
}
