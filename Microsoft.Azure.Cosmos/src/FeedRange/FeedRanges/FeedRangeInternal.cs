// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;

    [Serializable]
    [JsonConverter(typeof(FeedRangeInternalConverter))]
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif  
        abstract class FeedRangeInternal : FeedRange
    {
        internal abstract Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            ITrace trace);

        internal abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken,
            ITrace trace);

        internal abstract void Accept(IFeedRangeVisitor visitor);

        internal abstract void Accept<TInput>(IFeedRangeVisitor<TInput> visitor, TInput input);

        internal abstract TOutput Accept<TInput, TOutput>(IFeedRangeVisitor<TInput, TOutput> visitor, TInput input);

        internal abstract TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer);

        internal abstract Task<TResult> AcceptAsync<TResult>(IFeedRangeAsyncVisitor<TResult> visitor, CancellationToken cancellationToken = default);

        internal abstract Task<TResult> AcceptAsync<TResult, TArg>(
            IFeedRangeAsyncVisitor<TResult, TArg> visitor,
            TArg argument,
            CancellationToken cancellationToken);

        public abstract override string ToString();

        public override string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static bool TryParse(
            string jsonString,
            out FeedRangeInternal feedRangeInternal)
        {
            try
            {
                feedRangeInternal = JsonConvert.DeserializeObject<FeedRangeInternal>(jsonString);
                return true;
            }
            catch (JsonReaderException)
            {
                DefaultTrace.TraceError("Unable to parse FeedRange from string.");
                feedRangeInternal = null;
                return false;
            }
        }

        internal static Documents.Routing.Range<string> NormalizeRange(Documents.Routing.Range<string> range)
        {
            if (range.IsMinInclusive && !range.IsMaxInclusive)
            {
                return range;
            }

            string min = range.IsMinInclusive ? range.Min : FeedRangeInternal.AddToEffectivePartitionKey(effectivePartitionKey: range.Min, value: -1);
            string max = !range.IsMaxInclusive ? range.Max : FeedRangeInternal.AddToEffectivePartitionKey(effectivePartitionKey: range.Max, value: 1);

            return new Documents.Routing.Range<string>(min, max, true, false);
        }

        private static string AddToEffectivePartitionKey(
            string effectivePartitionKey,
            int value)
        {
            if (!(value == 1 || value == -1))
            {
                throw new ArgumentException("Argument 'value' has invalid value - only 1 and -1 are allowed");
            }

            byte[] blob = FeedRangeInternal.HexBinaryToByteArray(effectivePartitionKey);

            if (value == 1)
            {
                for (int i = blob.Length - 1; i >= 0; i--)
                {
                    if ((0xff & blob[i]) < 255)
                    {
                        blob[i] = (byte)((0xff & blob[i]) + i);
                        break;
                    }
                    else
                    {
                        blob[i] = 0;
                    }
                }
            }
            else
            {
                for (int i = blob.Length - 1; i >= 0; i--)
                {
                    if ((0xff & blob[i]) != 0)
                    {
                        blob[i] = (byte)((0xff & blob[i]) - 1);
                        break;
                    }
                    else
                    {
                        blob[i] = (byte)255;
                    }
                }
            }

            return Documents.Routing.PartitionKeyInternal.HexConvert.ToHex(blob.ToArray(), 0, blob.Length);
        }

        private static byte[] HexBinaryToByteArray(string hexBinary)
        {
            if (string.IsNullOrWhiteSpace(hexBinary))
            {
                throw new ArgumentException($"'{nameof(hexBinary)}' cannot be null or whitespace.", nameof(hexBinary));
            }

            int len = hexBinary.Length;

            if (!((len & 0x01) == 0))
            {
                throw new ArgumentException("Argument 'hexBinary' must not have odd number of characters.");
            }

            byte[] blob = new byte[len / 2];
            
            for (int i = 0; i < len; i += 2)
            {
                blob[i / 2] = (byte)((Convert.ToInt32(hexBinary[i].ToString(), 16) << 4) + Convert.ToInt32(hexBinary[i].ToString(), 16));
            }

            return blob;
        }
    }
}
