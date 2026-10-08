//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Immutable configuration for an <see cref="RntbdConnectionManager"/> instance.
    /// Derived from <see cref="ChannelProperties"/> at manager construction time so the
    /// manager does not have to reach back into the shared properties bag for hot-path
    /// decisions.
    /// </summary>
    /// <remarks>
    /// Per-design, the manager intentionally has no per-attempt timer. The
    /// existing <see cref="ChannelProperties.OpenTimeout"/> and
    /// <see cref="ChannelProperties.LocalRegionOpenTimeout"/> fields are *not* surfaced
    /// here on purpose — open attempts run until the protocol stack reports success
    /// or failure.
    /// </remarks>
    internal sealed class ConnectionManagerOptions
    {
        public ConnectionManagerOptions(
            int maxChannels,
            int maxRequestsPerChannel)
        {
            Debug.Assert(maxChannels > 0);
            Debug.Assert(maxRequestsPerChannel > 0);

            this.MaxChannels = maxChannels;
            this.MaxRequestsPerChannel = maxRequestsPerChannel;
        }

        /// <summary>
        /// Maximum number of physical RNTBD channels (slots) the manager will hold
        /// open to its endpoint at one time.
        /// </summary>
        public int MaxChannels { get; }

        /// <summary>
        /// Maximum number of in-flight requests allowed per channel before the
        /// manager surfaces a <c>SlotsFull</c> outcome.
        /// </summary>
        public int MaxRequestsPerChannel { get; }

        /// <summary>
        /// Convenience constructor that derives the options from a
        /// <see cref="ChannelProperties"/> instance.
        /// </summary>
        public static ConnectionManagerOptions FromChannelProperties(ChannelProperties channelProperties)
        {
            if (channelProperties == null)
            {
                throw new ArgumentNullException(nameof(channelProperties));
            }

            return new ConnectionManagerOptions(
                maxChannels: channelProperties.MaxChannels,
                maxRequestsPerChannel: channelProperties.MaxRequestsPerChannel);
        }
    }
}
