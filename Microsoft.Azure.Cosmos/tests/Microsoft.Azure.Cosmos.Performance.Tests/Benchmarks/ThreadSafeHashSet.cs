namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ThreadSafeHashSet<T>
    {
        private readonly HashSet<T> hashSet;
        private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

        public ThreadSafeHashSet()
        {
            this.hashSet = new HashSet<T>();
        }

        public bool Add(T item)
        {
            this.rwLock.EnterWriteLock();
            try
            {
                return this.hashSet.Add(item);
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            this.rwLock.EnterWriteLock();
            try
            {
                return this.hashSet.Remove(item);
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            this.rwLock.EnterReadLock();
            try
            {
                return this.hashSet.Contains(item);
            }
            finally
            {
                this.rwLock.ExitReadLock();
            }
        }

        public void Clear()
        {
            this.rwLock.EnterWriteLock();
            try
            {
                this.hashSet.Clear();
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                this.rwLock.EnterReadLock();
                try
                {
                    return this.hashSet.Count;
                }
                finally
                {
                    this.rwLock.ExitReadLock();
                }
            }
        }

    }
}
