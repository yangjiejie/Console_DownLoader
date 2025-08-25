


using System.Net;
using System.Net.Sockets;
using Assets.Script;
using Downloader;

public class MainProgram
{
    public static async Task Main()
    {
        await MainImp();
    }
    public static async Task MainImp()
    {
        //需要提前创建至少4个目录 
        //第一 下载后文件的最终目录 第二临时目录 第三断点续传相关的目录
        // 下载完毕后需要清理后面2个目录  第4就是我们自己搞的回收站目录
        DownLoadStateChange stateChange = new();
        DownLoadFunc.CreateDir(DownLoaderDefine.UnityPresentPath);
        DownLoadFunc.CreateDir(DownLoaderDefine.UnityTmpPath);
        DownLoadFunc.CreateDir(DownLoaderDefine.UnityTmpPartPath);
        DownLoadFunc.CreateDir(DownLoaderDefine.UnityDestroyPath);
        DownLoadFunc.CreateDir(DownLoaderDefine.UnityDestroyPath + "/part");
        DownLoadFunc.CreateDir(DownLoaderDefine.UnityDestroyPath + "/config");

        List<FileItem> fileInfos = new List<FileItem>();    
        List<Task<(string,long)>> allTask = new List<Task<(string,long)>>() ;
        //测试开发 假定需要下载3个文件 
        for(int i = 0; i < 3; i++)
        {
            var filePath = $"{DownLoaderDefine.Cdn}/{i+1}.txt";
            allTask.Add(DownLoadFunc.GetRemoteFileSizeAsync(filePath));
        }
        await Task.WhenAll(allTask) ;

        foreach (var item in allTask)
        {
            var info = new FileItem();
            info.fileSize = item.Result.Item2;
            info.filePathName = item.Result.Item1;
            info.MaxChunkCount = Math.Min(DownLoaderDefine.MaxClipCount, 1 + (int)info.fileSize / 512);
            fileInfos.Add(info);
        }
      
        ChunkInfo.map?.Clear();
        for (int i = 0; i < fileInfos.Count; i++)
        {
            //对任务进行分片处理 
            for(int j = 0; j < fileInfos[i].MaxChunkCount; j++)
            {
                DownLoadFunc.CreateDownLoadChunk(fileInfos[i], j);
            }
        }
        long totalSize = fileInfos.Sum(f => f.fileSize); // 所有文件总大小
        stateChange.totalSize = totalSize;
       
        List<Task> downloadTasks = new List<Task>();
        var semaphore = new SemaphoreSlim(4); // 限制并发下载数量为4个 
        foreach (var item in fileInfos)
        {
            if (ChunkInfo.map != null && ChunkInfo.map.TryGetValue(item.filePathName, out var tmpHashSet))
            {
                foreach (var item1 in tmpHashSet)
                {
                    if (item1.IsCompleted) continue;
                    var fileItem = item1;
                    downloadTasks.Add(DownLoadFunc.DownloadFileAsync(semaphore,fileItem, stateChange));
                }
            }
        }
        await Task.WhenAll(downloadTasks);
        //合并临时文件 

        foreach (var item in fileInfos)
        {
            var to = DownLoadFunc.GetPresentFilePathName(item.filePathName);
            if (File.Exists(to))
            {
                File.Delete(to);
            }
            
            using (var output = new FileStream(to, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < item.MaxChunkCount; i++)
                {
                    var partDownLoadTmpFile = DownLoadFunc.GetDownLoadTmpFilePathName(item.filePathName, i);
                    if (!File.Exists(partDownLoadTmpFile))
                    {
                        DownLoadFunc.ErrorCall($"临时partDownLoad文件不存在异常{partDownLoadTmpFile}{i}");
                        return;
                    }
                    using (var input = new FileStream(partDownLoadTmpFile, FileMode.Open, FileAccess.Read))
                    {
                        input.CopyTo(output); // 高效拷贝
                    }
                    File.Move(partDownLoadTmpFile,DownLoadFunc.GetDestroyFilePathName(partDownLoadTmpFile,"part"),true);
                    //移除json文件 
                    var jsonFile = DownLoadFunc.GetTmpPartFilePathName(item.filePathName, i);
                    if(File.Exists(jsonFile))
                    {
                        File.Move(jsonFile, DownLoadFunc.GetDestroyFilePathName(jsonFile,"config"),true);
                    }

                }
                
            }
            

        }

        Console.WriteLine("download sucess!");
        ChunkInfo.map?.Clear();
        Console.ReadLine();
    }

    
}


