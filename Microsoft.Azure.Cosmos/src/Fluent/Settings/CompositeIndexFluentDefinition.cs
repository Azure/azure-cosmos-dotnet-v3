//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Composite Index fluent definition.
    /// </summary>
    public class CompositeIndexFluentDefinition : FluentSettings<IndexingPolicyFluentDefinition>
    {
        private readonly Collection<CompositePath> compositePaths = new Collection<CompositePath>();

        internal CompositeIndexFluentDefinition(IndexingPolicyFluentDefinition indexingPolicyFluentDefinition) : base(indexingPolicyFluentDefinition)
        {
        }

        /// <summary>
        /// Add a path to the current <see cref="CompositePath"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        public virtual CompositeIndexFluentDefinition Path(string path)
        {
            this.compositePaths.Add(new CompositePath() { Path = path });
            return this;
        }

        /// <summary>
        /// Add a path to the current <see cref="CompositePath"/> definition with a particular <see cref="CompositePathSortOrder"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="sortOrder"><see cref="CompositePathSortOrder"/> to apply on the path.</param>
        /// <returns></returns>
        public virtual CompositeIndexFluentDefinition Path(
            string path, 
            CompositePathSortOrder sortOrder)
        {
            this.compositePaths.Add(new CompositePath() { Path = path, Order = sortOrder });
            return this;
        }
    }
}
