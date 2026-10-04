//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    internal sealed class ContainerMetadataSecondaryIndexMetadataProvider : ISecondaryIndexMetadataProvider
    {
        internal const string GlobalSecondaryIndexContainerType = "GlobalSecondaryIndex";

        private readonly DocumentClient documentClient;

        internal ContainerMetadataSecondaryIndexMetadataProvider(DocumentClient documentClient)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
        }

        public async Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
            string sourceCollectionRid,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            if (sourceCollectionRid == null)
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            using ITrace discoveryTrace = trace.StartChild("ContainerMetadataSecondaryIndexDiscovery", TraceComponent.Query, TraceLevel.Info);

            ClientCollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync(discoveryTrace);
            ContainerProperties source = await collectionCache.ResolveByRidAsync(
                HttpConstants.Versions.CurrentVersion,
                sourceCollectionRid,
                discoveryTrace,
                clientSideRequestStatistics: null,
                cancellationToken);
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
            return secondaryIndexesMetadata.AsReadOnly();
        }

        private static bool IsMaterializedViewForSource(
            ContainerProperties candidate,
            ContainerProperties source)
        {
            MaterializedViewDefinition definition = candidate?.MaterializedViewDefinition;
            return definition != null
                && source != null
                && string.Equals(definition.SourceContainerResourceId, source.ResourceId, StringComparison.Ordinal)
                && string.Equals(definition.SourceContainerId, source.Id, StringComparison.Ordinal);
        }
       
        // Query parsing logic is temporary and not exhaustive. This is intended to cover MVP scenarios, which intentionally limits possible defintion queries.
        private static bool TryGetIncludedProperties(
            string definition,
            ContainerProperties source,
            out IReadOnlyDictionary<PropertyPath, PropertyPath> includedProperties)
        {
            includedProperties = null;
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!SqlQueryParser.TryParse(definition, out SqlQuery query)
                || query.WhereClause != null
                || !TryGetRootCollectionIdentifier(query.FromClause, out string rootCollectionIdentifier))
            {
                return false;
            }

            Dictionary<PropertyPath, PropertyPath> projections = new ();
            switch (query.SelectClause.SelectSpec)
            {
                case SqlSelectStarSpec:
                    projections[PropertyPath.Wildcard] = PropertyPath.Wildcard;
                    foreach (string partitionKeyPath in source.PartitionKeyPaths)
                    {
                        PropertyPath path = new PropertyPath(PathParser.GetPathParts(partitionKeyPath));
                        projections[path] = path;
                    }

                    break;

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

                        projections[sourcePath] = new PropertyPath(new[] { projectedProperty });
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
            if (!TryGetIncludedProperties(definition.Definition, source, out IReadOnlyDictionary<PropertyPath, PropertyPath> includedProperties))
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

            segments.Reverse();
            if (!string.Equals(segments[0], rootCollectionIdentifier, StringComparison.Ordinal))
            {
                sourcePathSegments = null;
                return false;
            }

            sourcePathSegments = segments.Skip(1).ToArray();
            return true;
        }
    }
}
