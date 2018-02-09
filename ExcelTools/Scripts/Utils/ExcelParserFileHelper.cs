using ExcelTools.Scripts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class ExcelParserFileHelper
{
    static List<string> donot_copy_files = new List<string>() { "Table_Activity.txt", "Table_ActivityStep.txt", "Table_Timer.txt", "Table_Laboratory.txt", "Table_StrayCats.txt", "Table_SuperAI.txt", "Table_SystemChat.txt", "Table_SealMonster.txt", "Table_Org.txt", "Table_OperateReward.txt", "Table_MonsterSkill.txt", "Table_MonsterEvolution.txt", "Table_MonsterEmoji.txt", "Table_MapSky.txt", "Table_MapHuntTreasure.txt" };
    static List<string> donot_delete_files = new List<string>() { "MenuUnclock" };

    static string _TextExt = ".txt";
    static string _ExcelExt = ".xlsx";
    static string _ClientExt = _TextExt;
    static string _ServerExt = ".lua";
    static string temp_server_table_path = "temp/server";
    static string temp_client_table_path = "temp/client/Config";
    static string target_server_table_path = "../Lua/Table";
    static string target_client_table_path = "../../client-refactory/Develop/Assets/Resources/Script/Config";
    static string target_client_other_path = "../../client-refactory/Develop/Assets/Resources/Script/MConfig";
    string target_client_other_path_old = "../../client-refactory/Develop/Assets/Resources/Script/FrameWork/Config";
    string target_client_script_path = "../../client-refactory/Develop/Assets/Resources/Script/";

    public static void RemakePath(string path)
    {
        if (Directory.Exists(path))
        {
            RemoveAllFileExceptMeta(path);
        }
        else if (File.Exists(path))
        {
            //pass
        }
        else
            Directory.CreateDirectory(path);
    }

    public static bool isDoNotCopyFile(string fname)
    {
        return donot_copy_files.IndexOf(fname) > -1;
    }

    public static string GenTargetFilePath(string path)
    {
        return FileUtil.PathCombine(target_client_table_path, path);
    }

    private static void RemoveAllFileExceptMeta(string root)
    {
        List<string> files = FileUtil.CollectFolderExceptExt(root, ".meta");
        for (int i = 0; i < files.Count; i++)
            File.Delete(files[i]);
    }

    /// <summary>
    /// 获取文件MD5值
    /// </summary>
    /// <param name="fileName">文件绝对路径</param>
    /// <returns>MD5值</returns>
    public static string GetMD5HashFromFile(string fileName)
    {
        try
        {
            FileStream file = new FileStream(fileName, FileMode.Open);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
        }
    }

    static char[] _EscapeArr = new char[] { ' ', '\r', '\n', '\t' };

    public static string ReadTargetFileMD5Hash(string path)
    {
        string md5 = null;
        if (File.Exists(path))
        {
            using (StreamReader sr = new StreamReader(path))
            {
                string line = sr.ReadLine().Trim(_EscapeArr);
                md5 = line.Substring(7);
            }
        }
        return md5;
    }

    public static bool IsSameFileMD5(string path, string newMd5)
    {
        bool ret = false;
        if (File.Exists(path))
        {
            using (StreamReader sr = new StreamReader(path))
            {
                string line = sr.ReadLine();
                ret = line.IndexOf(newMd5) > -1;
            }
        }
        return ret;
    }

    public static string GetTargetLuaPath(string path, bool isServer)
    {
        string fname = Regex.Replace(Path.GetFileNameWithoutExtension(path), "[^a-zA-Z0-9_]", "_");
        fname = string.Format("Table_{0}{1}", fname, _TextExt);
        string targetPath = string.Empty;
        if (isServer)
            targetPath = FileUtil.PathCombine(GlobalCfg.SourcePath, target_server_table_path, fname);
        else if (path.IndexOf("SubConfigs") > -1)
        {
            string dir = path.Substring(path.IndexOf("SubConfigs")+ 11, path.LastIndexOf("/") - (path.IndexOf("SubConfigs") + 11) + 1);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                dir = "Config_" + dir;
                targetPath = target_client_table_path.Replace("Config", dir);
            }
            targetPath = FileUtil.PathCombine(GlobalCfg.SourcePath, targetPath, fname);
        }
        else
            targetPath = FileUtil.PathCombine(GlobalCfg.SourcePath, target_client_table_path, fname);
        return targetPath;
    }

    public static string GetTempLuaPath(string path, bool isServer)
    {
        string fname = Regex.Replace(Path.GetFileNameWithoutExtension(path), "[^a-zA-Z0-9_]", "_");
        fname = string.Format("Table_{0}{1}", fname, _TextExt);
        string tempPath = string.Empty;
        if (isServer)
            tempPath = FileUtil.PathCombine(GlobalCfg.SourcePath, temp_server_table_path, fname);
        else if (path.IndexOf("SubConfigs") > -1)
        {
            int idx = path.IndexOf("SubConfigs");
            if (idx > -1)
            {
                string dir = "Config_" + path.Substring(idx);
                tempPath = temp_client_table_path.Replace("Config", dir);
            }
            tempPath = FileUtil.PathCombine(GlobalCfg.SourcePath, tempPath, fname);
        }
        else
            tempPath = FileUtil.PathCombine(GlobalCfg.SourcePath, temp_client_table_path, fname);
        return tempPath;
    }
}
