using System;
using System.Drawing;
using System.Drawing.Printing;

namespace EzNutrition.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            // 创建打印文档对象
            PrintDocument printDocument = new PrintDocument();

            // 设置打印机选择（可省略）
            //printDocument.PrinterSettings.PrinterName = "你的打印机名称";

            // 设置纸张大小为 A4
            printDocument.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);

            printDocument.DefaultPageSettings.Landscape = true;

            // 添加 PrintPage 事件处理程序
            printDocument.PrintPage += new PrintPageEventHandler(PrintDocument_PrintPage);

            // 开始打印
            printDocument.Print();
        }

        private static void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            // 获取打印区域的矩形（这里假设你希望将文字打印在 A4 纸的左上角，距离上边缘和左边缘各 100 像素）
            RectangleF printArea = new RectangleF(100, 100, e.PageBounds.Width - 200, e.PageBounds.Height - 200);

            // 定义要打印的文字内容
            string textToPrint = "这是要打印的文字内容。";

            // 定义字体和格式
            Font font = new Font("Arial", 12);
            StringFormat stringFormat = new StringFormat();

            // 设置文字格式居中对齐
            stringFormat.Alignment = StringAlignment.Center;
            stringFormat.LineAlignment = StringAlignment.Center;

            // 使用 Graphics 对象绘制文字
            e.Graphics.DrawString(textToPrint, font, Brushes.Black, printArea, stringFormat);
        }
    }
}