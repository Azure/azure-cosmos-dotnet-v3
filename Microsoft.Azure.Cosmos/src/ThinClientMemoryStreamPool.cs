namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Concurrent;
    using System.IO;

    internal sealed class ThinClientMemoryStreamPool
    {
        public static ThinClientMemoryStreamPool Instance { get; } = new ();

        private readonly ConcurrentBag<MemoryStream> objects = new ConcurrentBag<MemoryStream>();

        private ThinClientMemoryStreamPool()
        {
        }

        public MemoryStream Get(int minimumCapacity)
        {
            if (this.objects.TryTake(out MemoryStream ms))
            {
                ms.Position = 0;
                ms.SetLength(0);
                if (ms.Capacity < minimumCapacity)
                {
                    ms.Capacity = minimumCapacity;
                }
                return ms;
            }

            return new MemoryStream(minimumCapacity);
        }

        public void Return(MemoryStream ms)
        {
            ms.Position = 0;
            ms.SetLength(0);
            this.objects.Add(ms);
        }
    }

}
