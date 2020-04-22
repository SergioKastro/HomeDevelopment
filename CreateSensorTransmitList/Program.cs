using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateSensorTransmitList
{
    class Program
    {
        static void Main(string[] args)
        {
        }


        public void ReadSample()
        {
            Excel.Application excelApp = new Excel.Application();
            if (excelApp != null)
            {
                Excel.Workbook excelWorkbook = excelApp.Workbooks.Open(@"C:\test.xls", 0, true, 5, "", "", true, Excel.XlPlatform.xlWindows, "\t", false, false, 0, true, 1, 0);
                Excel.Worksheet excelWorksheet = (Excel.Worksheet)excelWorkbook.Sheets[1];

                Excel.Range excelRange = excelWorksheet.UsedRange;
                int rowCount = excelRange.Rows.Count;
                int colCount = excelRange.Columns.Count;

                for (int i = 1; i <= rowCount; i++)
                {
                    for (int j = 1; j <= colCount; j++)
                    {
                        Excel.Range range = (excelWorksheet.Cells[i, 1] as Excel.Range);
                        string cellValue = range.Value.ToString();

                        //do anything
                    }
                }

                excelWorkbook.Close();
                excelApp.Quit();
            }
        }
    }
}
