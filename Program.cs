


using System.Net;
using System.Net.Sockets;
using Assets.Script;

public class MainProgram
{

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
    }
    
    public static async void Main()
    {
        CreateDir(DownLoadFunc.UnityPresentPath);
        CreateDir(DownLoadFunc.UnityTmpPath);
        List<FileItem> fileInfos = new List<FileItem>();    
        List<Task<(string,long)>> allTask = new List<Task<(string,long)>>() ;
        for(int i = 0; i < 4; i++)
        {
            var filePath = $"{DownLoaderDefine.Cdn}/{i+1}.xlsx";
            allTask.Add(DownLoadFunc.GetRemoteFileSizeAsync(""));
        }
        await Task.WhenAll(allTask) ;

        foreach (var item in allTask)
        {
            var info = new FileItem();
            info.fileSize = item.Result.Item2;
            info.filePathName = item.Result.Item1;
            fileInfos.Add(info);
        }
        List<ChunkInfo> chunkInfos = new List<ChunkInfo>();
        ChunkInfo.map?.Clear();
        for (int i = 0; i < fileInfos.Count; i++)
        {
            for(int j = 0; j < DownLoadFunc.MaxClipCount; j++)
            {
                chunkInfos.Add(DownLoadFunc.CreateDownLoadChunk(fileInfos[i], i));
            }
        }
        foreach(var item in fileInfos)
        {
            if(ChunkInfo.map.TryGetValue(item.filePathName,out var tmpHashSet))
            {
                int index = 0;
                foreach (var item1 in tmpHashSet)
                {
                    HttpClient httpClient = new HttpClient();
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, item1.filePath);
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(item1.Start, item1.End);
                    using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        // 检查服务器是否支持断点续传
                        if (response.StatusCode == System.Net.HttpStatusCode.OK )
                        {
                            Console.WriteLine("⚠️ 服务器不支持断点续传，重新下载整个文件。");
                            
                        }
                        using (FileStream fs = new FileStream(DownLoadFunc.UnityTmpPath, FileMode.Open, FileAccess.Write, FileShare.Write))
                        {
                            fs.Seek(item1.Start, SeekOrigin.Begin);
                        }
                            
                    }
                }
            }
           
           
        }
        
        
        

        Console.ReadLine();
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
}


