//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
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

        public PartitionedQueryExecutionInfo GetPartitionedQueryExecutionInfo(
            Func<string, Exception> createBadRequestException,
            SqlQuerySpec querySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey)
        {
            PartitionedQueryExecutionInfoInternal queryInfoInternal = this.GetPartitionedQueryExecutionInfoInternal(
                createBadRequestException,
                querySpec,
                partitionKeyDefinition,
                requireFormattableOrderByQuery,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                hasLogicalPartitionKey);

            return this.ConvertPartitionedQueryExecutionInfo(queryInfoInternal, partitionKeyDefinition);
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

        internal PartitionedQueryExecutionInfoInternal GetPartitionedQueryExecutionInfoInternal(
            Func<string, Exception> createBadRequestException,
            SqlQuerySpec querySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey)
        {
            if (querySpec == null || partitionKeyDefinition == null)
            {
                return DefaultInfoInternal;
            }

            string queryText = JsonConvert.SerializeObject(querySpec);

            List<string> paths = new List<string>(partitionKeyDefinition.Paths);

            List<string[]> pathParts = new List<string[]>();
            paths.ForEach(path =>
                {
                    pathParts.Add(PathParser.GetPathParts(path).ToArray());
                });

            string[] allParts = pathParts.SelectMany(parts => parts).ToArray();
            uint[] partsLengths = pathParts.Select(parts => (uint)parts.Length).ToArray();

            PartitionKind partitionKind = partitionKeyDefinition.Kind;

            this.Initialize();

            byte[] buffer = new byte[InitialBufferSize];
            uint errorCode;
            uint serializedQueryExecutionInfoResultLength;

            unsafe
            {
                fixed (byte* bytePtr = buffer)
                {
                    errorCode = ServiceInteropWrapper.GetPartitionKeyRangesFromQuery(
                        this.serviceProvider,
                        queryText,
                        requireFormattableOrderByQuery,
                        isContinuationExpected,
                        allowNonValueAggregateQuery,
                        hasLogicalPartitionKey,
                        allParts,
                        partsLengths,
                        (uint)partitionKeyDefinition.Paths.Count,
                        partitionKind,
                        new IntPtr(bytePtr),
                        (uint)buffer.Length,
                        out serializedQueryExecutionInfoResultLength);

                    if (errorCode == DISP_E_BUFFERTOOSMALL)
                    {
                        buffer = new byte[serializedQueryExecutionInfoResultLength];
                        fixed (byte* bytePtr2 = buffer)
                        {
                            errorCode = ServiceInteropWrapper.GetPartitionKeyRangesFromQuery(
                                this.serviceProvider,
                                queryText,
                                requireFormattableOrderByQuery,
                                isContinuationExpected,
                                allowNonValueAggregateQuery,
                                hasLogicalPartitionKey, // has logical partition key
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

            string serializedQueryExecutionInfo = Encoding.UTF8.GetString(buffer, 0, (int)serializedQueryExecutionInfoResultLength);

            Exception exception = Marshal.GetExceptionForHR((int)errorCode);
            if (exception != null)
            {
                DefaultTrace.TraceInformation("QueryEngineConfiguration: " + this.queryengineConfiguration);
                string errorMessage;
                if (string.IsNullOrEmpty(serializedQueryExecutionInfo))
                {
                    errorMessage = $"Message: Query service interop parsing hit an unexpected exception: {exception.ToString()}";
                }
                else
                {
                    errorMessage = "Message: " + serializedQueryExecutionInfo;
                }

                throw createBadRequestException(errorMessage);
            }

            PartitionedQueryExecutionInfoInternal queryInfoInternal =
               JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                   serializedQueryExecutionInfo,
                   new JsonSerializerSettings
                   {
                       DateParseHandling = DateParseHandling.None
                   });

            return queryInfoInternal;
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
