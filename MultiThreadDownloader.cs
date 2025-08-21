


using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Assets.Script;
using UnityEngine;
//多线程下载器 
public class MultiThreadDownloader
{
    private readonly HttpClient _httpClient;
    private const int ChunkSize = 1024 * 1024; // 1MB 块大小

    public MultiThreadDownloader()
    {
        _httpClient = new HttpClient();
    }

    public async Task DownloadFileAsync(string url, string savePath,
    int maxThreads = 4, IProgress<long> progress = null, CancellationToken cancellationToken = default)
    {
        var metadataPath = savePath + ".part"; // 断点文件路径
        DownloadMetadata metadata = null;

        try
        {
            // 获取文件大小
            var fileSize = await GetFileSizeAsync(url);

            // 检查是否有断点文件
            if (File.Exists(metadataPath))
            {
                // 加载断点信息（需实现 JSON 反序列化）
                var metadataJson = File.ReadAllText(metadataPath);
                metadata = JsonUtil.DeserializeObject<DownloadMetadata>(metadataJson);

                // 校验文件是否一致（URL 和文件大小）
                if (metadata.Url != url || metadata.FileSize != fileSize)
                {
                    File.Delete(metadataPath); // 文件已变更，删除旧断点
                    metadata = null;
                }
            }

            // 初始化或重新计算分块
            List<ChunkProgress> chunks;
            if (metadata == null)
            {
                // 新下载：创建文件并计算分块
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fileStream.SetLength(fileSize);
                }
                var rawChunks = CalculateChunks(fileSize, ChunkSize);
                chunks = rawChunks.Select(c => new ChunkProgress { Start = c.Start, End = c.End, IsCompleted = false }).ToList();
                metadata = new DownloadMetadata { Url = url, FileSize = fileSize, Chunks = chunks };
                SaveMetadata(metadata, metadataPath); // 保存初始断点信息
            }
            else
            {
                // 断点续传：使用已有的分块信息
                chunks = metadata.Chunks;
            }

            // 筛选未完成的分块
            var pendingChunks = chunks.Where(c => !c.IsCompleted).ToList();
            if (!pendingChunks.Any())
            {
                // 所有分块已完成，直接返回
                progress?.Report(100);
                return;
            }

            // 创建下载任务（仅处理未完成分块）
            var downloadTasks = new List<Task>();
            var totalBytesDownloaded = chunks.Where(c => c.IsCompleted).Sum(c => c.End - c.Start + 1);
            var progressLock = new object();

            foreach (var chunk in pendingChunks)
            {
                // 限制并发线程数
                if (downloadTasks.Count >= maxThreads)
                {
                    await Task.WhenAny(downloadTasks);
                    downloadTasks.RemoveAll(t => t.IsCompleted);
                }

                // 下载分块，并标记完成状态
                var task = DownloadChunkAsync(url, savePath, chunk.Start, chunk.End,
                    bytesDownloaded =>
                    {
                        lock (progressLock)
                        {
                            totalBytesDownloaded += bytesDownloaded;
                            progress?.Report(totalBytesDownloaded * 100 / metadata.FileSize);
                        }
                    }, cancellationToken)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            chunk.IsCompleted = true;
                            SaveMetadata(metadata, metadataPath); // 实时保存分块状态
                        }
                    }, cancellationToken);

                downloadTasks.Add(task);
            }

            // 等待所有分块下载完成
            await Task.WhenAll(downloadTasks);

            // 下载完成：删除断点文件
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }
        }
        catch (Exception ex)
        {
            // 异常时保留断点文件，仅删除不完整的目标文件（可选）
            if (File.Exists(savePath) && (metadata == null || metadata.Chunks.Any(c => !c.IsCompleted)))
            {
                File.Delete(savePath);
            }
            throw;
        }
    }

    // 保存断点信息到本地文件（JSON 序列化）
    private void SaveMetadata(DownloadMetadata metadata, string path)
    {

        var json = JsonUtil.SerializeObject(metadata);
        File.WriteAllText(path, json);
    }
    private async Task<long> GetFileSizeAsync(string url)
    {
        using (var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url)))
        {
            response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength ?? 0;
        }
    }

    private List<(long Start, long End)> CalculateChunks(long fileSize, int chunkSize)
    {
        var chunks = new List<(long, long)>();
        var position = 0L;

        while (position < fileSize)
        {
            var end = Math.Min(position + chunkSize - 1, fileSize - 1);
            chunks.Add((position, end));
            position = end + 1;
        }

        return chunks;
    }

    private async Task DownloadChunkAsync(string url, string savePath, long start, long end,
        Action<long> progress, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            //EnsureSuccessStatusCode它是一个便捷的方法，用于检查HTTP请求是否成功。如果响应状态码表示失败（即不在200-299范围内），它就抛出一个异常，让程序在这里中止；如果成功，它就什么都不做，让代码继续正常执行。
            response.EnsureSuccessStatusCode();

            using (var fileStream = new FileStream(savePath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                fileStream.Seek(start, SeekOrigin.Begin);

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    var bytesToRead = end - start + 1;

                    while (totalBytesRead < bytesToRead)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0,
                            (int)Math.Min(buffer.Length, bytesToRead - totalBytesRead), cancellationToken);

                        if (bytesRead == 0) break;

                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;
                        progress?.Invoke(bytesRead);
                    }
                }
            }
        }
    }
}