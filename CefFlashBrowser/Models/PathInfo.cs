using System.IO;
using System.Linq;

namespace CefFlashBrowser.Models
{
    public class PathInfo
    {
        public enum PathType
        {
            File,
            Directory
        }

        public PathType Type { get; set; }
        public string Path { get; set; }

        public bool Exist
        {
            get
            {
                switch (Type)
                {
                    case PathType.File:
                        return File.Exists(Path);

                    case PathType.Directory:
                        return Directory.Exists(Path);

                    default:
                        return false;
                }
            }
        }

        public bool IsFileInUse
        {
            get
            {
                if (Type != PathType.File || !Exist)
                    return false;

                FileStream stream = null;
                try
                {
                    stream = new FileStream(Path, FileMode.Open, FileAccess.Read);
                    return false;
                }
                catch
                {
                    return true;
                }
                finally
                {
                    if (stream != null)
                        stream.Close();
                }
            }
        }

        public PathInfo() { }

        public PathInfo(PathType type, string path)
        {
            Type = type;
            Path = path;
        }

        public void Delete()
        {
            switch (Type)
            {
                case PathType.File:
                    {
                        File.Delete(Path);
                    }
                    break;

                case PathType.Directory:
                    {
                        var files = from item in Directory.GetFiles(Path) select new PathInfo(PathType.File, item);
                        var dirs = from item in Directory.GetDirectories(Path) select new PathInfo(PathType.Directory, item);

                        foreach (var item in dirs)
                            item.Delete();
                        foreach (var item in files)
                            item.Delete();

                        Directory.Delete(Path);
                    }
                    break;

                default:
                    break;
            }
        }
    }
}
