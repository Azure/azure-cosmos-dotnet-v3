//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tracing;
    using HttpConstants = Microsoft.Azure.Documents.HttpConstants;
    using TraceLevel = Microsoft.Azure.Cosmos.Tracing.TraceLevel;

    /// <summary>
    /// Discovers secondary indexes from collection secondaryIndexesMetadata and normalizes them to
    /// the provider-neutral query-routing contract.
    /// </summary>
    internal sealed class SecondaryIndexMetadataProvider : ISecondaryIndexMetadataProvider
    {
        private readonly DocumentClient documentClient;

        internal SecondaryIndexMetadataProvider(DocumentClient documentClient)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
        }

        public async Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.ResourceId == null)
            {
                throw new ArgumentException("The source container must have a resource identifier.", nameof(source));
            }

            using ITrace discoveryTrace = trace.StartChild("SecondaryIndexDiscovery", TraceComponent.Query, TraceLevel.Info);

            ClientCollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync(discoveryTrace);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<MaterializedViewProperties> mvReferences = source.MaterializedViews;

            List<ISecondaryIndexMetadata> secondaryIndexesMetadata = new ();
            foreach (MaterializedViewProperties mvReference in mvReferences)
            {
                ContainerProperties candidate = await collectionCache.ResolveByRidAsync(
                    HttpConstants.Versions.CurrentVersion,
                    mvReference.ResourceId,
                    discoveryTrace,
                    clientSideRequestStatistics: null,
                    cancellationToken);
                if (TryCreateMetadata(candidate, source, out SecondaryIndexMetadata secondaryIndexMetadata))
                {
                    secondaryIndexesMetadata.Add(secondaryIndexMetadata);
                }
            }

            discoveryTrace.AddDatum("SecondaryIndexDiscovery.CandidateCount", secondaryIndexesMetadata.Count);
            return secondaryIndexesMetadata;
        }

        private static bool IsMaterializedViewForSource(
            ContainerProperties candidate,
            ContainerProperties source)
        {
            if (candidate == null || source == null)
            {
                return false;
            }

            MaterializedViewDefinition definition = candidate.MaterializedViewDefinition;
            return definition != null
                && string.Equals(definition.SourceContainerResourceId, source.ResourceId, StringComparison.Ordinal)
                && string.Equals(definition.SourceContainerId, source.Id, StringComparison.Ordinal);
        }
       
        // Query parsing logic is temporary and not exhaustive. This is intended to cover MVP scenarios, which intentionally limits possible defintion queries.
        private static bool TryGetIncludedProperties(
            string definition,
            out IReadOnlyDictionary<string, PropertyPath> includedProperties)
        {
            includedProperties = null;
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!SqlQueryParser.TryParse(definition, out SqlQuery query)
                || query.WhereClause != null
                || !TryGetRootCollectionIdentifier(query.FromClause, out string rootCollectionIdentifier))
            {
                return false;
            }

            Dictionary<string, PropertyPath> projections = new (StringComparer.Ordinal);
            switch (query.SelectClause.SelectSpec)
            {
                case SqlSelectListSpec selectList:
                    foreach (SqlSelectItem item in selectList.Items)
                    {
                        if (!TryGetSourcePathSegments(
                            item.Expression,
                            rootCollectionIdentifier,
                            out IReadOnlyList<string> sourcePathSegments))
                        {
                            return false;
                        }

                        PropertyPath sourcePath = new PropertyPath(sourcePathSegments);
                        string projectedProperty = item.Alias?.Value ?? sourcePathSegments[sourcePathSegments.Count - 1];
                        if (projectedProperty == null)
                        {
                            return false;
                        }

                        if (projections.TryGetValue(projectedProperty, out PropertyPath existingPath) && !existingPath.Equals(sourcePath))
                        {
                            return false;
                        }

                        projections[projectedProperty] = sourcePath;
                    }

                    break;

                default:
                    return false;
            }

            if (projections.Count == 0)
            {
                return false;
            }

            includedProperties = projections;
            return true;
        }

        private static bool TryCreateMetadata(
            ContainerProperties candidate,
            ContainerProperties source,
            out SecondaryIndexMetadata metadata)
        {
            metadata = null;
            if (!IsMaterializedViewForSource(candidate, source)
                || candidate.Id == null
                || candidate.ResourceId == null
                || candidate.PartitionKey == null
                || candidate.IndexingPolicy == null)
            {
                return false;
            }

            MaterializedViewDefinition definition = candidate.MaterializedViewDefinition;
            if (!TryGetIncludedProperties(definition.Definition, out IReadOnlyDictionary<string, PropertyPath> includedProperties))
            {
                return false;
            }

            metadata = new SecondaryIndexMetadata(
                candidate.Id,
                candidate.ResourceId,
                source.ResourceId,
                candidate.PartitionKey,
                candidate.IndexingPolicy,
                includedProperties,
                ConsistencyLevel.Eventual); // MV secondaryIndexesMetadata does not expose consistency level. Current MV-backed indexes are Eventual.
            return true;
        }

        private static bool TryGetRootCollectionIdentifier(
            SqlFromClause fromClause,
            out string rootCollectionIdentifier)
        {
            rootCollectionIdentifier = null;
            if (fromClause?.Expression is not SqlAliasedCollectionExpression aliasedCollection
                || aliasedCollection.Collection is not SqlInputPathCollection inputPathCollection
                || inputPathCollection.RelativePath != null)
            {
                return false;
            }

            rootCollectionIdentifier = aliasedCollection.Alias?.Value ?? inputPathCollection.Input.Value;
            return !string.IsNullOrEmpty(rootCollectionIdentifier);
        }

        private static bool TryGetSourcePathSegments(
            SqlScalarExpression expression,
            string rootCollectionIdentifier,
            out IReadOnlyList<string> sourcePathSegments)
        {
            List<string> segments = new List<string>();
            while (expression != null)
            {
                switch (expression)
                {
                    case SqlPropertyRefScalarExpression propertyReference:
                        segments.Add(propertyReference.Identifier.Value);
                        expression = propertyReference.Member;
                        break;

                    case SqlMemberIndexerScalarExpression memberIndexer
                        when memberIndexer.Indexer is SqlLiteralScalarExpression literalExpression
                            && literalExpression.Literal is SqlStringLiteral stringLiteral:
                        segments.Add(stringLiteral.Value);
                        expression = memberIndexer.Member;
                        break;

                    default:
                        sourcePathSegments = null;
                        return false;
                }
            }

            if (segments.Count < 2)
            {
                sourcePathSegments = null;
                return false;
            }

            if (!string.Equals(segments[segments.Count - 1], rootCollectionIdentifier, StringComparison.Ordinal))
            {
                sourcePathSegments = null;
                return false;
            }

            segments.RemoveAt(segments.Count - 1);
            segments.Reverse();
            sourcePathSegments = segments;
            return true;
        }
    }
}
