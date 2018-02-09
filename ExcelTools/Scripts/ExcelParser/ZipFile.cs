using System;

class ZipFile
{
    const Int64 ZIP64_LIMIT = (Int64)(1 << 31) - 1;
    const Int64 ZIP_FILECOUNT_LIMIT = (1 << 16) - 1;
    const Int64 ZIP_MAX_COMMENT = (1 << 16) - 1;

    // constants for Zip file compression methods
    const Int64 ZIP_STORED = 0;
    const Int64 ZIP_DEFLATED = 8;
    // Other ZIP compression methods not supported

    public enum FileMode
    {
        /// <summary>
        /// 'r'
        /// </summary>
        Read,
        /// <summary>
        /// 'w'
        /// </summary>
        Write,
        /// <summary>
        /// 'a'
        /// </summary>
        Append
    }

    // constants for Zip file compression methods
    // Other ZIP compression methods not supported
    public enum Compression
    {
        Zip_Stored = 0,
        Zip_Deflated = 8
    }

    private string path = null;
    public ZipFile(string file, FileMode mode = FileMode.Read, Compression compression = Compression.Zip_Stored, bool allowZip64 = false)
    {
        path = file;
    }
}
