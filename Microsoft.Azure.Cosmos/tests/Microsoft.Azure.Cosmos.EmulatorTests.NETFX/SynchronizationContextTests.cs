//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SynchronizationContextTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            await base.TestCleanup();
        }

        [TestMethod]
        [Timeout(30000)]
        public void VerifySynchronizationContextDoesNotLock()
        {
            WindowsFormsSynchronizationContext synchronizationContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            this.database.ReadStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
