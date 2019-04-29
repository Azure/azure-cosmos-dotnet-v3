//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// <see cref="SpatialSpec"/> fluent definition.
    /// </summary>
    public abstract class SpatialIndexFluentDefinition : FluentSettings<IndexingPolicyFluentDefinition>
    {
        /// <summary>
        /// Adds a path to the current <see cref="SpatialSpec"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <returns></returns>
        public abstract SpatialIndexFluentDefinition WithPath(string path);

        /// <summary>
        /// Add a path to the current <see cref="SpatialSpec"/> definition with a particular set of <see cref="SpatialType"/>s.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="spatialTypes">Set of <see cref="SpatialType"/> to apply to the path.</param>
        /// <returns></returns>
        public abstract SpatialIndexFluentDefinition WithPath(
            string path,
            params SpatialType[] spatialTypes);
    }
}
