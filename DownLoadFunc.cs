
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;


namespace Assets.Script
{
    public static class DownLoadFunc
    {
        public const int MaxClipCount  = 4;
        public const string partDownLoadSuffix  =".unity.download.tmp";
        public static string UnityTmpPath 
        {
            get
            {
                return System.Environment.CurrentDirectory + "/present";
            }
        }

        public static string UnityPresentPath
        {
            get
            {
                return System.Environment.CurrentDirectory + "/tmp";
            }
        }
        /// <summary>
        /// 构建分块下载的偏移字节起始位置和字节大小 
        /// </summary>
        /// <param name="item 文件信息 "></param>
        /// <param name="chunkIndex 分块索引"></param>
        public static ChunkInfo CreateDownLoadChunk(FileItem item,int chunkIndex)
        {
            var path = item.filePathName;
            var size = item.fileSize;
            var baseEleSize = size / MaxClipCount;
            
            var fileName = Path.GetFileName(path);
            var tmpPath = $"{UnityTmpPath}/{fileName}.{partDownLoadSuffix}");
            ChunkInfo info  = null;
            if (File.Exists(tmpPath))
            {
                 info = JsonUtil.DeserializeObject<ChunkInfo>(path);
            }
            else
            {
                info = new ChunkInfo();
                info.fileSize = size;
                info.filePath = path;
                info.Start = chunkIndex * baseEleSize;
                info.End = chunkIndex < MaxClipCount - 1 ? baseEleSize : size - 1;

                //info.downLoadTime = DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss:fff");
                //info.downLoadEndTime = DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss:fff");
                info.chunkIndex = chunkIndex;
               
            }
            
            if(!ChunkInfo.map.ContainsKey(path))
            {
                ChunkInfo.map.Add(path, new HashSet<ChunkInfo>());
            }
            var hashSet = ChunkInfo.map[path];
            hashSet.Add(info);
            return info;
        }
        /// <summary>
        /// 获取文件头大小 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<(string,long)> GetRemoteFileSizeAsync(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    HttpResponseMessage response = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.Content.Headers.ContentLength.HasValue)
                        {
                            return (url,response.Content.Headers.ContentLength.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRemoteFileSizeAsync] 获取文件大小失败: {url}, {ex.Message}");
            }

            return ("",-1); // 获取失败
        }

        public static string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    // 计算文件的 MD5 哈希值
                    byte[] hashBytes = md5.ComputeHash(stream);

                    // 将字节数组转换为十六进制字符串
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        sb.Append(hashBytes[i].ToString("x2")); // "x2" 表示两位小写十六进制
                    }

                    return sb.ToString();
                }
            }
        }


        public static string ToLinuxPath(this string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            return content.Replace("\\", "/");
        }
    }
}
