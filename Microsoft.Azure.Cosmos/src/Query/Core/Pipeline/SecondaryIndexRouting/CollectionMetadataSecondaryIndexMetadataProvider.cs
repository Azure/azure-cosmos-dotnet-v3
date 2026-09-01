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
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Discovers secondary indexes from collection metadata and normalizes them to
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

            using ITrace discoveryTrace = 
                (trace ?? NoOpTrace.Singleton).StartChild("CollectionMetadataSecondaryIndexDiscovery", TraceComponent.Query, Tracing.TraceLevel.Info);

            Routing.ClientCollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync(discoveryTrace);
            ContainerProperties source = await ResolveByRidAsync(collectionCache, sourceCollectionRid, discoveryTrace, cancellationToken);
            IReadOnlyList<MaterializedViewProperties> mvReferences = source.MaterializedViews;
            if (mvReferences == null || mvReferences.Count == 0)
            {
                return Array.Empty<ISecondaryIndexMetadata>();
            }

            List<ISecondaryIndexMetadata> metadata = new List<ISecondaryIndexMetadata>();
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
                SecondaryIndexMetadata normalized = TryCreateMetadata(candidate, source);
                if (normalized != null)
                {
                    metadata.Add(normalized);
                }
            }

            discoveryTrace.AddDatum("SecondaryIndexDiscovery.CandidateCount", metadata.Count);
            return metadata.AsReadOnly();
        }

        private static async Task<ContainerProperties> ResolveByRidAsync(
            Routing.ClientCollectionCache collectionCache,
            string collectionRid,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            try
            {
                return await collectionCache.ResolveByRidAsync(
                    HttpConstants.Versions.CurrentVersion, 
                    collectionRid, trace, 
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
            Cosmos.MaterializedViewDefinition definition = candidate?.MaterializedViewDefinition;
            return definition != null
                && IsGlobalSecondaryIndexContainerType(definition.ContainerType)
                && source != null
                && (string.Equals(definition.SourceContainerResourceId, source.ResourceId, StringComparison.Ordinal)
                    || string.Equals(definition.SourceContainerId, source.Id, StringComparison.Ordinal));
        }

        internal static bool TryGetIncludedProperties(
            Cosmos.MaterializedViewDefinition definition,
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

            if (query.WhereClause != null)
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
                if (!TryGetSourcePath(item.Expression, out string sourcePath))
                {
                    continue;
                }

                string projectedProperty = item.Alias?.Value ?? GetLastPathSegment(sourcePath);
                if (!string.IsNullOrEmpty(projectedProperty))
                {
                    projections[sourcePath] = $"/{projectedProperty}";
                }
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
                || string.IsNullOrWhiteSpace(candidate.ResourceId)
                || candidate.PartitionKey == null
                || candidate.IndexingPolicy == null
                || !TryGetIncludedProperties(candidate.MaterializedViewDefinition, source, out IReadOnlyDictionary<string, string> includedProperties))
            {
                return null;
            }

            return new SecondaryIndexMetadata(
                candidate.ResourceId,
                source.ResourceId,
                Clone(candidate.PartitionKey),
                Clone(candidate.IndexingPolicy),
                includedProperties,
                Cosmos.ConsistencyLevel.Eventual);
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

        private static bool TryGetSourcePath(
            SqlScalarExpression expression,
            out string sourcePath)
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
                        sourcePath = null;
                        return false;
                }
            }

            if (segments.Count < 2)
            {
                sourcePath = null;
                return false;
            }

            segments.Reverse();
            sourcePath = "/" + string.Join("/", segments.Skip(1));
            return true;
        }

        private static string GetLastPathSegment(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator >= 0 && separator < path.Length - 1 ? path.Substring(separator + 1) : null;
        }
    }
}
