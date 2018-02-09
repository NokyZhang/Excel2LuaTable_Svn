using System;
using NPOI.SS.UserModel;
using System.Collections.Generic;
using System.IO;
using NPOI.XSSF.UserModel;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.HSSF.UserModel;
using NPOI.OpenXml4Net.OPC;
using NPOI.POIFS.FileSystem;
using System.Data;

public class Excel
{
    public ISheet mainSheet;
    public List<ExcelRow> rows = new List<ExcelRow>();
    public bool isServerTable = false;
    private List<PropertyInfo> _Properties = new List<PropertyInfo>();
    private int _PropertyNums = -1;
    public string tableName { get; private set; }
    public string path { get; private set; }
    bool _Success = false;
    public bool success { get { return _Success; } }
    private int m_nPropertyNums
    {
        get
        {
            if(mainSheet == null)
            {
                Console.Error.WriteLine("mainSheet 为空！");
                return _PropertyNums;
            }
            if(_PropertyNums < 0)
                _PropertyNums = Math.Min(RowCropNullCell(mainSheet.GetRow(0)), Math.Min(RowCropNullCell(mainSheet.GetRow(1)), RowCropNullCell(mainSheet.GetRow(2))));
            return _PropertyNums;
        }
    }
    public List<PropertyInfo> Properties
    {
        get
        {
            return _Properties;
        }
    }

    private Excel(ISheet sheet)
    {
        mainSheet = sheet;
    }

    public static Excel Parse(string file, bool _IsServer)
    {
        ISheet sheet = GetMainSheet(file);
        if (sheet != null)
        {
            Excel excel = new Excel(sheet);
            excel.path = file;
            excel.isServerTable = _IsServer;
            excel.SetTableName(file);
            excel.ParsePropertyInfos();
            excel.ParseExcelContents();
            return excel;
        }
        return null;
    }

    public override string ToString()
    {
        if (!success) return string.Empty;
        string str;
        //用+号拼接的字符串分开Add可以略微提升性能，几毫秒级别，为了可读性不做优化。
        List<string> strList = new List<string>();
        strList.Add(tableName + "= {\n");
        for (int i = 0; i < rows.Count; i++)
        {
            str = rows[i].ToString();
            if (str != null)
            {
                if (i == rows.Count - 1)
                    strList.Add("\t" + rows[i].ToString() + "\n");
                else
                    strList.Add("\t" + rows[i].ToString() + ",\n");
            }
        }
        strList.Add("}\nreturn " + tableName);
        return string.Concat(strList.ToArray());
    }

    static ISheet GetMainSheet(string file)
    {
        ISheet sheet = null;
        using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
        {
            try
            {
                sheet = WorkbookFactory.Create(fileStream).GetSheetAt(0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Path = " + file + "\n" + e.ToString());
            }
            //var fileExt = Path.GetExtension(file);
            //if (fileExt == ".xls")
            //{
            //    HSSFWorkbook hssfwb = new HSSFWorkbook(fileStream);
            //    sheet = hssfwb.GetSheetAt(0);
            //}
            //else
            //{
            //}
        }
        return sheet;
    }

    private void SetTableName(string filePath)
    {
        string filename = Path.GetFileNameWithoutExtension(filePath);
        filename = Regex.Replace(filename, "[^a-zA-Z0-9_]", "_");
        tableName = string.Format("Table_{0}", filename);
    }

    private void ParsePropertyInfos()
    {
        // 预先缓存头四行
        IRow _IsServerRow = mainSheet.GetRow(0);
        IRow _CnameRow = mainSheet.GetRow(1);
        IRow _EnameRow = mainSheet.GetRow(2);
        IRow _DataTypeRow = mainSheet.GetRow(3);
        for (int i = 0; i < m_nPropertyNums; i++)
        {
            string _Ename = Regex.Replace(GetCellStr(_EnameRow, i), "[^a-zA-Z0-9_]", "_");
            string _DataType = Regex.Replace(GetCellStr(_DataTypeRow, i), "[^a-zA-Z0-9_]", "_");
            if (!Regex.IsMatch(_Ename, "[a-zA-Z]"))
            {
                _Success = false;
                Console.Error.WriteLine(string.Format("path = {0}的Excel文件第3行第{1}列不含有字母！", path, i));
                return;
            }
            if (i == 0)
                _Ename = "id";
            _Properties.Add(new PropertyInfo(GetCellStr(_IsServerRow, i), GetCellStr(_CnameRow, i), _Ename, _DataType));
            _Success = true;
        }
    }

    private string GetCellStr(IRow row, int idx)
    {
        string ret = string.Empty;
        if (row.Cells.Count > idx)
            ret = row.Cells[idx].ToString();
        return ret;
    }

    private int RowCropNullCell(IRow row)
    {
        int count = row.Cells.Count;
        int idx = count - 1;
        while(string.IsNullOrEmpty(row.Cells[count - 1].ToString()))
        {
            row.Cells.RemoveAt(count - 1);
            count--;
        }
        return count;
    }

    private void ParseExcelContents()
    {
        if (!success) return;
        for (int i = 4; i <= mainSheet.LastRowNum; i++)
        {
            IRow row = mainSheet.GetRow(i);
            if (row != null)
            {
                rows.Add(ParseExcelRow(row, i));
            }
        }
    }

    private ExcelRow ParseExcelRow(IRow r, int idx)
    {
        ExcelRow row = new ExcelRow(idx, this);
        for (int i = 0; i < m_nPropertyNums; i++)
        {
            ExcelCell c = ParseExcelCell(r.GetCell(i), _Properties[i], row);
            row.AppendCell(c);
        }
        return row;
    }

    private ExcelCell ParseExcelCell(ICell c, PropertyInfo info, ExcelRow row)
    {
        string content = c == null ? string.Empty : c.ToString();
        ExcelCell cell = new ExcelCell(row, content, info);
        return cell;
    }
}
