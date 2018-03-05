using ExcelTools.Scripts;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

public class SVNHelper
{
    public const string STATE_ADDED = "added";
    public const string STATE_DELETED = "deleted";
    public const string STATE_MODIFIED = "modified";
    public const string STATE_CONFLICT = "conflict";
    public const string STATE_LOCKED = "locked";

    /// <summary>
    /// arg0 = path
    /// arg1 2 3... = other arguments
    /// command = svn update arg0 otherargs
    /// </summary>
    /// <param name="args"></param>
    public static void Update(params string[] args)
    {
        string arguments = "update " + string.Join(" ", args);
        CommandHelper.ExcuteCommand("svn", arguments);
    }

    public static void Revert(params string[] args)
    {
        string arguments = "revert " + string.Join(" ", args);
        CommandHelper.ExcuteCommand("svn", arguments);
    }

    /// <summary>
    /// 锁定文件，一次只允许锁一个文件
    /// </summary>
    private static bool SVNLock(string path,string message = null)
    {
        string arguments = "lock -m " + "\"" + message + "\" " + path;
        string info = CommandHelper.ExcuteCommand("svn", arguments, true);
        //TODO:这里判断不知道是否严谨，待验证
        if (info.Contains("warning"))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    private static void SVNReleaseLock(string path)
    {
        string arguments = "unlock " + path;
        CommandHelper.ExcuteCommand("svn", arguments);
    }

    public static void Commit(params string[] args)
    {
        string arguments = "commit " + string.Join(" ", args);
        CommandHelper.ExcuteCommand("svn", arguments);
    }

    /// <summary>
    /// 获取目标路径最新改动的版本号
    /// </summary>
    /// <param name="args">需包含本地路径或URL，本地路径获得本地Revision，URL获得服务器最新Revision</param>
    public static string GetLastestReversion(params string[] args)
    {
        string rev = "";
        string arguments = "info " + string.Join(" ", args);
        string info = CommandHelper.ExcuteCommand("svn", arguments, true);
        string[] infoArray = info.Split('\n', '\r');
        foreach (string str in infoArray)
        {
            if (str.StartsWith("Revision:"))
            {
                rev =str.Split(' ')[1];
            }
        }
        return rev;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="args">需包含版本号,目标路径</param>
    public static string Differ(params string[] args)
    {
        string arguments = "diff -r" + string.Join(" ", args);
        string output = CommandHelper.ExcuteCommand("svn", arguments, true);
        return null;
    }

    /// <summary>
    /// 获取服务器上指定文件的最新拷贝
    /// </summary>
    public static void CatFile(string fileUrl, string aimPath, bool setHidden)
    {
        string arguments = "/C svn cat " + fileUrl + " > " + aimPath;
        CommandHelper.ExcuteCommand("cmd", arguments);
        if (setHidden)
        {
            FileUtil.SetHidden(aimPath, true);
        }
    }

    #region 原始Excel及相关配置的捆绑操作

    public struct FileStatusStr
    {
        public string exlSourceNm;
        public bool isSame;
        public bool isLock;
        public List<string> paths;
    }

    public static Dictionary<string, FileStatusStr> AllStatus()
    {
        Dictionary<string, FileStatusStr> dic = new Dictionary<string, FileStatusStr>();
        Dictionary<string, string[]> strDic = Status(GlobalCfg.SourcePath + "/..");
        foreach(KeyValuePair<string, string[]> kv in strDic)
        {
            string fileName = Path.GetFileNameWithoutExtension(kv.Key);
            string excelName = fileName.Contains("Table_") ? 
                fileName.Substring(fileName.IndexOf("_") + 1) : 
                fileName;
            bool islock = false;
            bool issame = true;
            if (kv.Value[1] == STATE_LOCKED)
            {
                GlobalCfg.Instance.LockedPaths.Add(kv.Key);
            }
            if (kv.Value[0] != "") {
                issame = false;
            }
            if (islock || !issame)
            {
                if (!dic.ContainsKey(excelName))
                {
                    dic[excelName] = new FileStatusStr
                    {
                        exlSourceNm = excelName,
                        isSame = issame,
                        isLock = islock,
                        paths = new List<string>()
                    };
                }
                if(!issame)
                    dic[excelName].paths.Add(kv.Key);
            }
        }
        return dic;
    }

    //不仅仅是revert操作,将所有状态恢复
    public static void RevertAll(List<string> paths)
    {
        for(int i = 0; i < paths.Count; i++)
        {
            Dictionary<string, string[]> dic = Status(paths[i]);
            string state = dic[paths[i]][0];
            switch (state)
            {
                case STATE_MODIFIED:
                    Revert(paths[i]);
                    break;
                case STATE_ADDED:
                    File.Delete(paths[i]);
                    break;
                case STATE_DELETED:
                    Update(paths[i]);
                    break;
                default:
                    break;
            }
        }
    }

    public static bool IsLockAll(string exlNm)
    {
        List<string> paths = GetAllPaths(exlNm);
        for(int i = 0; i < paths.Count; i++)
        {
            if (!IsLockedByMe(paths[i]))
            {
                return false;
            }
        }
        return true;
    }

    public static bool RequestEdit(string exlpath)
    {
        if (!File.Exists(exlpath))
            return false;
        List<string> paths = GetAllPaths(Path.GetFileNameWithoutExtension(exlpath));
        paths.Add(exlpath);
        Stack<string> haslockPaths = new Stack<string>();
        for (int i = 0; i < paths.Count; i++)
        {
            if (!IsLockedByMe(paths[i]))
            {
                if(Lock(paths[i], "请求锁定" + paths[i]))
                {
                    haslockPaths.Push(paths[i]);
                }
                else
                {
                    string message = SVNHelper.LockInfo(exlpath);
                    string caption = "无法进入编辑状态";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    MessageBox.Show(message, caption, buttons);
                    break;
                }
            }
            else
            {
                haslockPaths.Push(paths[i]);
            }
            //成功进入编辑状态，继续持有锁
            if (i == paths.Count - 1)
            {
                return true;
            }
        }

        //未成功进入编辑状态，释放锁
        for(int i = 0; i < haslockPaths.Count; i++)
        {
            Release(haslockPaths.Pop());
        }

        return false;
    }

    public static List<string> GetAllPaths(string exlNmWithoutExt)
    {
        List<string> paths = new List<string>();
        FileUtil.FindFile(GlobalCfg.SourcePath + "/../TmpTable", exlNmWithoutExt, ref paths);
        string luaNm = "Table_" + exlNmWithoutExt + GlobalCfg._Local_Table_Ext;
        FileUtil.FindFile(GlobalCfg.SourcePath + "/../TmpTable", luaNm, ref paths);
        return paths;
    }

    #endregion

    #region 加锁解锁的复合操作
    public static bool Lock(string path , string message = null)
    {
        if (SVNLock(path, message))
        {
            GlobalCfg.Instance.LockedPaths.Add(path);
            return true;
        }
        else
        {
            return false;
        }
    }

    public static void Release(string path)
    {
        if (GlobalCfg.Instance.LockedPaths.Contains(path))
        {
            SVNReleaseLock(path);
            GlobalCfg.Instance.LockedPaths.Remove(path);
        }
    }

    public static void ReleaseAll()
    {
        int pathNm = GlobalCfg.Instance.LockedPaths.Count;
        for (int i = 0; i < pathNm; i++)
        {
            Release(GlobalCfg.Instance.LockedPaths[0]);
        }
    }

    //释放与一个Excel相关的所以文件
    public static void ReleaseExcelRelative(string exlpath)
    {
        List<string> paths = GetAllPaths(Path.GetFileNameWithoutExtension(exlpath));
        paths.Add(exlpath);
        for(int i = 0; i < paths.Count; i++)
        {
            Release(paths[i]);
        }
    }
    #endregion

    /// <summary>
    /// 可获得目标文件的状态
    /// </summary>
    /// <param name="args">需包含目标文件的路径</param>
    public static Dictionary<string, string[]> Status(params string[] args)
    {
        string arguments = "status " + string.Join(" ", args);
        string output = CommandHelper.ExcuteCommand("svn", arguments, true);
        string[] statusArray = output.Split('\n', '\r');
        Dictionary<string, string[]> statusDic = new Dictionary<string, string[]>();
        foreach (string str in statusArray)
        {
            if (str != "")
            {
                string[] tmp = str.Split(' ');
                //info数组元素：[0]状态[1]锁定状态[2]文件路径
                string[] info = new string[3] { "", "", "" };
                info[0] = tmp[0];
                info[2] = tmp[tmp.Length -1];
                for (int i = 1; i< tmp.Length -1;i++)
                {
                    if(tmp[i] != "")
                    {
                        info[1] = tmp[i];
                        break;
                    }
                }
                string path = info[2].Replace(@"\","/");
                string key;
                string state = IdentiToState(info[0]);
                string islockbyMe = IdentiToState(info[1]);
                //if (state[1] != "")
                //{
                //    val = IdentiToState(state[1]);
                //}
                if (info[0] != "" || info[1] != "")
                {
                    if (Directory.Exists(path))
                    {
                        List<string> files = FileUtil.CollectFolder(path, ".xlsx");
                        for (int i = 0; i < files.Count; i++)
                        {
                            key = files[i];
                            if (!statusDic.ContainsKey(key))
                            {
                                statusDic.Add(key, new string[2] { state, islockbyMe });
                            }
                        }
                    }
                    else
                    {
                        key = path;
                        if (!statusDic.ContainsKey(key))
                        {
                            statusDic.Add(key, new string[2] { state, islockbyMe });
                        }
                    }
                }
            }
        }
        return statusDic;
    }

    public static string LockInfo(string path)
    {
        string lockInfo = null;
        string arguments = "info " + PathToUrl(path);
        string info = CommandHelper.ExcuteCommand("svn", arguments, true);
        string[] infoArray = info.Split('\n', '\r');
        foreach (string str in infoArray)
        {
            if (str.StartsWith("Lock"))
            {
                lockInfo += str + "\n";
            }
        }
        return lockInfo;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path">只能填文件的本地路径</param>
    public static bool IsLockedByMe(string path)
    {
        string arguments = "info " + path;
        string info = CommandHelper.ExcuteCommand("svn", arguments, true);
        if(info.Contains("Lock Owner"))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static string IdentiToState(string identifier)
    {
        switch (identifier)
        {
            case "!":
                return STATE_DELETED;
            case "?":
                return STATE_ADDED;
            case "M":
                return STATE_MODIFIED;
            case "C":
                return STATE_CONFLICT;
            case "K":
                return STATE_LOCKED;
            default:
                return identifier;
        }
    }

    public static string PathToUrl(string path)
    {
        string url = "svn://svn.sg.xindong.com/RO/client-trunk/" + path.Substring(path.IndexOf("Cehua"));
        return url;
    }
} 
