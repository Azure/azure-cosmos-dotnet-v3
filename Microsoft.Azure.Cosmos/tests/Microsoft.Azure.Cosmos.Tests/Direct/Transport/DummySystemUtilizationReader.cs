// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Helper class to mock the <see cref="SystemUtilizationReaderBase"/> class.
    /// </summary>
    internal class DummySystemUtilizationReader : SystemUtilizationReaderBase
    {
        /// <summary>
        /// Mocks the current CPU usage for the purpose of the test.
        /// </summary>
        private readonly float cpuUsage;

        /// <summary>
        /// Initializes a new instance of the <see cref="DummySystemUtilizationReader"/> class.
        /// </summary>
        /// <param name="cpuUsage">A float value containing the CPU usage.</param>
        public DummySystemUtilizationReader(float cpuUsage) : base()
        {
            this.cpuUsage = cpuUsage;
        }

        /// <inheritdoc/>
        protected override float GetSystemWideCpuUsageCore()
        {
            return this.cpuUsage;
        }

        /// <inheritdoc/>
        protected override long? GetSystemWideMemoryAvailabiltyCore()
        {
            return null;
        }
    }
}