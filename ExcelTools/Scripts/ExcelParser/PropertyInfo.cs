using NPOI.SS.UserModel;

public class PropertyInfo
{
    /// <summary>
    /// 第0行
    /// </summary>
    public bool isServerProperty;
    /// <summary>
    /// 第1行
    /// </summary>
    public string cname;
    /// <summary>
    /// 第2行
    /// </summary>
    public string ename;
    /// <summary>
    /// 第3行
    /// </summary>
    public string type;

    public PropertyInfo(string row0, string row1, string row2, string row3)
    {
        isServerProperty = row0 == "1";
        cname = row1;
        ename = row2;
        type = row3;
    }
}
