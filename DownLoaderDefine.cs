using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Script
{
    public class DownLoaderDefine
    {
        public static string Cdn
        {
            get
            {
                return "http://192.168.103.77:80";
            }
        }
    }

    public class ChunkInfo
    {
        public string filePath;
        public string TempFilePath;
        public long Start;
        public long End;
        public long chunkIndex;
        public bool IsCompleted;
        public string downLoadTime;
        public string downLoadEndTime;
        public long DownloadedSize;
    }

    public class DownloadProgress
    {
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public double Percentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
        public int ActiveThreads { get; set; }
    }
    public class FileItem
    {
        public string filePathName;
        public string md5;
        public long fileSize;
        

        public override string ToString()
        {
            return $"路径：{filePathName},size={fileSize},md5= ,{md5}";
        }
    }


    // 分块状态信息
    public class ChunkProgress
    {
        public long Start { get; set; }
        public long End { get; set; }
        public bool IsCompleted { get; set; }
    }

    // 断点续传元数据（保存到本地文件）
    public class DownloadMetadata
    {
        public string Url { get; set; } // 下载链接（用于校验文件是否一致）
        public long FileSize { get; set; } // 文件总大小
        public List<ChunkProgress> Chunks { get; set; } = new List<ChunkProgress>();
    }

}
