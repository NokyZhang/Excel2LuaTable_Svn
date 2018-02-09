using System.IO;
using System.Collections.Generic;
using System;
using System.Windows.Forms;
using System.Reflection;

class FileUtil
{
    public static List<string> CollectFolder(string folder, string ext)
    {
        List<string> files = new List<string>();
        if (Directory.Exists(folder))
            CollectFile(ref files, folder, new List<string>() { ext }, true);
        return files;
    }

    public static List<string> CollectFolderExceptExt(string folder, string ext)
    {
        List<string> files = new List<string>();
        if (Directory.Exists(folder))
            CollectFileExceptExts(ref files, folder, new List<string>() { ext }, true);
        return files;
    }

    public static List<string> CollectAllFolders(List<string> folders, string ext, Boolean collectHidden = false)
    {
        List<string> files = new List<string>();
        for (int i = 0; i < folders.Count; i++)
        {
            if (Directory.Exists(folders[i]))
                CollectFile(ref files, folders[i], new List<string>() { ext }, true, "", collectHidden);
        }
        return files;
    }

    public static List<string> CollectFolder(string folder, string ext, Action<string, string, string> match)
    {
        List<string> files = new List<string>();
        if (Directory.Exists(folder))
            CollectFile(ref files, folder, new List<string>() { ext }, true, "", false,match);
        return files;
    }

    public static List<string> CollectAllFolders(List<string> folders, List<string> exts)
    {
        List<string> files = new List<string>();
        for (int i = 0; i < folders.Count; i++)
        {
            if (Directory.Exists(folders[i]))
                CollectFile(ref files, folders[i], exts, true);
        }
        return files;
    }

    public static void CollectFile(ref List<string> fileList, string folder, List<string> exts, bool recursive = false, string ppath = "", Boolean collectHidden = false, Action<string, string, string> match = null)
    {
        folder = AppendSlash(folder);
        ppath = AppendSlash(ppath);
        DirectoryInfo dir = new DirectoryInfo(folder);
        FileInfo[] files = dir.GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (exts.Contains(files[i].Extension.ToLower()))//e.g ".txt"
            {
                string fpath = folder + files[i].Name;
                if (!string.IsNullOrEmpty(fpath))
                {
                    FileAttributes attributes = File.GetAttributes(fpath);
                    if(!( ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden) ^ collectHidden) ){
                        fileList.Add(fpath);
                        match?.Invoke(fpath, ppath, files[i].Name);
                    }
                }
            }
        }

        if (recursive)
        {
            foreach (var sub in dir.GetDirectories())
            {
                CollectFile(ref fileList, folder + sub.Name, exts, recursive, ppath + sub.Name, collectHidden, match);
            }
        }
    }

    public static void CollectFileExceptExts(ref List<string> fileList, string folder, List<string> exts, bool recursive = false, string ppath = "")
    {
        folder = AppendSlash(folder);
        ppath = AppendSlash(ppath);
        DirectoryInfo dir = new DirectoryInfo(folder);
        FileInfo[] files = dir.GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (!exts.Contains(files[i].Extension.ToLower()))//e.g ".txt"
            {
                string fpath = folder + files[i].Name;
                if (!string.IsNullOrEmpty(fpath))
                    fileList.Add(fpath);
            }
        }

        if (recursive)
        {
            foreach (var sub in dir.GetDirectories())
            {
                CollectFile(ref fileList, folder + sub.Name, exts, recursive, ppath + sub.Name);
            }
        }
    }

    public static bool FindFile(string filePath, string fileName, ref List<string> paths)
    {
        if (string.IsNullOrEmpty(fileName)) return false;

        DirectoryInfo di = new DirectoryInfo(filePath);
        DirectoryInfo[] arrDir = di.GetDirectories();

        foreach (DirectoryInfo dir in arrDir)
        {
            if (FindFile(di + "/" + dir.ToString() + "/", fileName, ref paths))
                return true;
        }

        foreach (FileInfo fi in di.GetFiles("*.*"))
        {
            if (fi.Name == fileName)
            {
                paths.Add(fi.FullName);
            }
        }
        return false;
    }

    public static bool RenameFile(string filePath, string rename)
    {
        try
        {
            if (File.Exists(filePath))
            {
                string aimPath = filePath.Remove(filePath.LastIndexOf('/') + 1) + rename;
                File.Move(filePath, aimPath);
                return true;
            }
            else
            {
                return false;
            }
        }
        catch(IOException e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
    }

    public static void SetHidden(string path,Boolean doHidden = false)
    {
        if(doHidden)
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        else
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.Hidden);
    }

    public static void DeleteHiddenFile(List<string> floders, string ext)
    {
        List<string> hiddenFiles = CollectAllFolders(floders, ext, true);
        for(int i = 0; i < hiddenFiles.Count; i++)
        {
            if (File.Exists(hiddenFiles[i]))
            {
                try
                {
                    File.Delete(hiddenFiles[i]);
                }
                catch(IOException e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }

    public static string AppendSlash(string path)
    {
        if (path == null || path == "")
            return "";
        int idx = path.LastIndexOf('/');
        if (idx == -1)
            return path + "/";
        if (idx == path.Length - 1)
            return path;
        return path + "/";
    }

    public static void OverWriteText(string path, string contents)
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using (StreamWriter sw = File.CreateText(path))
        {
            sw.Write(contents);
        }
    }

    public static void WriteTextFile(string contents, string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.Write(contents);
            }
        }
        else
        {
            OverWriteText(path, contents);
        }
    }

    public static string PathCombine(params string[] paths)
    {
        var path = Path.Combine(paths);
        path = path.Replace(Path.DirectorySeparatorChar, '/');
        return path;
    }
}
