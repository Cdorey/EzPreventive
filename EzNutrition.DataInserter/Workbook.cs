using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace EzNutrition.DataInserter
{
    public class Workbook : IDisposable
    {
        private bool disposedValue;
        private IWorkbook workbook;
        private readonly FileStream _fileStream;

        public List<string> Headers => ReadExcelHeader(workbook.GetSheetAt(0));
        public IEnumerable<List<string>> Rows => ReadRows();

        public Workbook(string filePath)
        {
            _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            workbook = ReadExcelFile(_fileStream, filePath);
        }

        static IWorkbook ReadExcelFile(FileStream fs, string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return new XSSFWorkbook(fs);
            }
            else if (Path.GetExtension(filePath).Equals(".xls", StringComparison.OrdinalIgnoreCase))
            {
                return new HSSFWorkbook(fs);
            }
            else
            {
                throw new NotSupportedException("不支持的文件格式");
            }
        }

        static List<string> ReadExcelHeader(ISheet sheet)
        {
            var headers = new List<string>();
            IRow headerRow = sheet.GetRow(0);
            if (headerRow != null)
            {
                for (var i = 0; i < headerRow.LastCellNum; i++)
                {
                    ICell cell = headerRow.GetCell(i);
                    headers.Add(cell?.ToString() ?? string.Empty);
                }
            }
            return headers;
        }

        public IEnumerable<List<string>> ReadRows(int startRow = 1)
        {
            ISheet sheet = workbook.GetSheetAt(0);
            for (var rowIndex = startRow; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (row == null) continue;

                var rowData = new List<string>();
                for (var colIndex = 0; colIndex < row.LastCellNum; colIndex++)
                {
                    ICell cell = row.GetCell(colIndex);
                    rowData.Add(cell?.ToString() ?? string.Empty);
                }
                yield return rowData;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    (workbook as IDisposable)?.Dispose();
                    _fileStream.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}

