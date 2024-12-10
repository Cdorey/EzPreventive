using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace EzAttached
{
    public class AttachableTable : Dictionary<string, List<string>>
    {
        public string PrimaryKey { get; }

        public List<string> Columns { get; } = [];

        public AttachableTable(string primaryKey)
        {
            PrimaryKey = primaryKey;
        }

        public AttachableTable()
        {
            PrimaryKey = string.Empty;
        }

        public void ToExcel(string filePath)
        {
            // 创建工作簿和工作表
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Sheet1");

            // 创建表头行
            IRow headerRow = sheet.CreateRow(0);
            for (int i = 0; i < Columns.Count; i++)
            {
                ICell cell = headerRow.CreateCell(i);
                cell.SetCellValue(Columns[i]); // 设置表头值
            }

            // 创建数据行
            int rowIndex = 0;
            foreach (var row in Values)
            {
                IRow dataRow = sheet.CreateRow(rowIndex + 1); // 数据行从第2行开始
                for (int colIndex = 0; colIndex < row.Count; colIndex++)
                {
                    ICell cell = dataRow.CreateCell(colIndex);
                    cell.SetCellValue(row[colIndex]); // 设置单元格值
                }
                rowIndex++;
            }

            // 保存文件
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        public void Attach(List<string> headers, IEnumerable<List<string>> rows)
        {
            foreach (string header in headers)
            {
                if (!Columns.Contains(header))
                {
                    Columns.Add(header);
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
                Console.WriteLine($"正在载入第{index}行数据");
                string? pk;
                if (pkIndex >= 0)
                {
                    pk = row[pkIndex];
                }
                else
                {
                    do
                    {
                        pk = Guid.NewGuid().ToString();
                    } while (ContainsKey(pk));
                    pk = Guid.NewGuid().ToString();
                }

                var oldData = ContainsKey(pk) ? this[pk] : null;
                var data = new List<string>();
                var indexOfColumns = 0;
                foreach (var column in Columns)
                {
                    var oldValue = indexOfColumns >= data.Count ? string.Empty : data[indexOfColumns];
                    var i = headers.IndexOf(column);
                    data.Add((i == -1 || i >= row.Count) ? oldValue : row[i]);
                    indexOfColumns++;
                }
                this[pk] = data;
            }
        }
    }
}
