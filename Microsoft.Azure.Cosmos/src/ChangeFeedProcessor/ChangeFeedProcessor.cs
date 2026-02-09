//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
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
        /// Exports all leases from the lease container.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A list of lease objects as JSON elements.</returns>
        /// <remarks>
        /// <para>
        /// Each exported lease is a JSON object representing the serialized lease state.
        /// The payload should not be modified and is intended to be passed directly to <see cref="ImportLeasesAsync"/>.
        /// </para>
        /// <para>
        /// This operation can be performed while the processor is running.
        /// </para>
        /// </remarks>
        /// <exception cref="NotSupportedException">Thrown when the implementation does not support export.</exception>
        public virtual Task<IReadOnlyList<JsonElement>> ExportLeasesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This ChangeFeedProcessor implementation does not support lease export.");
        }

        /// <summary>
        /// Imports leases into the lease container.
        /// </summary>
        /// <param name="leases">The list of lease objects as JSON elements to import.</param>
        /// <param name="overwriteExisting">Whether to overwrite existing leases with the same token. Default is false.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// The lease objects should be the opaque JSON elements obtained from <see cref="ExportLeasesAsync"/>.
        /// </para>
        /// <para>
        /// If <paramref name="overwriteExisting"/> is false (default), existing leases will not be modified.
        /// If true, existing leases will be replaced with the imported data.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="leases"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the implementation does not support import.</exception>
        public virtual Task ImportLeasesAsync(
            IReadOnlyList<JsonElement> leases,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This ChangeFeedProcessor implementation does not support lease import.");
        }
    }
}