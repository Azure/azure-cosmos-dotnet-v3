//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// Base abstract class to define Settings with Fluent configurations.
    /// </summary>
    public abstract class FluentSettings<T>
    {
        /// <summary>
        /// Empty constructor that can be used for unit testing.
        /// </summary>
        public FluentSettings() { }

        /// <summary>
        /// Attaches the current Setting to a parent Settings container.
        /// </summary>
        public abstract T Attach();
    }
}
