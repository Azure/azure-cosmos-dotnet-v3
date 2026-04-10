//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class TaskHelperTests
    {
        private readonly CancellationToken cancellationToken = new CancellationToken();

        [TestMethod]
        public async Task CancellationTokenIsPassedToTask()
        {
            await TaskHelper.RunInlineIfNeededAsync(() => this.RunAndAssertAsync(this.cancellationToken));
        }

        [TestMethod]
        public async Task CancellationTokenIsPassedToTask_WhenSyncContextPresent()
        {
            Mock<SynchronizationContext> mockSynchronizationContext = new Mock<SynchronizationContext>()
            {
                CallBase = true
            };

            try
            {
                SynchronizationContext.SetSynchronizationContext(mockSynchronizationContext.Object);
                await TaskHelper.RunInlineIfNeededAsync(() => this.RunAndAssertAsync(this.cancellationToken));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        public Task<bool> RunAndAssertAsync(CancellationToken cancellationToken)
        {
            Assert.AreEqual(this.cancellationToken, cancellationToken);
            return Task.FromResult(true);
        }
    }
}