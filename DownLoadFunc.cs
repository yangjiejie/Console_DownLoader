
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;


namespace Assets.Script
{
    public static class DownLoadFunc
    {

        public static readonly HttpClient client = new HttpClient();
        /// <summary>
        /// 构建分块下载的偏移字节起始位置和字节大小 
        /// </summary>
        /// <param name="item 文件信息 "></param>
        /// <param name="chunkIndex 分块索引"></param>
        public static ChunkInfo CreateDownLoadChunk(FileItem item,int chunkIndex)
        {
            var path = item.filePathName;
            var size = item.fileSize;
            var baseEleSize = size / DownLoaderDefine. MaxClipCount;
            
            var fileName = Path.GetFileName(path);
            var tmpPath = GetTmpPartFilePathName(path, chunkIndex);
            ChunkInfo info  = null;
            if (File.Exists(tmpPath))
            {
                var json = File.ReadAllText(tmpPath);
                info = JsonUtil.DeserializeObject<ChunkInfo>(json);
            }
            else
            {
                info = new ChunkInfo();
                info.fileSize = size;
                info.filePath = path;
                info.Start = chunkIndex * baseEleSize;
                info.End = chunkIndex < DownLoaderDefine.MaxClipCount - 1 ? info.Start + baseEleSize - 1 : size - 1;

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
        /// <summary>
        /// 下载并限流 
        /// </summary>
        /// <param name="semaphoreSlim"></param>
        /// <param name="chunkFile"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static async Task DownloadFileAsync(SemaphoreSlim semaphoreSlim, ChunkInfo chunkFile, IProgress<long> progress)
        {
            // 这里必须 await WaitAsync()，否则调用方会一下子启动大量任务，
            // 导致信号量还没起作用就已经把任务都“洪水猛兽”般排队起来了。
            // await 可以确保任务调度真正被限流。
            await semaphoreSlim.WaitAsync();
            try
            {
                // 这里也必须 await DownLoadFileAsync。
                // 如果不 await，而是直接 fire-and-forget，那么 finally 会立即执行 Release()，
                // 这样信号量会过早释放，后续任务会直接并发运行，相当于没有并发控制。
                await DownLoadFileAsyncImp(chunkFile, progress, semaphoreSlim);
            }
            finally
            {
                semaphoreSlim.Release();
            }             
        }

        public static async Task DownLoadFileAsyncImp(ChunkInfo item1,IProgress<long> progress, SemaphoreSlim semaphoreSlim)
        {
            var httpClient = DownLoadFunc.client;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, item1.filePath);
            //支持断点续传 
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(item1.Start, item1.End);
            //ResponseHeadersRead 请求头即可 获取流即可 按需读取 
            using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                //如果在200-299之间 ok否则 抛出异常 
                response.EnsureSuccessStatusCode();
                var tmpPath = Path.Combine(DownLoaderDefine.UnityTmpPath, Path.GetFileName(item1.filePath) + DownLoaderDefine.partDownLoadSuffix).ToLinuxPath();
                item1.downLoadTime = DownLoadFunc.GetNowTime();
                using (Stream remoteStream = await response.Content.ReadAsStreamAsync())
                using (FileStream fs = new FileStream(tmpPath,
                    FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write))
                {
                    fs.Seek(item1.Start, SeekOrigin.Begin); // 定位到断点位置
                                                            //一般来说8k-16k是推荐的 这里也可以优化 先用8192 
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    int downLoadSize = 0;
                    while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead);

                        // 全局累计进度
                        progress.Report(bytesRead);
                        downLoadSize += bytesRead;
                      
                    }
                    item1.downLoadEndTime = DownLoadFunc.GetNowTime();
                    item1.DownloadedSize = downLoadSize;
                    if (item1.DownloadedSize == item1.End - item1.Start + 1)
                    {
                        item1.IsCompleted = true;
                    }
                    var partJson = JsonUtil.SerializeObject(item1);
                    var partPath = DownLoadFunc.GetTmpPartFilePathName(item1.filePath, item1.chunkIndex);
                    File.WriteAllText(partPath, partJson);
                }
            }
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

        public static string GetLocalIPv4()
        {
            string localIP = "";
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) // 只要 IPv4
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }
        /// <summary>
        /// 获取tmp下载目录地址 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetTmpFilePathName(string fileName)
        {
            fileName = Path.GetFileName(fileName);
            return Path.Combine(DownLoaderDefine.UnityTmpPath, fileName+ DownLoaderDefine.partDownLoadSuffix).ToLinuxPath();
        }
        /// <summary>
        /// 获取持久化目录文件地址 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetPresentFilePathName(string fileName)
        {
            fileName = Path.GetFileName(fileName);
            return Path.Combine(DownLoaderDefine.UnityPresentPath, fileName).ToLinuxPath();
        }
        /// <summary>
        /// 获取断点续传的文件记录 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetTmpPartFilePathName(string fileName,int index)
        {
            fileName = Path.GetFileName(fileName);
            return Path.Combine(DownLoaderDefine.UnityTmpPartPath, fileName + index.ToString()).ToLinuxPath();
        }

        /// <summary>
        /// 获取回收站目录
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetDestroyFilePathName(string fileName)
        {
            fileName = Path.GetFileName(fileName);
            return Path.Combine(DownLoaderDefine.UnityDestroyPath, fileName ).ToLinuxPath();
        }



        public static void CreateDir(string path)
        {
            if (File.Exists(path))
            {
                return;
            }
            if (Directory.Exists(path))
            {
                return;
            }

            var father = Directory.GetParent(path);

            while (father != null && !father.Exists)
            {
                Directory.CreateDirectory(father.FullName);
                father = Directory.GetParent(father.FullName);
            }
            Directory.CreateDirectory(path);
        }
        public static string GetNowTime()
        {
            return DateTime.Now.ToString("yyyy/MM/dd:HH:mm:ss:fff");
        }
    }
}
