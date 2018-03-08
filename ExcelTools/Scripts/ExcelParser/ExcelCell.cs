using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ExcelCell
{
    private int index;
    private string content;
    public PropertyInfo propertyInfo { get; private set; }
    public ExcelRow parent { get; private set; }
    public ExcelCell(ExcelRow p, string con, PropertyInfo info)
    {
        parent = p;
        content = con;
        propertyInfo = info;
    }

    public override string ToString()
    {
        string str = GetValue();
        if (str != null)
            return propertyInfo.ename + " = " + GetValue();
        else
            return null;
        //return string.Format("{0} = {1}", propertyInfo.ename, getvalue());
    }

    public string ToString(bool outforClient)
    {
        string str = GetValue(outforClient);
        if (str != null)
            return propertyInfo.ename + " = " + GetValue(outforClient);
        else
            return null;
    }

    public string GetValue(bool outforClient = false)
    {
        //C#中拼接字符串，固定表达式a + b + c会被优化成string.Concat(new string[]{ a, b, c })
        //性能最好
        string ret = null;
        switch (propertyInfo.type)
        {
            case "number":
                int n;
                float f;
                if (!string.IsNullOrEmpty(content) && int.TryParse(content, out n))
                    ret = n.ToString();
                else if (content.IndexOf('.') > 0 && float.TryParse(content, out f))
                    ret = f.ToString();
                //else
                //    ret = null;
                break;
            case "string":
                ret = content.Replace(@"\\", @"\\\\");
                ret = ret.Replace(@"\\\\n", @"\\n");
                ret = "'" + ret + "'";
                //tmp = string.Format("'{0}'", tmp);
                break;
            case "bittable":
                if (!string.IsNullOrWhiteSpace(content))
                {
                    int num = 0;
                    string[] bits = content.Split(',');
                    int bit;
                    for (int i = 0; i < bits.Length; i++)
                    {
                        if (int.TryParse(bits[i].Trim(), out bit))
                            num += 1 << (bit - 1);
                    }
                    ret = num.ToString();
                }
                break;
            case "table":
                if (!parent.parent.isServerTable && string.IsNullOrEmpty(content))
                    ret = outforClient? "_EmptyTable" : "{}";
                else
                    ret = "{" + content + "}";
                    //tmp = string.Format("{{{0}}}", content);
                break;
            default:
                break;
        }
        return ret;
    }
}
