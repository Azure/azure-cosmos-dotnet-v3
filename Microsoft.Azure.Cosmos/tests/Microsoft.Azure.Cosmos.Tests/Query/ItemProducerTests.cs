//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class ItemProducerTests
    {
        private static readonly int iterations = 1;
        private bool unhandledException = false;
        private CancellationToken cancellationToken = new CancellationTokenSource().Token;

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
                new int[] { pageSize, 0, 0 },
                new int[] {0, pageSize, 0 },
                new int[] {0, 0, pageSize },
                new int[] { pageSize, 0, pageSize },
                new int[] { pageSize, pageSize, pageSize },
            };

            foreach (var combination in combinations)
            {
                (ItemProducer itemProducer, ReadOnlyCollection<ToDoItem> allItems) itemFactory = MockItemProducerFactory.Create(
                    responseMessagePageSizes: combination,
                    maxPageSize: maxPageSize,
                    cancellationToken: cancellationToken);

                ItemProducer itemProducer = itemFactory.itemProducer;

                List<ToDoItem> itemsRead = new List<ToDoItem>();

                Assert.IsTrue(itemProducer.HasMoreResults);

                while ((await itemProducer.MoveNextAsync(cancellationToken)).successfullyMovedNext)
                {
                    Assert.IsTrue(itemProducer.HasMoreResults);
                    string jsonValue = itemProducer.Current.ToString();
                    ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                    itemsRead.Add(item);
                }

                Assert.IsFalse(itemProducer.HasMoreResults);

                Assert.AreEqual(itemFactory.allItems.Count, itemsRead.Count);

                CollectionAssert.AreEqual(itemsRead, itemFactory.allItems, new ToDoItemComparer());
            }
        }

        //        [TestMethod]
        //        [Owner("brchon")]
        //        public async Task BufferMore()
        //        {
        //            for (int iteration = 0; iteration < iterations; iteration++)
        //            {
        //                int pageSize = 100;
        //                int numPages = (int)Math.Ceiling(((double)MockItemProducerFactory.Dataset.Count / pageSize));
        //                ItemProducer ItemProducer = MockItemProducerFactory.Create(initialPageSize: pageSize);
        //                Assert.AreEqual(0, ItemProducer.BufferedItemCount);

        //                // Single thread
        //                for (int iterations = 0; iterations < 10; iterations++)
        //                {
        //                    // Only first buffer more should go through
        //                    await ItemProducer.BufferMoreIfEmpty(CancellationToken.None);
        //                    Assert.AreEqual(pageSize, ItemProducer.BufferedItemCount);
        //                }

        //                for (int roundTrip = 1; roundTrip < numPages; roundTrip++)
        //                {
        //                    await ItemProducer.BufferMoreItems(CancellationToken.None);
        //                    Assert.AreEqual(pageSize * (roundTrip + 1), ItemProducer.BufferedItemCount);
        //                }

        //                for (int roundTrip = 0; roundTrip < numPages; roundTrip++)
        //                {
        //                    await ItemProducer.BufferMoreItems(CancellationToken.None);
        //                    Assert.AreEqual(MockItemProducerFactory.Dataset.Count, ItemProducer.BufferedItemCount);
        //                }

        //                // Parallel
        //                List<Task> tasks = new List<Task>();
        //                ItemProducer = MockItemProducerFactory.Create(initialPageSize: pageSize);
        //                for (int i = 0; i < numPages / 2; i++)
        //                {
        //                    tasks.Add(Task.Run(async () => { await ItemProducer.BufferMoreItems(CancellationToken.None); }));
        //                }

        //                await Task.WhenAll(tasks);
        //                Assert.AreEqual(pageSize * (numPages / 2), ItemProducer.BufferedItemCount);
        //            }
        //        }

        //        [TestMethod]
        //        [Owner("brchon")]
        //        public async Task ConcurrentMoveNextAndBufferMore()
        //        {
        //            Random random = new Random();
        //            for (int iteration = 0; iteration < iterations; iteration++)
        //            {
        //                ItemProducer ItemProducer = MockItemProducerFactory.Create(initialPageSize: 1);

        //                // BufferMore
        //                // Fire and Forget this task.
        //#pragma warning disable 4014
        //                Task.Run(() => BufferMoreInBackground(ItemProducer));
        //#pragma warning restore 4014

        //                // MoveNextAsync
        //                List ItemsRead = new List();
        //                while (await ItemProducer.MoveNextAsync(CancellationToken.None))
        //                {
        //                    ItemsRead.Add(ItemProducer.Current);
        //                    await Task.Delay(random.Next(0, 10));
        //                }

        //                Assert.IsTrue(ItemsRead.SequenceEqual(MockItemProducerFactory.Dataset));
        //            }
        //        }

        //        [TestMethod]
        //        [Owner("brchon")]
        //        public async Task TestForUnobservedExceptions()
        //        {
        //            this.unhandledException = false;
        //            TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;

        //            Random random = new Random();
        //            for (int iteration = 0; iteration < iterations; iteration++)
        //            {
        //                ItemProducer ItemProducer = MockItemProducerFactory.Create(
        //                    initialPageSize: 1,
        //                    throwExceptions: true);

        //                // BufferMore
        //                // Fire and Forget this task.
        //#pragma warning disable 4014
        //                Task.Run(async () => await BufferMoreInBackground(ItemProducer));
        //#pragma warning restore 4014

        //                bool ItemProducerFaulted = false;
        //                bool hasMoreResults = true;
        //                // MoveNextAsync
        //                while (hasMoreResults && !ItemProducerFaulted)
        //                {
        //                    try
        //                    {
        //                        hasMoreResults = await ItemProducer.MoveNextAsync(CancellationToken.None);
        //                        await Task.Delay(10);
        //                    }
        //                    catch (Exception)
        //                    {
        //                        ItemProducerFaulted = true;
        //                    }
        //                }
        //            }

        //            this.AssertNoUnhandledException();
        //        }

        //        private async Task BufferMoreInBackground(ItemProducer ItemProducer)
        //        {
        //            while (true)
        //            {
        //                await ItemProducer.BufferMoreItems(CancellationToken.None);
        //                await Task.Delay(10);
        //            }
        //        }

        //        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Justification = "Function Reviewed")]
        //        private void AssertNoUnhandledException()
        //        {
        //            GC.Collect();
        //            GC.WaitForPendingFinalizers();
        //            Assert.IsFalse(this.unhandledException, "Caught unhandled exception");
        //        }

        //        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        //        {
        //            this.unhandledException = true;
        //        }

        //[TestMethod]
        //public async Task TestEmptyPages()
        //{
        //        ItemProducer ItemProducer = MockItemProducerFactory.Create(
        //            initialPageSize: 10,
        //            returnEmptyPages: true);

        //        List ItemsRead = new List();
        //        await ItemProducer.MoveNextAsync(CancellationToken.None);
        //        while (ItemProducer.HasMoreResults)
        //        {
        //            ItemsRead.Add(ItemProducer.Current);
        //            await ItemProducer.MoveNextAsync(CancellationToken.None);
        //        }

        //        Assert.IsTrue(ItemsRead.SequenceEqual(MockItemProducerFactory.Dataset));
        //    }
        //}
    }
}
