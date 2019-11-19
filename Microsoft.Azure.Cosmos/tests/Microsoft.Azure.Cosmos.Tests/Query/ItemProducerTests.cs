//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class ItemProducerTests
    {
        private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        [TestMethod]
        [DataRow(0, 5)]
        [DataRow(1, 5)]
        [DataRow(2, 2)]
        [DataRow(2, 5)]
        public async Task TestMoveNextAsync(int pageSize, int maxPageSize)
        {
            List<int[]> combinations = new List<int[]>()
            {
                // Create all combination with empty pages
                new int[] { pageSize },
                new int[] { pageSize, 0 },
                new int[] { 0, pageSize },
                new int[] { pageSize, pageSize },
                new int[] { pageSize, 0, 0 },
                new int[] {0, pageSize, 0 },
                new int[] {0, 0, pageSize },
                new int[] { pageSize, 0, pageSize },
                new int[] { pageSize, pageSize, pageSize },
            };

            foreach (int[] combination in combinations)
            {
                (ItemProducer itemProducer, ReadOnlyCollection<ToDoItem> allItems) itemFactory = MockItemProducerFactory.Create(
                    responseMessagesPageSize: combination,
                    maxPageSize: maxPageSize,
                    cancellationToken: this.cancellationToken);

                ItemProducer itemProducer = itemFactory.itemProducer;

                List<ToDoItem> itemsRead = new List<ToDoItem>();

                Assert.IsTrue(itemProducer.HasMoreResults);

                while ((await itemProducer.TryMoveNextPageAsync(this.cancellationToken)).movedToNextPage)
                {
                    while (itemProducer.TryMoveNextDocumentWithinPage())
                    {
                        Assert.IsTrue(itemProducer.HasMoreResults);
                        string jsonValue = itemProducer.Current.ToString();
                        ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                        itemsRead.Add(item);
                    }
                }

                Assert.IsFalse(itemProducer.HasMoreResults);

                Assert.AreEqual(itemFactory.allItems.Count, itemsRead.Count);

                CollectionAssert.AreEqual(itemsRead, itemFactory.allItems, new ToDoItemComparer());
            }
        }

        [TestMethod]
        [DataRow(50, new int[] { 2, 4, 0, 5, 1, 6, 3, 2, 50, 0 })]
        [DataRow(5, new int[] { 0, 1 })]
        [DataRow(5, new int[] { 5, 0, 4 })]
        public async Task BufferMore(int maxPageSize, int[] pageSizes)
        {
            (ItemProducer itemProducer, ReadOnlyCollection<ToDoItem> allItems) itemFactory = MockItemProducerFactory.Create(
                responseMessagesPageSize: pageSizes,
                maxPageSize: maxPageSize,
                cancellationToken: this.cancellationToken);

            ItemProducer itemProducer = itemFactory.itemProducer;
            int currentItemCount = pageSizes[0];

            // Single thread
            for (int iterations = 0; iterations < 10; iterations++)
            {
                // Only first buffer more should go through
                await itemProducer.BufferMoreIfEmptyAsync(this.cancellationToken);
                Assert.AreEqual(currentItemCount, itemProducer.BufferedItemCount);
            }

            for (int roundTrip = 1; roundTrip < pageSizes.Length; roundTrip++)
            {
                await itemProducer.BufferMoreDocumentsAsync(this.cancellationToken);
                currentItemCount += pageSizes[roundTrip];
                Assert.AreEqual(currentItemCount, itemProducer.BufferedItemCount);
            }

            Assert.AreEqual(itemFactory.allItems.Count, itemProducer.BufferedItemCount);

            // Verify that Buffer More does nothing after all pages are already loaded
            for (int roundTrip = 0; roundTrip < pageSizes.Length; roundTrip++)
            {
                await itemProducer.BufferMoreDocumentsAsync(this.cancellationToken);
                Assert.AreEqual(itemFactory.allItems.Count, itemProducer.BufferedItemCount);
            }

            // Parallel
            itemFactory = MockItemProducerFactory.Create(
               responseMessagesPageSize: pageSizes,
               maxPageSize: 10,
               cancellationToken: this.cancellationToken);
            itemProducer = itemFactory.itemProducer;

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < pageSizes.Length; i++)
            {
                tasks.Add(Task.Run(async () => { await itemProducer.BufferMoreDocumentsAsync(this.cancellationToken); }));
            }

            await Task.WhenAll(tasks);
            Assert.AreEqual(currentItemCount, itemProducer.BufferedItemCount);
        }

        [TestMethod]
        public async Task ConcurrentMoveNextAndBufferMore()
        {
            bool blockExecute = true;
            int callBackCount = 0;
            void callbackBlock()
            {
                int callBackWaitCount = 0;
                callBackCount++;
                while (blockExecute)
                {
                    if (callBackWaitCount++ > 200)
                    {
                        Assert.Fail("The task never started to buffer the items. The callback was never called");
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(.1));
                }

                // Reset the block for the  next call
                blockExecute = true;
            }

            int[] pageSizes = new int[] { 2, 3, 1, 4 };
            (ItemProducer itemProducer, ReadOnlyCollection<ToDoItem> allItems) itemFactory = MockItemProducerFactory.Create(
               responseMessagesPageSize: pageSizes,
               maxPageSize: 10,
               executeCallback: callbackBlock,
               cancellationToken: this.cancellationToken);

            ItemProducer itemProducer = itemFactory.itemProducer;

            // BufferMore
            // Fire and Forget this task.
#pragma warning disable 4014
            Task bufferTask = Task.Run(() => itemProducer.BufferMoreDocumentsAsync(this.cancellationToken));

            // Verify the task started
            int waitCount = 0;
            while (callBackCount == 0)
            {
                if (waitCount++ > 100)
                {
                    Assert.Fail("The task never started to buffer the items. The callback was never called");
                }

                Thread.Sleep(TimeSpan.FromSeconds(.1));
            }

            List<ToDoItem> itemsRead = new List<ToDoItem>();
            Assert.AreEqual(0, itemProducer.BufferedItemCount, "Mocked response should be delayed until after move next is called.");

            int itemsToRead = pageSizes[0] + pageSizes[1];
            // Call move next while buffer more is waiting for response. 
            // Move next should wait for buffer more to complete then use the results of the buffer more.
#pragma warning disable 4014
            bool readTaskRunning = false;
            Task readTask = Task.Run(async () =>
            {
                readTaskRunning = true;
                List<ToDoItem> currentPage = await this.ReadItemProducer(itemProducer, itemsToRead);
                itemsRead.AddRange(currentPage);
            });
#pragma warning restore 4014

            // Verify the task started
            while (readTaskRunning == false)
            {
                Thread.Sleep(TimeSpan.FromSeconds(.1));
            }

            Assert.AreEqual(0, itemProducer.BufferedItemCount, "The call back block will prevent any item from being buffered");
            Assert.AreEqual(1, callBackCount, "Buffer more should have a lock which prevents multiple executes.");

            // Unblock the buffer task
            blockExecute = false;
            await bufferTask;
            Assert.AreEqual(2, itemProducer.BufferedItemCount, "Buffer should be completed and have 2 items from first page");

            // Unblock the read task
            blockExecute = false;
            await readTask;
            Assert.AreEqual(itemsToRead, itemsRead.Count, "All of the first and 2nd page should be read.");
            Assert.AreEqual(1, itemProducer.BufferedItemCount, "The last element should still be buffered. Moving next will cause another buffer.");

            itemsToRead = pageSizes[2] + pageSizes[3];
#pragma warning disable 4014
            Task moveNext = Task.Run(async () =>
            {
                if (!itemProducer.TryMoveNextDocumentWithinPage())
                {
                    Assert.IsTrue((await itemProducer.TryMoveNextPageAsync(this.cancellationToken)).movedToNextPage);
                    Assert.IsTrue(itemProducer.TryMoveNextDocumentWithinPage());
                }
            });
#pragma warning restore 4014
            while (callBackCount == 2)
            {
                if (waitCount++ > 100)
                {
                    Assert.Fail("The task never started to buffer the items. The callback was never called");
                }

                Thread.Sleep(TimeSpan.FromSeconds(.1));
            }

            bufferTask = Task.Run(() => itemProducer.BufferMoreDocumentsAsync(this.cancellationToken));

            Assert.AreEqual(3, callBackCount, "Buffer more should have a lock which prevents multiple executes.");

            blockExecute = false;
            await moveNext;

            blockExecute = false;
            await bufferTask;
            Assert.AreEqual(itemsToRead, itemProducer.BufferedItemCount, "2nd Page should be loaded.");
        }

        private async Task<List<ToDoItem>> ReadItemProducer(ItemProducer itemProducer, int numItems)
        {
            List<ToDoItem> itemsRead = new List<ToDoItem>();
            for (int i = 0; i < numItems; i++)
            {
                if (!itemProducer.TryMoveNextDocumentWithinPage())
                {
                    Assert.IsTrue((await itemProducer.TryMoveNextPageAsync(this.cancellationToken)).movedToNextPage);
                    Assert.IsTrue(itemProducer.TryMoveNextDocumentWithinPage());
                }

                Assert.IsTrue(itemProducer.HasMoreResults);
                itemsRead.Add(this.ConvertCosmosElement(itemProducer.Current));
            }

            return itemsRead;
        }

        private ToDoItem ConvertCosmosElement(CosmosElement element)
        {
            string jsonValue = element.ToString();
            ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
            return item;
        }
    }
}
