


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
        List<FileItem> fileInfos = new List<FileItem>();    
        List<Task<(string,long)>> allTask = new List<Task<(string,long)>>() ;
        //测试开发 假定需要下载4个文件 
        for(int i = 0; i < 4; i++)
        {
            var filePath = $"{DownLoaderDefine.Cdn}/{i+1}.xlsx";
            allTask.Add(DownLoadFunc.GetRemoteFileSizeAsync(filePath));
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
            //对任务进行分片处理 
            for(int j = 0; j < DownLoaderDefine.MaxClipCount; j++)
            {
                chunkInfos.Add(DownLoadFunc.CreateDownLoadChunk(fileInfos[i], j));
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

        foreach (var item in fileInfos)
        {

            var from = DownLoadFunc.GetTmpFilePathName(item.filePathName);
            var to = DownLoadFunc.GetPresentFilePathName(item.filePathName);
            var destroyFoler = DownLoaderDefine.UnityDestroyPath.ToLinuxPath();
            if (File.Exists(from))
            {
                //移除临时文件 
                if (File.Exists(to))
                {
                    File.Delete(to);
                }
                //移除临时文件 end 
                File.Move(from, to);
                //移除分块文件
                if(ChunkInfo.map != null && ChunkInfo.map.ContainsKey(item.filePathName))
                {
                    var partItems = ChunkInfo.map[item.filePathName];
                    foreach (var partItem in partItems)
                    {
                        var partFilePathName  = DownLoadFunc.GetTmpPartFilePathName(partItem.filePath, partItem.chunkIndex);
                        if(File.Exists(partFilePathName))
                        {
                            var destroyPartFilePathName = DownLoadFunc.GetDestroyFilePathName(partFilePathName);
                            if(File.Exists(destroyPartFilePathName))
                            {
                                File.Delete(destroyPartFilePathName);
                            }
                            File.Move(partFilePathName, destroyPartFilePathName);
                        }
                        
                    }
                    
                }
                //移除分块文件 end 
            }

        }


        ChunkInfo.map?.Clear();
        Console.ReadLine();
    }

    
}


