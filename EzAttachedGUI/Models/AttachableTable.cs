using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections;
using System.IO;

namespace EzAttachedGUI.Models
{
    public class AttachableTable(string? primaryKey = null)
    {
        private readonly Dictionary<string, string[]> _rows = [];
        private readonly Dictionary<string, int> _headerIndex = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Headers { get; private set; } = [];
        public string? PrimaryKey { get; } = primaryKey;

        public int this[string headerName]
        {
            get => _headerIndex[headerName];
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
            for (var i = 0; i < Headers.Count; i++)
            {
                headerRow.CreateCell(i).SetCellValue(Headers[i]);
            }

            // 写数据
            var rowIndex = 1;
            foreach (var row in _rows.Values)
            {
                var dataRow = sheet.CreateRow(rowIndex++);
                for (var i = 0; i < row.Length; i++)
                {
                    if (!string.IsNullOrEmpty(row[i]))
                    {
                        dataRow.CreateCell(i).SetCellValue(row[i]);
                    }
                }
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        /// <summary>
        /// 初始化列表头
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="isAutoAddHeader">自动添加至Headers</param>
        /// <returns>未知的新表头</returns>
        public IEnumerable<string> InitialHeaders(List<string> headers, bool isAutoAddHeader = false)
        {
            var missedHeaders = headers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !_headerIndex.ContainsKey(x));
            if (missedHeaders.Any() && isAutoAddHeader)
            {
                foreach (var header in missedHeaders)
                {
                    _headerIndex[header] = Headers.Count;
                    Headers.Add(header);
                }
            }

            return missedHeaders;
        }

        public void SetHeadersIndex(Dictionary<string, int> headers)
        {
            foreach (var header in headers)
            {
                if (header.Value == -1)
                {
                    _headerIndex[header.Key] = Headers.Count;
                    Headers.Add(header.Key);
                }
                else
                {
                    _headerIndex[header.Key] = header.Value;
                    Headers[header.Value] = header.Key;
                }
            }
        }

        public Dictionary<int, IEnumerable<string>> PrintHeaders()
        {
            var result = from header in _headerIndex
                         group header by header.Value into grouped
                         select new { grouped.Key, Child = grouped.Select(x => x.Key) };
            return result.ToDictionary(grouped => grouped.Key, grouped => grouped.Child);
        }

        /// <summary>
        /// 向内存表附加新的数据行
        /// </summary>
        /// <param name="headers">数据行的列索引</param>
        /// <param name="rows"></param>
        /// <exception cref="InvalidDataException"></exception>
        public void Attach(List<int> headers, IEnumerable<List<string>> rows)
        {
            var pkIndex = string.IsNullOrEmpty(PrimaryKey) ? -1 : Headers.IndexOf(PrimaryKey);
            if (pkIndex == -1 || headers.Contains(pkIndex))
            {
                foreach (var row in rows)
                {
                    var pk = pkIndex == -1 ? Guid.NewGuid().ToString() : row[headers.IndexOf(pkIndex)];
                    var data = new string[Headers.Count];
                    if (_rows.TryGetValue(pk, out string[]? value))
                    {
                        value.CopyTo(data, 0);
                    }

                    for (var i = 0; i < headers.Count; i++)
                    {
                        //headers[i] == -1时丢弃数据
                        //有时当前行的末尾单元格可能因为没有数据，为null需跳过
                        //跳过没有实际数据的row[i]
                        if (headers[i] != -1 && i < row.Count && !string.IsNullOrWhiteSpace(row[i]))
                        {
                            data[headers[i]] = row[i];
                        }
                    }
                    _rows[pk] = data;
                }
            }
            else
            {
                throw new InvalidDataException("新附加的数据集缺少预期的唯一索引列");
            }
        }
    }
}
