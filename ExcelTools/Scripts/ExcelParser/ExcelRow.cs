using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class ExcelRow
{
    public int index;
    public List<ExcelCell> cells = new List<ExcelCell>();
    public Excel parent { get; private set; }
    public ExcelRow(int idx, Excel p)
    {
        index = idx;
        parent = p;
    }

    public void AppendCell(ExcelCell c)
    {
        cells.Add(c);
    }

    /// <summary>
    /// 返回完整的内容
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        string str;
        for(int i = 0; i < cells.Count; i++)
        {
            if (i == 0)
            {
                string id = cells[i].GetValue();
                if (string.IsNullOrWhiteSpace(id))
                    return null;
                sb.AppendFormat("[{0}] = {{", id);
            }
            str = cells[i].ToString(false);
            if (str != null)
            {
                if (i != cells.Count - 1)
                    sb.AppendFormat("{0}, ", str);
                else
                    sb.Append(str);
            }
            if (i == cells.Count - 1)
                sb.Append("}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 返回客户端版本的内容
    /// </summary>
    public string ToStringClient()
    {
        StringBuilder sb = new StringBuilder();
        string str;
        for (int i = 0; i < cells.Count; i++)
        {
            if (i == 0)
            {
                string id = cells[i].GetValue(true);
                if (string.IsNullOrWhiteSpace(id))
                    return null;
                sb.AppendFormat("[{0}] = {{", id);
            }
            if (!cells[i].propertyInfo.isServerProperty)
            {
                str = cells[i].ToString(true);
                if (str != null)
                {
                    if (i != cells.Count - 1)
                        sb.AppendFormat("{0}, ", str);
                    else
                        sb.Append(str);
                }
            }
            if (i == cells.Count - 1)
                sb.Append("}");
        }
        return sb.ToString();
    }

    public string ToStringWithOutIndex()
    {
        StringBuilder sb = new StringBuilder();
        string tmp = string.Format("[{0}] = {{ ", index.ToString());
        for (int i = 0; i < cells.Count; i++)
        {
            //if (i != cells.Count - 1)
            sb.AppendFormat("{0}, ", cells[i].ToString());
        }
        return sb.ToString();
    }
}
