//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.IO;

    internal class CosmosMemoryStreamManager
    {
        private readonly object lockObject;
        private ArrayPool<MemoryStream> memmoryPool = null;
        private int counter = 0;
        private MemoryStream[] arrayCheck;

        public CosmosMemoryStreamManager()
        {
            this.lockObject = new object();
            this.memmoryPool = ArrayPool<MemoryStream>.Create(1, 100);
        }

        public ArrayPool<MemoryStream> GetArrayPool() => this.memmoryPool;

        public MemoryStream GetMemoryStream()
        {
            if (this.arrayCheck == null || this.counter >= this.arrayCheck.Length)
            {
                this.counter = 0;
                this.arrayCheck = this.memmoryPool.Rent(100);
            }

            MemoryStream stream = null;

            lock (this.lockObject)
            {
                stream = this.arrayCheck[this.counter];
                this.counter++;

                if (this.arrayCheck == null || this.counter >= this.arrayCheck.Length)
                {
                    this.counter = 0;
                    this.arrayCheck = this.memmoryPool.Rent(1);

                    //Console.WriteLine($"Current pool is of length: {this.arrayCheck.Length} and counter: {this.counter}");

                }
            }

            return stream;
        }

        public void Return(MemoryStream stream)
        {
            this.memmoryPool.Return(new MemoryStream[] { stream });
        }
    }

    internal class CosmosMemoryStream : MemoryStream
    {
        private CosmosMemoryStreamManager manager = null;
        private MemoryStream stream = null;

        public CosmosMemoryStream(CosmosMemoryStreamManager manager)
        {
            //Console.WriteLine("Asking for memory");
            this.manager = manager;
            this.stream = this.manager.GetMemoryStream();
        }

        protected override void Dispose(bool disposing)
        {
            //Console.WriteLine("Return mmeory stream now");
            base.Dispose(disposing);
            this.manager.Return(this.stream);
        }
    }
}