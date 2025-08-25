using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownLoadStateChange : IProgress<long>
    {
        public long totalSize = 1;
        public long currentSize = 0;
        public void Report(long value)
        {
            Interlocked.Add(ref currentSize,value);
            var percent = $"{(100 * currentSize / totalSize)}%";
            Console.WriteLine(percent);
        }
    }
}
