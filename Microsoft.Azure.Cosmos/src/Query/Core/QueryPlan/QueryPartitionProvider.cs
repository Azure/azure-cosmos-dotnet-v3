//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;
    using PartitionKind = Documents.PartitionKind;
    using ServiceInteropWrapper = Documents.ServiceInteropWrapper;

    internal sealed class QueryPartitionProvider : IDisposable
    {
        private static readonly int InitialBufferSize = 1024;
#pragma warning disable SA1310 // Field names should not contain underscore
        private static readonly uint DISP_E_BUFFERTOOSMALL = 0x80020013;
#pragma warning restore SA1310 // Field names should not contain underscore
        private static readonly PartitionedQueryExecutionInfoInternal DefaultInfoInternal = new PartitionedQueryExecutionInfoInternal
        {
            QueryInfo = new QueryInfo(),
            QueryRanges = new List<Documents.Routing.Range<PartitionKeyInternal>>
                    {
                        new Documents.Routing.Range<PartitionKeyInternal>(
                            PartitionKeyInternal.InclusiveMinimum,
                            PartitionKeyInternal.ExclusiveMaximum,
                            true,
                            false)
                    }
        };

        private readonly object serviceProviderStateLock;

        private IntPtr serviceProvider;
        private bool disposed;
        private string queryengineConfiguration;

        // TODO: Move this into a config class of its own
        public bool ClientDisableOptimisticDirectExecution { get; private set; }

        public QueryPartitionProvider(IDictionary<string, object> queryengineConfiguration)
        {
            if (queryengineConfiguration == null)
            {
                throw new ArgumentNullException("queryengineConfiguration");
            }

            if (queryengineConfiguration.Count == 0)
            {
                throw new ArgumentException("queryengineConfiguration cannot be empty!");
            }

            this.disposed = false;
            this.queryengineConfiguration = JsonConvert.SerializeObject(queryengineConfiguration);
            this.ClientDisableOptimisticDirectExecution = GetClientDisableOptimisticDirectExecution((IReadOnlyDictionary<string, object>)queryengineConfiguration);
            this.serviceProvider = IntPtr.Zero;

            this.serviceProviderStateLock = new object();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Update(IDictionary<string, object> queryengineConfiguration)
        {
            if (queryengineConfiguration == null)
            {
                throw new ArgumentNullException("queryengineConfiguration");
            }

            if (queryengineConfiguration.Count == 0)
            {
                throw new ArgumentException("queryengineConfiguration cannot be empty!");
            }

            if (!this.disposed)
            {
                lock (this.serviceProviderStateLock)
                {
                    string newConfiguration = JsonConvert.SerializeObject(queryengineConfiguration);

                    if (!string.Equals(this.queryengineConfiguration, newConfiguration))
                    {
                        this.queryengineConfiguration = newConfiguration;
                        this.ClientDisableOptimisticDirectExecution = GetClientDisableOptimisticDirectExecution((IReadOnlyDictionary<string, object>)queryengineConfiguration);

                        if (!this.disposed && this.serviceProvider != IntPtr.Zero)
                        {
                            uint errorCode = ServiceInteropWrapper.UpdateServiceProvider(
                                this.serviceProvider,
                                this.queryengineConfiguration);

                            Exception exception = Marshal.GetExceptionForHR((int)errorCode);
                            if (exception != null) throw exception;
                        }
                    }
                }
            }
            else
            {
                throw new ObjectDisposedException(typeof(QueryPartitionProvider).Name);
            }
        }

        public TryCatch<PartitionedQueryExecutionInfo> TryGetPartitionedQueryExecutionInfo(
            string querySpecJsonString,
            PartitionKeyDefinition partitionKeyDefinition,
            VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount,
            bool useSystemPrefix,
            GeospatialType geospatialType)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> tryGetInternalQueryInfo = this.TryGetPartitionedQueryExecutionInfoInternal(
                querySpecJsonString: querySpecJsonString,
                partitionKeyDefinition: partitionKeyDefinition,
                vectorEmbeddingPolicy: vectorEmbeddingPolicy,
                requireFormattableOrderByQuery: requireFormattableOrderByQuery,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                hasLogicalPartitionKey: hasLogicalPartitionKey,
                allowDCount: allowDCount,
                useSystemPrefix: useSystemPrefix,
                geospatialType: geospatialType);

            if (!tryGetInternalQueryInfo.Succeeded)
            {
                return TryCatch<PartitionedQueryExecutionInfo>.FromException(tryGetInternalQueryInfo.Exception);
            }

            PartitionedQueryExecutionInfo queryInfo = this.ConvertPartitionedQueryExecutionInfo(tryGetInternalQueryInfo.Result, partitionKeyDefinition);
            return TryCatch<PartitionedQueryExecutionInfo>.FromResult(queryInfo);
        }

        private static bool GetClientDisableOptimisticDirectExecution(IReadOnlyDictionary<string, object> queryengineConfiguration)
        {
            if (queryengineConfiguration.TryGetValue(CosmosQueryExecutionContextFactory.ClientDisableOptimisticDirectExecution, out object queryConfigProperty))
            {
                return (bool)queryConfigProperty;
            }

            return false;
        }

        internal PartitionedQueryExecutionInfo ConvertPartitionedQueryExecutionInfo(
            PartitionedQueryExecutionInfoInternal queryInfoInternal,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            List<Documents.Routing.Range<string>> effectiveRanges = new List<Documents.Routing.Range<string>>(queryInfoInternal.QueryRanges.Count);
            foreach (Documents.Routing.Range<PartitionKeyInternal> internalRange in queryInfoInternal.QueryRanges)
            {
                effectiveRanges.Add(PartitionKeyInternal.GetEffectivePartitionKeyRange(partitionKeyDefinition, internalRange));
            }

            effectiveRanges.Sort(Documents.Routing.Range<string>.MinComparer.Instance);

            return new PartitionedQueryExecutionInfo()
            {
                QueryInfo = queryInfoInternal.QueryInfo,
                QueryRanges = effectiveRanges,
                HybridSearchQueryInfo = queryInfoInternal.HybridSearchQueryInfo,
            };
        }

        internal TryCatch<PartitionedQueryExecutionInfoInternal> TryGetPartitionedQueryExecutionInfoInternal(
            string querySpecJsonString,
            PartitionKeyDefinition partitionKeyDefinition,
            VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount,
            bool useSystemPrefix,
            GeospatialType geospatialType)
        {
            if (querySpecJsonString == null || partitionKeyDefinition == null)
            {
                return TryCatch<PartitionedQueryExecutionInfoInternal>.FromResult(DefaultInfoInternal);
            }

            List<string> paths = new List<string>(partitionKeyDefinition.Paths);
            List<IReadOnlyList<string>> pathPartsList = new List<IReadOnlyList<string>>(paths.Count);
            uint[] partsLengths = new uint[paths.Count];
            int allPartsLength = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                IReadOnlyList<string> pathParts = PathParser.GetPathParts(paths[i]);
                partsLengths[i] = (uint)pathParts.Count;
                pathPartsList.Add(pathParts);
                allPartsLength += pathParts.Count;
            }

            string[] allParts = new string[allPartsLength];
            int allPartsIndex = 0;
            foreach (IReadOnlyList<string> pathParts in pathPartsList)
            {
                foreach (string part in pathParts)
                {
                    allParts[allPartsIndex++] = part;
                }
            }

            PartitionKind partitionKind = partitionKeyDefinition.Kind;

            this.Initialize();

            Span<byte> buffer = stackalloc byte[QueryPartitionProvider.InitialBufferSize];
            uint errorCode;
            uint serializedQueryExecutionInfoResultLength;

            string vectorEmbeddingPolicyString = vectorEmbeddingPolicy != null ?
                JsonConvert.SerializeObject(vectorEmbeddingPolicy) :
                null;

            unsafe
            {
                ServiceInteropWrapper.PartitionKeyRangesApiOptions partitionKeyRangesApiOptions =
                    new ServiceInteropWrapper.PartitionKeyRangesApiOptions()
                    {
                        bAllowDCount = Convert.ToInt32(allowDCount),
                        bAllowNonValueAggregateQuery = Convert.ToInt32(allowNonValueAggregateQuery),
                        bHasLogicalPartitionKey = Convert.ToInt32(hasLogicalPartitionKey),
                        bIsContinuationExpected = Convert.ToInt32(isContinuationExpected),
                        bRequireFormattableOrderByQuery = Convert.ToInt32(requireFormattableOrderByQuery),
                        bUseSystemPrefix = Convert.ToInt32(useSystemPrefix),
                        eGeospatialType = Convert.ToInt32(geospatialType),
                        ePartitionKind = Convert.ToInt32(partitionKind)
                    };

                fixed (byte* bytePtr = buffer)
                {
                    errorCode = ServiceInteropWrapper.GetPartitionKeyRangesFromQuery4(
                        this.serviceProvider,
                        querySpecJsonString,
                        partitionKeyRangesApiOptions,
                        allParts,
                        partsLengths,
                        (uint)partitionKeyDefinition.Paths.Count,
                        vectorEmbeddingPolicyString,
                        vectorEmbeddingPolicyString?.Length ?? 0,
                        new IntPtr(bytePtr),
                        (uint)buffer.Length,
                        out serializedQueryExecutionInfoResultLength);

                    if (errorCode == DISP_E_BUFFERTOOSMALL)
                    {
                        // Allocate on stack for smaller arrays, otherwise use heap.
                        buffer = serializedQueryExecutionInfoResultLength < 4096
                            ? stackalloc byte[(int)serializedQueryExecutionInfoResultLength]
                            : new byte[serializedQueryExecutionInfoResultLength];

                        fixed (byte* bytePtr2 = buffer)
                        {
                            errorCode = ServiceInteropWrapper.GetPartitionKeyRangesFromQuery4(
                                this.serviceProvider,
                                querySpecJsonString,
                                partitionKeyRangesApiOptions,
                                allParts,
                                partsLengths,
                                (uint)partitionKeyDefinition.Paths.Count,
                                vectorEmbeddingPolicyString,
                                vectorEmbeddingPolicyString?.Length ?? 0,
                                new IntPtr(bytePtr2),
                                (uint)buffer.Length,
                                out serializedQueryExecutionInfoResultLength);
                        }
                    }
                }
            }

            string serializedQueryExecutionInfo = Encoding.UTF8.GetString(buffer.Slice(0, (int)serializedQueryExecutionInfoResultLength));

            Exception exception = Marshal.GetExceptionForHR((int)errorCode);
            if (exception != null)
            {
                QueryPartitionProviderException queryPartitionProviderException;
                if (string.IsNullOrEmpty(serializedQueryExecutionInfo))
                {
                    queryPartitionProviderException = new UnexpectedQueryPartitionProviderException(
                        "Query service interop parsing hit an unexpected exception",
                        exception);
                }
                else
                {
                    queryPartitionProviderException = new ExpectedQueryPartitionProviderException(
                        serializedQueryExecutionInfo,
                        exception);
                }

                return TryCatch<PartitionedQueryExecutionInfoInternal>.FromException(
                    queryPartitionProviderException);
            }

            PartitionedQueryExecutionInfoInternal queryInfoInternal =
               JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                   serializedQueryExecutionInfo,
                   new JsonSerializerSettings
                   {
                       DateParseHandling = DateParseHandling.None,
                       MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
                   });

            if (!this.ValidateQueryExecutionInfo(queryInfoInternal, out ArgumentException innerException))
            {
                return TryCatch<PartitionedQueryExecutionInfoInternal>.FromException(
                    new ExpectedQueryPartitionProviderException(
                        serializedQueryExecutionInfo,
                        innerException));
            }

            return TryCatch<PartitionedQueryExecutionInfoInternal>.FromResult(queryInfoInternal);
        }

        private bool ValidateQueryExecutionInfo(PartitionedQueryExecutionInfoInternal queryExecutionInfo, out ArgumentException innerException)
        {
            if (queryExecutionInfo.QueryInfo?.Limit.HasValue == true &&
                queryExecutionInfo.QueryInfo.Limit.Value > int.MaxValue)
            {
                innerException = new ArgumentOutOfRangeException("QueryInfo.Limit");
                return false;
            }

            if (queryExecutionInfo.QueryInfo?.Offset.HasValue == true &&
                queryExecutionInfo.QueryInfo.Offset.Value > int.MaxValue)
            {
                innerException = new ArgumentOutOfRangeException("QueryInfo.Offset");
                return false;
            }

            if (queryExecutionInfo.QueryInfo?.Top.HasValue == true &&
                queryExecutionInfo.QueryInfo.Top.Value > int.MaxValue)
            {
                innerException = new ArgumentOutOfRangeException("QueryInfo.Top");
                return false;
            }

            if ((queryExecutionInfo.HybridSearchQueryInfo?.Skip ?? 0) > int.MaxValue)
            {
                innerException = new ArgumentOutOfRangeException("HybridSearchQueryInfo.Skip");
                return false;
            }

            if ((queryExecutionInfo.HybridSearchQueryInfo?.Take ?? 0) > int.MaxValue)
            {
                innerException = new ArgumentOutOfRangeException("HybridSearchQueryInfo.Take");
                return false;
            }

            innerException = null;
            return true;
        }

        internal static TryCatch<IntPtr> TryCreateServiceProvider(string queryEngineConfiguration)
        {
            try
            {
                IntPtr serviceProvider = IntPtr.Zero;
                uint errorCode = ServiceInteropWrapper.CreateServiceProvider(
                                queryEngineConfiguration,
                                out serviceProvider);
                Exception exception = Marshal.GetExceptionForHR((int)errorCode);
                if (exception != null)
                {
                    DefaultTrace.TraceWarning("QueryPartitionProvider.TryCreateServiceProvider failed with exception {0}", exception);
                    return TryCatch<IntPtr>.FromException(exception);
                }
                
                return TryCatch<IntPtr>.FromResult(serviceProvider);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("QueryPartitionProvider.TryCreateServiceProvider failed with exception {0}", ex);
                return TryCatch<IntPtr>.FromException(ex);
            }
        }

        ~QueryPartitionProvider()
        {
            this.Dispose(false);
        }

        private void Initialize()
        {
            if (!this.disposed)
            {
                if (this.serviceProvider == IntPtr.Zero)
                {
                    lock (this.serviceProviderStateLock)
                    {
                        if (!this.disposed && this.serviceProvider == IntPtr.Zero)
                        {
                            TryCatch<IntPtr> tryCreateServiceProvider = QueryPartitionProvider.TryCreateServiceProvider(this.queryengineConfiguration);
                            if (tryCreateServiceProvider.Failed)
                            {
                                throw ExceptionWithStackTraceException.UnWrapMonadExcepion(tryCreateServiceProvider.Exception, NoOpTrace.Singleton);
                            }

                            this.serviceProvider = tryCreateServiceProvider.Result;
                        }
                    }
                }
            }
            else
            {
                throw new ObjectDisposedException(typeof(QueryPartitionProvider).Name);
            }
        }

        private void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                // Free managed objects
            }

            lock (this.serviceProviderStateLock)
            {
                if (this.serviceProvider != IntPtr.Zero)
                {
                    Marshal.Release(this.serviceProvider);
                    this.serviceProvider = IntPtr.Zero;
                }

                this.disposed = true;
            }
        }
    }
}
