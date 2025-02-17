using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO;

namespace EzAttachedGUI.Models
{
    public class Workbook : IDisposable
    {
        private bool disposedValue;
        private readonly IWorkbook workbook;

        public Workbook(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件未找到: {filePath}");
            }

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            workbook = ReadExcelFile(fileStream, filePath);
        }

        public int SheetCount => workbook.NumberOfSheets;

        public List<string> Headers => ReadExcelHeader(workbook.GetSheetAt(0));

        public IEnumerable<List<string>> Rows => ReadRows();

        private static IWorkbook ReadExcelFile(FileStream fs, string filePath)
        {
            return Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? new XSSFWorkbook(fs)
                : new HSSFWorkbook(fs);
        }

        private static List<string> ReadExcelHeader(ISheet sheet)
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

        private static string GetCellValue(ICell cell)
        {
            if (cell == null) return string.Empty;

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? cell.DateCellValue?.ToString("yyyy-MM-dd") : cell.NumericCellValue.ToString(),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                CellType.Formula => cell.CellFormula,
                _ => string.Empty,
            } ?? string.Empty;
        }

        public IEnumerable<List<string>> ReadRows(int sheetIndex = 0, int startRow = 1)
        {
            ISheet sheet = workbook.GetSheetAt(sheetIndex);
            for (var rowIndex = startRow; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (row == null) continue;

                var rowData = new List<string>();
                for (var colIndex = 0; colIndex < row.LastCellNum; colIndex++)
                {
                    ICell cell = row.GetCell(colIndex);
                    rowData.Add(GetCellValue(cell));
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