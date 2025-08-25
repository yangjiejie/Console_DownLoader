using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Assets.Script
{
    public class DownLoaderDefine
    {
        public const int MaxClipCount = 4; // 最大分片数 
        public const string partDownLoadSuffix = ".part";
        public const string chunkConfig = ".chunk.cofig";

        //注意实际unity的生产环境下不要使用 System.Environment.CurrentDirectory 这个目录
        //要使用沙盒目录
        public static string UnityTmpPath
        {
            get
            {
                return System.Environment.CurrentDirectory + "/tmp";
            }
        }

        public static string UnityTmpPartPath
        {
            get
            {
                return System.Environment.CurrentDirectory + "/part";
            }
        }

        public static string UnityPresentPath
        {
            get
            {
                return System.Environment.CurrentDirectory + "/present";
            }
        }
        public static string UnityDestroyPath
        {
            get
            {
                return System.Environment.CurrentDirectory + "/destroy";
            }
        }

        public static string Cdn
        {
            get
            {
                
                return $"http://{DownLoadFunc.GetLocalIPv4()}:80";
            }
        }
    }

    public class ChunkInfo
    {
        [JsonIgnore]
        public static Dictionary<string, HashSet<ChunkInfo>> map = new();
        public  long fileSize;
        public string filePath;
        
        public long Start;
        public long End;
        public int chunkIndex;
        public bool IsCompleted;
        public string downLoadTime;
        public string downLoadEndTime;
        public long DownloadedSize;

        public static bool JudgeIsCompleted()
        {
            lock (map)
            {
                foreach (var kvp in map)
                {
                    long size = 0;
                    foreach (var chunk in kvp.Value)
                    {
                        size += chunk.DownloadedSize;
                        if (size >= chunk.fileSize)
                        {
                            break;
                        }
                    }
                    
                }
            }
            return false;
        }
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
        public int MaxChunkCount;
        

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
