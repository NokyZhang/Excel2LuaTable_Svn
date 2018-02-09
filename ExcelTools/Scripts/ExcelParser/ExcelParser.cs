using System.IO;
using System.Collections.Generic;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.Data.OleDb;
using System.Data;
using System;
using ExcelTools.Scripts;

class ExcelParser
{
    static List<string> _Folders = new List<string>(){
            "D:/RO/ROTrunk/Cehua/Table/serverexcel",
            "D:/RO/ROTrunk/Cehua/Table/SubConfigs"
    };
    static string _ExcelExt = ".xlsx";
    static string _ClientExt = ".txt";
    static string _ServerExt = ".lua";
    static string _TableImportPath = "Table.txt";
    static string target_server_table_path = "../Lua/Table";
    static string target_client_table_path = "../../client-refactory/Develop/Assets/Resources/Script/Config";

    static List<string> _NeedImportClient = new List<string>();
    static List<string> _NeedImportServer = new List<string>();

    static ExcelParser instance
    {
        get { return new ExcelParser(); }
    }

    //public ExcelParser(string file)
    //{
    //    Application app = new Application();
    //    _Workbook wb = app.Workbooks.Add(file);
    //    List<Worksheet> sheets = new List<Worksheet>();
    //    for (int i = 0; i < wb.Sheets.Count; i++)
    //    {
    //        sheets.Add(wb.Sheets[i] as Worksheet);
    //    }
    //}

    public static IWorkbook Parse(string file)
    {
        FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
        IWorkbook workbook = new XSSFWorkbook(fileStream);
        ISheet mainSheet = workbook.GetSheetAt(0);
        return workbook;
    }

    public static void ParseAll()
    {
        SVNHelper.Update(FileUtil.PathCombine(GlobalCfg.SourcePath, ".."));
        SVNHelper.Update(FileUtil.PathCombine(GlobalCfg.SourcePath, target_client_table_path));
        _NeedImportClient.Clear();
        _NeedImportServer.Clear();
        List<string> files = FileUtil.CollectFolder(GlobalCfg.SourcePath, _ExcelExt, instance.MatchExcelFile);
        #region 生成Table.txt的Client和Server版本
        GenTableImportFile();
        #endregion
        //for (int i = 0; i < files.Count; i++)
        //{
            //_ExcelFiles.Add(new ExcelFileListItem()
            //{
            //    Name = Path.GetFileNameWithoutExtension(files[i]),
            //    Status = "C/S",
            //    ClientServer = "C/S",
            //    FilePath = files[i]
            //});
        //}
    }

    public static void ReGenLuaTable(string xlsxPath)
    {
        Excel excel = GlobalCfg.Instance.GetParsedExcel(xlsxPath);
        string fname = excel.tableName + ".txt";
        string md5 = ExcelParserFileHelper.GetMD5HashFromFile(xlsxPath);
        string contents = "--md5:" + md5 + "\n";
        contents += excel.ToString();
        FileUtil.WriteTextFile(contents, Path.Combine(GlobalCfg.SourcePath, GlobalCfg.LocalTmpTablePath, fname));
    }

    //版本库中最新的表格若未生成配置则生成临时配置
    public static void ParseTemp(string exlPath, string branch)
    {
        SVNHelper.Update(FileUtil.PathCombine(GlobalCfg.SourcePath, ".."));
        SVNHelper.Update(FileUtil.PathCombine(GlobalCfg.SourcePath, target_client_table_path));
        instance.MatchExcelFile(exlPath, null, null);
        GenTableImportFile();
    }

    private static void GenServerVersion(Excel excel, string targetPath, string md5)
    {
        string contents = "--md5:" + md5 + "\n";
        contents += excel.ToString();
        FileUtil.WriteTextFile(contents, targetPath);
    }

    private static void GenClientVersion(Excel excel, string targetPath, string md5)
    {
        string contents = "--md5:" + md5 + "\n";
        contents += excel.ToString();
        FileUtil.WriteTextFile(contents, targetPath);
    }

    private void MatchExcelFile(string path, string relativeDir, string fileNameContainExt)
    {
        string excelmd5 = ExcelParserFileHelper.GetMD5HashFromFile(path);
        string tarPath = ExcelParserFileHelper.GetTargetLuaPath(path, true);
        string tempPath = ExcelParserFileHelper.GetTempLuaPath(path, true);

        //#region 生成一份服务器的配置
        //if (!ExcelParserFileHelper.IsSameFileMD5(tarPath, excelmd5))
        //{
        //    Excel excel = Excel.Parse(latestExlPath, true);
        //    if (excel != null && excel.success)
        //        GenServerVersion(excel, tempPath, excelmd5);
        //}
        //#endregion
        #region 客户端的Excel生成一份客户端的配置
        if (path.IndexOf("serverexcel") < 0)
        {
            //tarPath = ExcelParserFileHelper.GetTargetLuaPath(path, false);
            //if (aimPath == null)
            //{
            //    tempPath = ExcelParserFileHelper.GetTempLuaPath(path, false);
            //}
            //else
            //{
            //    //这里暂时这么处理
            //    tempPath = aimPath;
            //}
            //if (!ExcelParserFileHelper.IsSameFileMD5(tempPath, excelmd5))
            //{
            //    Excel excel = Excel.Parse(latestExlPath, false);
            //    if (excel != null && excel.success)
            //        GenClientVersion(excel, tempPath, excelmd5);
            //}
            Excel excel = Excel.Parse(path, false);
            if (excel != null && excel.success)
                GenClientVersion(excel, tempPath, excelmd5);
            if (NeedAutoImport(path))
                _NeedImportClient.Add(Path.GetFileNameWithoutExtension(tempPath));
        }
        if (NeedAutoImport(path))
            _NeedImportServer.Add(Path.GetFileNameWithoutExtension(tempPath));
        #endregion
    }

    static bool NeedAutoImport(string path)
    {
        return path.IndexOf("not_import") < 0 && path.IndexOf("Debug") < 0;
    }

    static void GenTableImportFile()
    {
        _NeedImportClient.Add("Table_MenuUnclock");
        string server = string.Empty;
        string client = "local _DisableWriteTable = {\n\t__newindex = function ()\n\t\terror(\"Attemp to modify read-only table\")\n\tend\n}\n_EmptyTable = {}\nsetmetatable(_EmptyTable, _DisableWriteTable)\n_DisableWriteTable.__metatable = false\n\n";
        if (_NeedImportServer.Count > 0)
        {
            for (int i = 0; i < _NeedImportServer.Count; i++)
            {
                if (!_NeedImportServer[i].EndsWith("_server"))
                    server += "autoImport('" + _NeedImportServer[i] + "') \n";
            }
            WriteTableImportFile(server);
        }
        if (_NeedImportClient.Count > 0)
        {
            for (int i = 0; i < _NeedImportClient.Count; i++)
            {
                if (!_NeedImportClient[i].EndsWith("_server"))
                    client += "autoImport('" + _NeedImportClient[i] + "') \n";
            }
            WriteTableImportFile(client, false);
        }
        _NeedImportServer.Clear();
        _NeedImportClient.Clear();
    }

    static void WriteTableImportFile(string contents, bool isServer = true)
    {
        string path = ExcelParserFileHelper.GetTempLuaPath(_TableImportPath, isServer);
        FileUtil.WriteTextFile(contents, path);
    }

    public static DataTable GetExcelTableByOleDB(string path)
    {
        try
        {
            DataTable dtExcel = new DataTable();
            //数据表
            DataSet ds = new DataSet();
            //获取文件扩展名
            string fext = Path.GetExtension(path);
            string fname = Path.GetFileName(path);
            //Excel的连接
            OleDbConnection objConn = null;
            switch (fext)
            {
                case ".xls":
                    objConn = new OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + path + ";" + "Extended Properties=\"Excel 8.0;HDR=NO;IMEX=1;\"");
                    break;
                case ".xlsx":
                    objConn = new OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + path + ";" + "Extended Properties=\"Excel 12.0;HDR=NO;IMEX=1;\"");
                    break;
                default:
                    objConn = null;
                    break;
            }
            if (objConn == null)
            {
                return null;
            }
            objConn.Open();
            //获取Excel中所有Sheet表的信息
            System.Data.DataTable schemaTable = objConn.GetOleDbSchemaTable(System.Data.OleDb.OleDbSchemaGuid.Tables, null);
            //获取Excel的第一个Sheet表名
            string tableName = schemaTable.Rows[0][2].ToString().Trim();
            string sqlcmd = "select * from [" + tableName + "]";
            //获取Excel指定Sheet表中的信息
            OleDbCommand objCmd = new OleDbCommand(sqlcmd, objConn);
            OleDbDataAdapter data = new OleDbDataAdapter(sqlcmd, objConn);
            data.Fill(ds, tableName);//填充数据
            objConn.Close();
            //dtExcel即为excel文件中指定表中存储的信息
            dtExcel = ds.Tables[tableName];
            return dtExcel;
        }
        catch(Exception e)
        {
            string error = e.ToString();
            return null;
        }
    }
}
