//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// Base abstract class to define Settings with Fluent configurations
    /// </summary>
    public abstract class FluentSettings<T>
    {
        private readonly T fluentBaseSettings;

        /// <summary>
        /// Empty constructor that can be used for unit testing
        /// </summary>
        public FluentSettings() { }

        /// <summary>
        /// Creates a base fluent Settings
        /// </summary>
        /// <param name="fluentBaseSettings"></param>
        internal FluentSettings(T fluentBaseSettings)
        {
            this.fluentBaseSettings = fluentBaseSettings;
        }

        /// <summary>
        /// Attaches the current Setting to a higher Settings container
        /// </summary>
        /// <returns></returns>
        public virtual T Attach()
        {
            return this.fluentBaseSettings;
        }
    }
}
