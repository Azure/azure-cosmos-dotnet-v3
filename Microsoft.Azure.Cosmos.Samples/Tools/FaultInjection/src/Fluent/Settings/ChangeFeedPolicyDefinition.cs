//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// <see cref="ChangeFeedPolicy"/> fluent definition.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class ChangeFeedPolicyDefinition
    {
        private readonly ContainerBuilder parent;
        private readonly Action<ChangeFeedPolicy> attachCallback;
        private TimeSpan changeFeedPolicyRetention;

        internal ChangeFeedPolicyDefinition(
            ContainerBuilder parent,
            TimeSpan retention,
            Action<ChangeFeedPolicy> attachCallback)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.attachCallback = attachCallback ?? throw new ArgumentNullException(nameof(attachCallback));
            this.changeFeedPolicyRetention = retention;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            ChangeFeedPolicy resolutionPolicy = new ChangeFeedPolicy
            {
                FullFidelityRetention = this.changeFeedPolicyRetention
            };

            this.attachCallback(resolutionPolicy);
            return this.parent;
        }
    }
}
