using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher.Scripts
{
    struct PathInfo
    {
        public const bool FILE = true;
        public const bool FOLDER = false;

        public bool type;
        public string path;

        public PathInfo(bool type, string path)
        {
            this.type = type;
            this.path = path;
        }
    }

    static class FolderRemover
    {
        private static void GetDelList(List<PathInfo> list, string path)
        {
            foreach (var item in Directory.GetDirectories(path))
                GetDelList(list, item);

            foreach (var item in Directory.GetFiles(path))
                list.Add(new PathInfo(PathInfo.FILE, item));

            list.Add(new PathInfo(PathInfo.FOLDER, path));
        }

        public static List<PathInfo> GetDelList(string path)
        {
            List<PathInfo> ret = new List<PathInfo>();
            GetDelList(ret, path);
            return ret;
        }

        public static void Remove(PathInfo pathInfo)
        {
            if (pathInfo.type == PathInfo.FILE)
                File.Delete(pathInfo.path);
            else
                Directory.Delete(pathInfo.path);
        }

        public static void Remove(string path)
        {
            foreach (var item in GetDelList(path))
                Remove(item);
        }

        public static bool TryRemove(string path)
        {
            try
            {
                Remove(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
