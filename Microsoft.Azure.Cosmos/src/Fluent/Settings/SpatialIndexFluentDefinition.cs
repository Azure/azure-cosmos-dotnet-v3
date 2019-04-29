//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// <see cref="SpatialSpec"/> fluent definition.
    /// </summary>
    public class SpatialIndexFluentDefinition : FluentSettings<IndexingPolicyFluentDefinition>
    {
        private readonly SpatialSpec spatialSpec = new SpatialSpec();

        internal SpatialIndexFluentDefinition(IndexingPolicyFluentDefinition indexingPolicyFluentDefinition) : base(indexingPolicyFluentDefinition)
        {
        }

        /// <summary>
        /// Adds a path to the current <see cref="SpatialSpec"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <returns></returns>
        public virtual SpatialIndexFluentDefinition WithPath(string path)
        {
            this.spatialSpec.Path = path;
            return this;
        }

        /// <summary>
        /// Add a path to the current <see cref="SpatialSpec"/> definition with a particular set of <see cref="SpatialType"/>s.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="spatialTypes">Set of <see cref="SpatialType"/> to apply to the path.</param>
        /// <returns></returns>
        public virtual SpatialIndexFluentDefinition WithPath(
            string path, 
            params SpatialType[] spatialTypes)
        {
            this.spatialSpec.Path = path;

            foreach (SpatialType spatialType in spatialTypes)
            {
                this.spatialSpec.SpatialTypes.Add(spatialType);
            }

            return this;
        }
    }
}
