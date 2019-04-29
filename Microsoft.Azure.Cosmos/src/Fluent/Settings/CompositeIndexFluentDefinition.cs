//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// Composite Index fluent definition.
    /// </summary>
    public abstract class CompositeIndexFluentDefinition : FluentSettings<IndexingPolicyFluentDefinition>
    {
        /// <summary>
        /// Add a path to the current <see cref="CompositePath"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        public abstract CompositeIndexFluentDefinition Path(string path);

        /// <summary>
        /// Add a path to the current <see cref="CompositePath"/> definition with a particular <see cref="CompositePathSortOrder"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="sortOrder"><see cref="CompositePathSortOrder"/> to apply on the path.</param>
        /// <returns></returns>
        public abstract CompositeIndexFluentDefinition Path(
            string path,
            CompositePathSortOrder sortOrder);
    }
}
