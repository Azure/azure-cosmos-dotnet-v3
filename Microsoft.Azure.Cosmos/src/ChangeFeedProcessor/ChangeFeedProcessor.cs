//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides an API to start and stop a <see cref="ChangeFeedProcessor"/> instance created by <see cref="ChangeFeedProcessorBuilder.Build"/>.
    /// </summary>
    public abstract class ChangeFeedProcessor
    {
        /// <summary>
        /// Start listening for changes.
        /// </summary>
        /// <returns>A <see cref="Task"/>.</returns>
        public abstract Task StartAsync();

        /// <summary>
        /// Stops listening for changes.
        /// </summary>
        /// <returns>A <see cref="Task"/>.</returns>
        public abstract Task StopAsync();

        /// <summary>
        /// Exports all leases from the lease container to a list of <see cref="LeaseExportData"/> objects.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A list of exported lease data.</returns>
        /// <remarks>
        /// <para>
        /// The processor must be stopped before calling this method. If the processor is running,
        /// an <see cref="InvalidOperationException"/> will be thrown.
        /// </para>
        /// <para>
        /// The export includes all lease metadata including continuation tokens, ownership history,
        /// and custom properties. The exported data can be used to restore leases using <see cref="ImportLeasesAsync"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the processor is currently running.</exception>
        /// <exception cref="NotSupportedException">Thrown when the implementation does not support export.</exception>
        public virtual Task<IReadOnlyList<LeaseExportData>> ExportLeasesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This ChangeFeedProcessor implementation does not support lease export.");
        }

        /// <summary>
        /// Imports leases from a list of <see cref="LeaseExportData"/> objects into the lease container.
        /// </summary>
        /// <param name="leases">The list of lease data to import.</param>
        /// <param name="overwriteExisting">Whether to overwrite existing leases with the same ID. Default is false.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// The processor must be stopped before calling this method. If the processor is running,
        /// an <see cref="InvalidOperationException"/> will be thrown.
        /// </para>
        /// <para>
        /// When importing, the ownership is transferred to the current processor instance,
        /// and the ownership history is updated to reflect the import action.
        /// </para>
        /// <para>
        /// If <paramref name="overwriteExisting"/> is false (default), existing leases will not be modified.
        /// If true, existing leases will be replaced with the imported data.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="leases"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the processor is currently running.</exception>
        /// <exception cref="NotSupportedException">Thrown when the implementation does not support import.</exception>
        public virtual Task ImportLeasesAsync(
            IReadOnlyList<LeaseExportData> leases,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This ChangeFeedProcessor implementation does not support lease import.");
        }
    }
}