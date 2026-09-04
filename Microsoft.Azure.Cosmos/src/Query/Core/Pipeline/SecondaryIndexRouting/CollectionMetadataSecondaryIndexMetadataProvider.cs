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
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;
    using DocumentClientException = Microsoft.Azure.Documents.DocumentClientException;
    using HttpConstants = Microsoft.Azure.Documents.HttpConstants;
    using TraceLevel = Microsoft.Azure.Cosmos.Tracing.TraceLevel;

    /// <summary>
    /// Discovers secondary indexes from collection secondaryIndexesMetadata and normalizes them to
    /// the provider-neutral query-routing contract.
    /// </summary>
    internal sealed class CollectionMetadataSecondaryIndexMetadataProvider : ISecondaryIndexMetadataProvider
    {
        internal const string GlobalSecondaryIndexContainerType = "GlobalSecondaryIndex";
        internal const string WildcardProjectionPath = "/*";

        private readonly DocumentClient documentClient;

        internal CollectionMetadataSecondaryIndexMetadataProvider(DocumentClient documentClient)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
        }

        public async Task<IReadOnlyList<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
            string sourceCollectionRid,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceCollectionRid))
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            using ITrace discoveryTrace = (trace ?? NoOpTrace.Singleton).StartChild(
                "CollectionMetadataSecondaryIndexDiscovery",
                TraceComponent.Query,
                TraceLevel.Info);

            ClientCollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync(discoveryTrace);
            ContainerProperties source = await ResolveByRidAsync(collectionCache, sourceCollectionRid, discoveryTrace, cancellationToken);
            IReadOnlyList<MaterializedViewProperties> mvReferences = source.MaterializedViews;
            if (mvReferences == null || mvReferences.Count == 0)
            {
                return Array.Empty<ISecondaryIndexMetadata>();
            }

            List<ISecondaryIndexMetadata> secondaryIndexesMetadata = new List<ISecondaryIndexMetadata>();
            HashSet<string> discoveredRids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MaterializedViewProperties mvReference in mvReferences
                .Where(mvReference =>
                    !string.IsNullOrWhiteSpace(mvReference?.ResourceId)
                    && IsGlobalSecondaryIndexContainerType(mvReference.ContainerType))
                .OrderBy(mvReference => mvReference.ResourceId, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!discoveredRids.Add(mvReference.ResourceId))
                {
                    continue;
                }

                ContainerProperties candidate = await ResolveByRidAsync(collectionCache, mvReference.ResourceId, discoveryTrace, cancellationToken);
                SecondaryIndexMetadata secondaryIndexMetadata = TryCreateMetadata(candidate, source);
                if (secondaryIndexMetadata != null)
                {
                    secondaryIndexesMetadata.Add(secondaryIndexMetadata);
                }
            }

            discoveryTrace.AddDatum("SecondaryIndexDiscovery.CandidateCount", secondaryIndexesMetadata.Count);
            return secondaryIndexesMetadata.AsReadOnly();
        }

        private static async Task<ContainerProperties> ResolveByRidAsync(
            ClientCollectionCache collectionCache,
            string collectionRid,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            try
            {
                return await collectionCache.ResolveByRidAsync(
                    HttpConstants.Versions.CurrentVersion,
                    collectionRid,
                    trace,
                    clientSideRequestStatistics: null,
                    cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                throw CosmosExceptionFactory.Create(exception, trace);
            }
        }

        internal static bool IsMaterializedViewForSource(
            ContainerProperties candidate,
            ContainerProperties source)
        {
            MaterializedViewDefinition definition = candidate?.MaterializedViewDefinition;
            return definition != null
                && IsGlobalSecondaryIndexContainerType(definition.ContainerType)
                && source != null
                && string.Equals(definition.SourceContainerResourceId, source.ResourceId, StringComparison.Ordinal)
                && string.Equals(definition.SourceContainerId, source.Id, StringComparison.Ordinal);
        }
       
        // Query parsing logic is not exhaustive. This is intended to cover MVP scenarios, which intentionally limits possible defintion queries.
        internal static bool TryGetIncludedProperties(
            MaterializedViewDefinition definition,
            ContainerProperties source,
            out IReadOnlyDictionary<string, string> includedProperties)
        {
            includedProperties = null;
            if (string.IsNullOrWhiteSpace(definition?.Definition)
                || source == null
                || !SqlQueryParser.TryParse(definition.Definition, out SqlQuery query))
            {
                return false;
            }

            if (!TryGetRootCollectionIdentifier(query.FromClause, out string rootCollectionIdentifier))
            {
                return false;
            }

            Dictionary<string, string> projections = new Dictionary<string, string>(StringComparer.Ordinal);
            if (query.SelectClause.SelectSpec is SqlSelectStarSpec)
            {
                projections[WildcardProjectionPath] = WildcardProjectionPath;
                foreach (string partitionKeyPath in source.PartitionKeyPaths ?? Array.Empty<string>())
                {
                    projections[partitionKeyPath] = partitionKeyPath;
                }

                includedProperties = projections;
                return true;
            }

            if (query.SelectClause.SelectSpec is not SqlSelectListSpec selectList)
            {
                return false;
            }

            foreach (SqlSelectItem item in selectList.Items)
            {
                if (!TryGetSourcePathSegments(
                    item.Expression,
                    rootCollectionIdentifier,
                    out IReadOnlyList<string> sourcePathSegments))
                {
                    return false;
                }

                string sourcePath = ToCanonicalPath(sourcePathSegments);
                string projectedProperty = item.Alias?.Value ?? sourcePathSegments[sourcePathSegments.Count - 1];
                if (string.IsNullOrEmpty(projectedProperty))
                {
                    return false;
                }

                projections[sourcePath] = ToCanonicalPath(new[] { projectedProperty });
            }

            if (projections.Count == 0)
            {
                return false;
            }

            includedProperties = projections;
            return true;
        }

        private static SecondaryIndexMetadata TryCreateMetadata(
            ContainerProperties candidate,
            ContainerProperties source)
        {
            if (!IsMaterializedViewForSource(candidate, source)
                || string.IsNullOrWhiteSpace(candidate.Id)
                || string.IsNullOrWhiteSpace(candidate.ResourceId)
                || candidate.PartitionKey == null
                || candidate.IndexingPolicy == null
                || IsFilteredMaterializedView(candidate.MaterializedViewDefinition)
                || !TryGetIncludedProperties(candidate.MaterializedViewDefinition, source, out IReadOnlyDictionary<string, string> includedProperties))
            {
                return null;
            }

            // MV secondaryIndexesMetadata does not expose synchronization consistency; current MV-backed indexes are Eventual.
            return new SecondaryIndexMetadata(
                candidate.Id,
                candidate.ResourceId,
                source.ResourceId,
                Clone(candidate.PartitionKey),
                Clone(candidate.IndexingPolicy),
                includedProperties,
                ConsistencyLevel.Eventual);
        }

        internal static bool IsFilteredMaterializedView(MaterializedViewDefinition definition)
        {
            return !string.IsNullOrWhiteSpace(definition?.Definition)
                && SqlQueryParser.TryParse(definition.Definition, out SqlQuery query)
                && query.WhereClause != null;
        }

        private static bool IsGlobalSecondaryIndexContainerType(string containerType)
        {
            return string.IsNullOrWhiteSpace(containerType)
                || string.Equals(containerType, GlobalSecondaryIndexContainerType, StringComparison.OrdinalIgnoreCase);
        }

        private static T Clone<T>(T value)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
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

        private static string ToCanonicalPath(IEnumerable<string> segments)
        {
            return "/" + string.Join("/", segments.Select(ToCanonicalPathSegment));
        }

        private static string ToCanonicalPathSegment(string segment)
        {
            return segment.All(character => char.IsLetterOrDigit(character) || character == '_')
                ? segment
                : JsonConvert.ToString(segment);
        }
    }
}
