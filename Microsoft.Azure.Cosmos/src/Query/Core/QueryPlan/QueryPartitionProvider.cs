//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
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
                    this.queryengineConfiguration = JsonConvert.SerializeObject(queryengineConfiguration);

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
            else
            {
                throw new ObjectDisposedException(typeof(QueryPartitionProvider).Name);
            }
        }

        public TryCatch<PartitionedQueryExecutionInfo> TryGetPartitionedQueryExecutionInfo(
            SqlQuerySpec querySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> tryGetInternalQueryInfo = this.TryGetPartitionedQueryExecutionInfoInternal(
                querySpec: querySpec,
                partitionKeyDefinition: partitionKeyDefinition,
                requireFormattableOrderByQuery: requireFormattableOrderByQuery,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                hasLogicalPartitionKey: hasLogicalPartitionKey,
                allowDCount: allowDCount);
            if (!tryGetInternalQueryInfo.Succeeded)
            {
                return TryCatch<PartitionedQueryExecutionInfo>.FromException(tryGetInternalQueryInfo.Exception);
            }

            PartitionedQueryExecutionInfo queryInfo = this.ConvertPartitionedQueryExecutionInfo(tryGetInternalQueryInfo.Result, partitionKeyDefinition);
            return TryCatch<PartitionedQueryExecutionInfo>.FromResult(queryInfo);
        }

        internal PartitionedQueryExecutionInfo ConvertPartitionedQueryExecutionInfo(
            PartitionedQueryExecutionInfoInternal queryInfoInternal,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            List<Documents.Routing.Range<string>> effectiveRanges = new List<Documents.Routing.Range<string>>(queryInfoInternal.QueryRanges.Count);
            foreach (Documents.Routing.Range<PartitionKeyInternal> internalRange in queryInfoInternal.QueryRanges)
            {
                effectiveRanges.Add(new Documents.Routing.Range<string>(
                     internalRange.Min.GetEffectivePartitionKeyString(partitionKeyDefinition, false),
                     internalRange.Max.GetEffectivePartitionKeyString(partitionKeyDefinition, false),
                     internalRange.IsMinInclusive,
                     internalRange.IsMaxInclusive));
            }

            effectiveRanges.Sort(Documents.Routing.Range<string>.MinComparer.Instance);

            return new PartitionedQueryExecutionInfo()
            {
                QueryInfo = queryInfoInternal.QueryInfo,
                QueryRanges = effectiveRanges,
            };
        }

        internal TryCatch<PartitionedQueryExecutionInfoInternal> TryGetPartitionedQueryExecutionInfoInternal(
            SqlQuerySpec querySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount)
        {
            if (querySpec == null || partitionKeyDefinition == null)
            {
                return TryCatch<PartitionedQueryExecutionInfoInternal>.FromResult(DefaultInfoInternal);
            }

            string queryText = JsonConvert.SerializeObject(querySpec);

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

            unsafe
            {
                fixed (byte* bytePtr = buffer)
                {
                    errorCode = ServiceInteropWrapper.GetPartitionKeyRangesFromQuery2(
                        this.serviceProvider,
                        queryText,
                        requireFormattableOrderByQuery,
                        isContinuationExpected,
                        allowNonValueAggregateQuery,
                        hasLogicalPartitionKey,
                        allowDCount,
                        allParts,
                        partsLengths,
                        (uint)partitionKeyDefinition.Paths.Count,
                        partitionKind,
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
                            errorCode = ServiceInteropWrapper.GetPartitionKeyRangesFromQuery2(
                                this.serviceProvider,
                                queryText,
                                requireFormattableOrderByQuery,
                                isContinuationExpected,
                                allowNonValueAggregateQuery,
                                hasLogicalPartitionKey, // has logical partition key
                                allowDCount,
                                allParts,
                                partsLengths,
                                (uint)partitionKeyDefinition.Paths.Count,
                                partitionKind,
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
                       DateParseHandling = DateParseHandling.None
                   });

            return TryCatch<PartitionedQueryExecutionInfoInternal>.FromResult(queryInfoInternal);
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
                            uint errorCode = ServiceInteropWrapper.CreateServiceProvider(
                                this.queryengineConfiguration,
                                out this.serviceProvider);

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
