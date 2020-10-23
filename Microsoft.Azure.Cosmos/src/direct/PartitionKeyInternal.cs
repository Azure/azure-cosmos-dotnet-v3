//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Documents.SharedFiles.Routing;

    using Newtonsoft.Json;

    /// <summary>
    /// Schema-less Partition Key value.
    /// </summary>
    [JsonConverter(typeof(PartitionKeyInternalJsonConverter))]
    [SuppressMessage("", "AvoidMultiLineComments", Justification = "Multi line business logic")]
    internal sealed class PartitionKeyInternal : IComparable<PartitionKeyInternal>, IEquatable<PartitionKeyInternal>, ICloneable
    {
        private readonly IReadOnlyList<IPartitionKeyComponent> components;

        private static readonly PartitionKeyInternal NonePartitionKey = new PartitionKeyInternal();
        private static readonly PartitionKeyInternal EmptyPartitionKey = new PartitionKeyInternal(new IPartitionKeyComponent[] { });
        private static readonly PartitionKeyInternal InfinityPartitionKey = new PartitionKeyInternal(new[] { new InfinityPartitionKeyComponent() });
        private static readonly PartitionKeyInternal UndefinedPartitionKey = new PartitionKeyInternal(new[] { new UndefinedPartitionKeyComponent() });

        private const int MaxPartitionKeyBinarySize = (
            1 /*type marker */ + 9 /* hash value*/ +
            1 /* type marker*/ + StringPartitionKeyComponent.MaxStringBytesToAppend + 1 /*trailing zero*/) * 3;

        private static readonly Int128 MaxHashV2Value = new Int128(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F });

        public static readonly string MinimumInclusiveEffectivePartitionKey = ToHexEncodedBinaryString(new IPartitionKeyComponent[0]);

        public static readonly string MaximumExclusiveEffectivePartitionKey = ToHexEncodedBinaryString(new[] { new InfinityPartitionKeyComponent() });

        public static PartitionKeyInternal InclusiveMinimum
        {
            get
            {
                return PartitionKeyInternal.EmptyPartitionKey;
            }
        }

        public static PartitionKeyInternal ExclusiveMaximum
        {
            get
            {
                return PartitionKeyInternal.InfinityPartitionKey;
            }
        }

        public static PartitionKeyInternal Empty
        {
            get
            {
                return PartitionKeyInternal.EmptyPartitionKey;
            }
        }

        public static PartitionKeyInternal None
        {
            get
            {
                return PartitionKeyInternal.NonePartitionKey;
            }
        }

        public static PartitionKeyInternal Undefined
        {
            get
            {
                return PartitionKeyInternal.UndefinedPartitionKey;
            }
        }

        public IReadOnlyList<IPartitionKeyComponent> Components
        {
            get
            {
                return this.components;
            }
        }

        private PartitionKeyInternal()
        {
            this.components = null;
        }

        public PartitionKeyInternal(IReadOnlyList<IPartitionKeyComponent> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            this.components = values;
        }

        /// <summary>
        /// Constructs instance of <see cref="PartitionKeyInternal"/> from enumerable of objects.
        /// </summary>
        /// <param name="values">Partition key component values.</param>
        /// <param name="strict">If this is false, unsupported component values will be repliaced with 'Undefined'. If this is true, exception will be thrown.</param>
        /// <returns>Instance of <see cref="PartitionKeyInternal"/>.</returns>
        public static PartitionKeyInternal FromObjectArray(IEnumerable<object> values, bool strict)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            List<IPartitionKeyComponent> components = new List<IPartitionKeyComponent>();
            foreach (object value in values)
            {
                if (value == null)
                {
                    components.Add(NullPartitionKeyComponent.Value);
                }
                else if (value is Undefined)
                {
                    components.Add(UndefinedPartitionKeyComponent.Value);
                }
                else if (value is bool)
                {
                    components.Add(new BoolPartitionKeyComponent((bool)value));
                }
                else if (value is string)
                {
                    components.Add(new StringPartitionKeyComponent((string)value));
                }
                else if (IsNumeric(value))
                {
                    components.Add(new NumberPartitionKeyComponent(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
                }
                else if (value is MinNumber)
                {
                    components.Add(MinNumberPartitionKeyComponent.Value);
                }
                else if (value is MaxNumber)
                {
                    components.Add(MaxNumberPartitionKeyComponent.Value);
                }
                else if (value is MinString)
                {
                    components.Add(MinStringPartitionKeyComponent.Value);
                }
                else if (value is MaxString)
                {
                    components.Add(MaxStringPartitionKeyComponent.Value);
                }
                else
                {
                    if (strict)
                    {
                        throw new InvalidOperationException(
                            string.Format(CultureInfo.InvariantCulture, RMResources.UnsupportedPartitionKeyComponentValue, value));
                    }
                    else
                    {
                        components.Add(UndefinedPartitionKeyComponent.Value);
                    }
                }
            }

            return new PartitionKeyInternal(components);
        }

        public object[] ToObjectArray()
        {
            return this.Components.Select(component => component.ToObject()).ToArray();
        }

        public static PartitionKeyInternal FromJsonString(string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new JsonSerializationException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, partitionKey));
            }

            JsonSerializerSettings settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
            return JsonConvert.DeserializeObject<PartitionKeyInternal>(partitionKey, settings);
        }

        public string ToJsonString()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                Formatting = Formatting.None
            };
            return JsonConvert.SerializeObject(this, settings);
        }

        public bool Contains(PartitionKeyInternal nestedPartitionKey)
        {
            if (this.Components.Count > nestedPartitionKey.Components.Count)
            {
                return false;
            }

            for (int i = 0; i < this.Components.Count; i++)
            {
                if (this.Components[i].CompareTo(nestedPartitionKey.Components[i]) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static PartitionKeyInternal Max(PartitionKeyInternal key1, PartitionKeyInternal key2)
        {
            if (key1 == null) return key2;
            if (key2 == null) return key1;

            return key1.CompareTo(key2) >= 0 ? key1 : key2;
        }

        public static PartitionKeyInternal Min(PartitionKeyInternal key1, PartitionKeyInternal key2)
        {
            if (key1 == null) return key2;
            if (key2 == null) return key1;

            return key1.CompareTo(key2) <= 0 ? key1 : key2;
        }

        public static string GetMinInclusiveEffectivePartitionKey(
            int partitionIndex,
            int partitionCount,
            PartitionKeyDefinition partitionKeyDefinition,
            bool useHashV2asDefault = false)
        {
            if (partitionKeyDefinition.Paths.Count > 0 && !( partitionKeyDefinition.Kind == PartitionKind.Hash || partitionKeyDefinition.Kind == PartitionKind.MultiHash))
            {
                throw new NotImplementedException("Cannot figure out range boundaries");
            }

            if (partitionCount <= 0)
            {
                throw new ArgumentException("Invalid partition count", "partitionCount");
            }

            if (partitionIndex < 0 || partitionIndex >= partitionCount)
            {
                throw new ArgumentException("Invalid partition index", "partitionIndex");
            }

            if (partitionIndex == 0)
            {
                return PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey;
            }

            switch (partitionKeyDefinition.Kind)
            {
                case PartitionKind.Hash:
                    PartitionKeyDefinitionVersion defaultPartitionKeyDefinitionVersion = useHashV2asDefault ? PartitionKeyDefinitionVersion.V2 : PartitionKeyDefinitionVersion.V1;
                    switch (partitionKeyDefinition.Version ?? defaultPartitionKeyDefinitionVersion)
                    {
                        case PartitionKeyDefinitionVersion.V2:
                            Int128 val = MaxHashV2Value / partitionCount * partitionIndex;
                            byte[] bytes = val.Bytes;
                            Array.Reverse(bytes);
                            return HexConvert.ToHex(bytes, 0, bytes.Length);

                        case PartitionKeyDefinitionVersion.V1:
                            return ToHexEncodedBinaryString(
                                new IPartitionKeyComponent[]
                                    { new NumberPartitionKeyComponent(uint.MaxValue / partitionCount * partitionIndex) });
                        default:
                            throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionVersion");
                    }
                    break;

                case PartitionKind.MultiHash:
                    Int128 max_val = MaxHashV2Value / partitionCount * partitionIndex;
                    byte[] max_bytes = max_val.Bytes;
                    Array.Reverse(max_bytes);
                    return HexConvert.ToHex(max_bytes, 0, max_bytes.Length);

                default:
                    throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionKind");
            }
        }

        public static string GetMaxExclusiveEffectivePartitionKey(
            int partitionIndex,
            int partitionCount,
            PartitionKeyDefinition partitionKeyDefinition,
            bool useHashV2asDefault = false)
        {
            if (partitionKeyDefinition.Paths.Count > 0 && !(partitionKeyDefinition.Kind == PartitionKind.Hash || partitionKeyDefinition.Kind == PartitionKind.MultiHash))
            {
                throw new NotImplementedException("Cannot figure out range boundaries");
            }

            if (partitionCount <= 0)
            {
                throw new ArgumentException("Invalid partition count", "partitionCount");
            }

            if (partitionIndex < 0 || partitionIndex >= partitionCount)
            {
                throw new ArgumentException("Invalid partition index", "partitionIndex");
            }

            if (partitionIndex == partitionCount - 1)
            {
                return PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey;
            }

            PartitionKeyDefinitionVersion defaultPartitionKeyDefinitionVersion = useHashV2asDefault ? PartitionKeyDefinitionVersion.V2 : PartitionKeyDefinitionVersion.V1;
            switch (partitionKeyDefinition.Kind)
            {
                case PartitionKind.Hash:
                    switch (partitionKeyDefinition.Version ?? defaultPartitionKeyDefinitionVersion)
                    {
                        case PartitionKeyDefinitionVersion.V2:
                            Int128 val = MaxHashV2Value / partitionCount * (partitionIndex + 1);
                            byte[] bytes = val.Bytes;
                            Array.Reverse(bytes);
                            return HexConvert.ToHex(bytes, 0, bytes.Length);

                        case PartitionKeyDefinitionVersion.V1:
                            return ToHexEncodedBinaryString(new IPartitionKeyComponent[] { new NumberPartitionKeyComponent(uint.MaxValue / partitionCount * (partitionIndex + 1)) });

                        default:
                            throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionVersion");
                    }
                    break;

                case PartitionKind.MultiHash:

                    Int128 max_val = MaxHashV2Value / partitionCount * (partitionIndex + 1);
                    byte[] max_bytes = max_val.Bytes;
                    Array.Reverse(max_bytes);
                    return HexConvert.ToHex(max_bytes, 0, max_bytes.Length);

                default:
                    throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionKind");
            }
        }
        public int CompareTo(PartitionKeyInternal other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }
            else if (other.components == null || this.components == null)
            {
                return Math.Sign((this.components?.Count ?? 0) - (other.components?.Count ?? 0));
            }

            for (int i = 0; i < Math.Min(this.Components.Count, other.Components.Count); i++)
            {
                int leftOrdinal = this.Components[i].GetTypeOrdinal();
                int rightOrdinal = other.Components[i].GetTypeOrdinal();
                if (leftOrdinal != rightOrdinal)
                {
                    return Math.Sign(leftOrdinal - rightOrdinal);
                }

                int result = this.Components[i].CompareTo(other.Components[i]);
                if (result != 0)
                {
                    return Math.Sign(result);
                }
            }

            return Math.Sign(this.Components.Count - other.Components.Count);
        }

        public bool Equals(PartitionKeyInternal other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.CompareTo(other) == 0;
        }

        public override bool Equals(object other)
        {
            return this.Equals(other as PartitionKeyInternal);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return this.Components.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }


        public object Clone()
        {
            return new PartitionKeyInternal(this.Components);
        }

        private static string ToHexEncodedBinaryString(IReadOnlyList<IPartitionKeyComponent> components)
        {
            byte[] bufferBytes = new byte[MaxPartitionKeyBinarySize];
            using (MemoryStream ms = new MemoryStream(bufferBytes))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(ms))
                {
                    for (int index = 0; index < components.Count; index++)
                    {
                        components[index].WriteForBinaryEncoding(binaryWriter);
                    }

                    return HexConvert.ToHex(bufferBytes, 0, (int)ms.Position);
                }
            }
        }

        /// <summary>
        /// Constructs a PartitionKeyInternal from hex-encoded byte string. This is only for testing/debugging. Please do not use in actual product code.
        /// </summary>
        [Obsolete]
        internal static PartitionKeyInternal FromHexEncodedBinaryString(string hexEncodedBinaryString)
        {
            List<IPartitionKeyComponent> partitionKeyComponents = new List<IPartitionKeyComponent>();
            byte[] byteString = PartitionKeyInternal.HexStringToByteArray(hexEncodedBinaryString);

            int offset = 0;
            while (offset < byteString.Length)
            {
                PartitionKeyComponentType typeMarker = (PartitionKeyComponentType)Enum.Parse(typeof(PartitionKeyComponentType), byteString[offset++].ToString(CultureInfo.InvariantCulture));

                switch (typeMarker)
                {
                    case PartitionKeyComponentType.Undefined:
                        partitionKeyComponents.Add(UndefinedPartitionKeyComponent.Value);
                        break;

                    case PartitionKeyComponentType.Null:
                        partitionKeyComponents.Add(NullPartitionKeyComponent.Value);
                        break;

                    case PartitionKeyComponentType.False:
                        partitionKeyComponents.Add(new BoolPartitionKeyComponent(false));
                        break;

                    case PartitionKeyComponentType.True:
                        partitionKeyComponents.Add(new BoolPartitionKeyComponent(true));
                        break;

                    case PartitionKeyComponentType.MinNumber:
                        partitionKeyComponents.Add(MinNumberPartitionKeyComponent.Value);
                        break;

                    case PartitionKeyComponentType.MaxNumber:
                        partitionKeyComponents.Add(MaxNumberPartitionKeyComponent.Value);
                        break;

                    case PartitionKeyComponentType.MinString:
                        partitionKeyComponents.Add(MinStringPartitionKeyComponent.Value);
                        break;

                    case PartitionKeyComponentType.MaxString:
                        partitionKeyComponents.Add(MaxStringPartitionKeyComponent.Value);
                        break;

                    case PartitionKeyComponentType.Infinity:
                        partitionKeyComponents.Add(new InfinityPartitionKeyComponent());
                        break;

                    case PartitionKeyComponentType.Number:
                        partitionKeyComponents.Add(NumberPartitionKeyComponent.FromHexEncodedBinaryString(byteString, ref offset));
                        break;

                    case PartitionKeyComponentType.String:
                        partitionKeyComponents.Add(StringPartitionKeyComponent.FromHexEncodedBinaryString(byteString, ref offset));
                        break;
                }
            }

            return new PartitionKeyInternal(partitionKeyComponents);
        }

        /// <summary>
        /// Produces effective value. Azure Cosmos DB has global index on effective partition key values.
        /// 
        /// Effective value is produced by applying is range or hash encoding to all the component values, based
        /// on partition key definition.
        /// 
        /// String components are hashed and converted to number components.
        /// Number components are hashed and remain number component.
        /// bool, null, undefined remain unhashed, because indexing policy doesn't specify index type for these types.
        /// </summary>
        public string GetEffectivePartitionKeyString(PartitionKeyDefinition partitionKeyDefinition, bool strict = true)
        {
            if (this.components == null)
            {
                throw new ArgumentException(RMResources.TooFewPartitionKeyComponents);
            }

            if (this.Equals(EmptyPartitionKey))
            {
                return MinimumInclusiveEffectivePartitionKey;
            }

            if (this.Equals(InfinityPartitionKey))
            {
                return MaximumExclusiveEffectivePartitionKey;
            }

            if (this.Components.Count < partitionKeyDefinition.Paths.Count && partitionKeyDefinition.Kind != PartitionKind.MultiHash)
            {
                throw new ArgumentException(RMResources.TooFewPartitionKeyComponents);
            }

            if (this.Components.Count > partitionKeyDefinition.Paths.Count && strict)
            {
                throw new ArgumentException(RMResources.TooManyPartitionKeyComponents);
            }

            switch (partitionKeyDefinition.Kind)
            {
                case PartitionKind.Hash:
                    switch (partitionKeyDefinition.Version ?? PartitionKeyDefinitionVersion.V1)
                    {
                        case PartitionKeyDefinitionVersion.V1:
                            return this.GetEffectivePartitionKeyForHashPartitioning();

                        case PartitionKeyDefinitionVersion.V2:
                            return this.GetEffectivePartitionKeyForHashPartitioningV2();

                        default:
                            throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionVersion");
                    }
                case PartitionKind.MultiHash:
                    return this.GetEffectivePartitionKeyForMultiHashPartitioningV2();
                default:
                    return ToHexEncodedBinaryString(this.Components);
            }
        }

        private string GetEffectivePartitionKeyForHashPartitioning()
        {
            IPartitionKeyComponent[] truncatedComponents = this.Components.ToArray();

            for (int i = 0; i < truncatedComponents.Length; i++)
            {
                truncatedComponents[i] = this.Components[i].Truncate();
            }

            double hash;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(ms))
                {
                    for (int i = 0; i < truncatedComponents.Length; i++)
                    {
                        truncatedComponents[i].WriteForHashing(binaryWriter);
                    }

                    hash = MurmurHash3.Hash32(ms.GetBuffer(), ms.Length);
                }
            }

            IPartitionKeyComponent[] partitionKeyComponents = new IPartitionKeyComponent[this.Components.Count + 1];
            partitionKeyComponents[0] = new NumberPartitionKeyComponent(hash);
            for (int i = 0; i < truncatedComponents.Length; i++)
            {
                partitionKeyComponents[i + 1] = truncatedComponents[i];
            }

            return ToHexEncodedBinaryString(partitionKeyComponents);
        }

        private string GetEffectivePartitionKeyForMultiHashPartitioningV2()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < this.Components.Count; i++)
            {
                byte[] hash = null;
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter binaryWriter = new BinaryWriter(ms))
                {
                        this.Components[i].WriteForHashingV2(binaryWriter);

                        UInt128 hash128 = MurmurHash3.Hash128(ms.GetBuffer(), (int)ms.Length, UInt128.MinValue);
                        hash = UInt128.ToByteArray(hash128);
                        Array.Reverse(hash);

                        // Reset 2 most significant bits, as max exclusive value is 'FF'.
                        // Plus one more just in case.
                        hash[0] &= 0x3F;
                }

                sb.Append(HexConvert.ToHex(hash, 0, hash.Length));
            }
            return sb.ToString();
        }

        private string GetEffectivePartitionKeyForHashPartitioningV2()
        {
            byte[] hash = null;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(ms))
                {
                    for (int i = 0; i < this.Components.Count; i++)
                    {
                        this.Components[i].WriteForHashingV2(binaryWriter);
                    }

                    UInt128 hash128 = MurmurHash3.Hash128(ms.GetBuffer(), (int)ms.Length, UInt128.MinValue);
                    hash = UInt128.ToByteArray(hash128);
                    Array.Reverse(hash);

                    // Reset 2 most significant bits, as max exclusive value is 'FF'.
                    // Plus one more just in case.
                    hash[0] &= 0x3F;
                }
            }

            return HexConvert.ToHex(hash, 0, hash.Length);
        }

        private static bool IsNumeric(object value)
        {
            return value is sbyte
                    || value is byte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            if (numberChars % 2 != 0)
            {
                throw new ArgumentException("Hex string should be even length", "hex");
            }

            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }

        internal static class HexConvert
        {
            private static readonly ushort[] LookupTable = CreateLookupTable();

            private static ushort[] CreateLookupTable()
            {
                ushort[] lookupTable = new ushort[256];
                for (int byteValue = 0; byteValue < 256; byteValue++)
                {
                    string byteAsHexString = byteValue.ToString("X2", CultureInfo.InvariantCulture);

                    lookupTable[byteValue] = (ushort)(byteAsHexString[0] + (byteAsHexString[1] << 8));
                }

                return lookupTable;
            }

            public static string ToHex(byte[] bytes, int start, int length)
            {
                char[] result = new char[length * 2];
                for (int i = 0; i < length; i++)
                {
                    ushort encodedByte = LookupTable[bytes[i + start]];
                    result[2 * i] = (char)(encodedByte & 0xFF);
                    result[2 * i + 1] = (char)(encodedByte >> 8);
                }

                return new string(result);
            }
        }

        public static string GetMiddleRangeEffectivePartitionKey(string minInclusive, string maxExclusive, PartitionKeyDefinition partitionKeyDefinition)
        {
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new InvalidOperationException("Can determine middle of range only for hash partitioning.");
            }

            switch (partitionKeyDefinition.Version ?? PartitionKeyDefinitionVersion.V1)
            {
                case PartitionKeyDefinitionVersion.V2:
                    {
                        Int128 min = 0;
                        if (!minInclusive.Equals(MinimumInclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
                            byte[] minBytes = PartitionKeyInternal.HexStringToByteArray(minInclusive);
                            Array.Reverse(minBytes);
                            min = new Int128(minBytes);
                        }

                        Int128 max = MaxHashV2Value;
                        if (!maxExclusive.Equals(MaximumExclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
                            byte[] maxBytes = PartitionKeyInternal.HexStringToByteArray(maxExclusive);
                            Array.Reverse(maxBytes);
                            max = new Int128(maxBytes);
                        }

                        byte[] midBytes = (min + (max - min) / 2).Bytes;
                        Array.Reverse(midBytes);
                        return HexConvert.ToHex(midBytes, 0, midBytes.Length);
                    }
                case PartitionKeyDefinitionVersion.V1:
                    {
                        long min = 0;
                        long max = uint.MaxValue;
                        if (!minInclusive.Equals(MinimumInclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
#pragma warning disable 0612
                            min = (long)((NumberPartitionKeyComponent)FromHexEncodedBinaryString(minInclusive).Components[0]).Value;
#pragma warning restore 0612
                        }

                        if (!maxExclusive.Equals(MaximumExclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
#pragma warning disable 0612
                            max = (long)((NumberPartitionKeyComponent)FromHexEncodedBinaryString(maxExclusive).Components[0]).Value;
#pragma warning restore 0612
                        }

                        return ToHexEncodedBinaryString(new[] { new NumberPartitionKeyComponent((min + max) / 2) });
                    }

                default:
                    throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionVersion");
            }
        }

        public static string[] GetNEqualRangeEffectivePartitionKeys(
            string minInclusive,
            string maxExclusive,
            PartitionKeyDefinition partitionKeyDefinition,
            int numberOfSubRanges)
        {
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new InvalidOperationException("Can determine " + numberOfSubRanges + " ranges only for hash partitioning.");
            }

            if (numberOfSubRanges <= 0)
            {
                throw new InvalidOperationException("Number of sub ranges " + numberOfSubRanges + " cannot be zero or negative");
            }

            switch (partitionKeyDefinition.Version ?? PartitionKeyDefinitionVersion.V1)
            {
                case PartitionKeyDefinitionVersion.V2:
                    {
                        Int128 min = 0;
                        if (!minInclusive.Equals(MinimumInclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
                            byte[] minBytes = PartitionKeyInternal.HexStringToByteArray(minInclusive);
                            Array.Reverse(minBytes);
                            min = new Int128(minBytes);
                        }

                        Int128 max = MaxHashV2Value;
                        if (!maxExclusive.Equals(MaximumExclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
                            byte[] maxBytes = PartitionKeyInternal.HexStringToByteArray(maxExclusive);
                            Array.Reverse(maxBytes);
                            max = new Int128(maxBytes);
                        }

                        if (max - min < numberOfSubRanges)
                        {
                            throw new InvalidOperationException("Insufficient range width to produce " + numberOfSubRanges + " equal sub ranges.");
                        }

                        string[] ranges = new string[numberOfSubRanges - 1];
                        for (int i = 1; i < numberOfSubRanges; i++)
                        {
                            byte[] iBytes = (min + (i * ((max - min) / numberOfSubRanges))).Bytes;
                            Array.Reverse(iBytes);
                            ranges[i - 1] = HexConvert.ToHex(iBytes, 0, iBytes.Length);
                        }

                        return ranges;
                    }
                case PartitionKeyDefinitionVersion.V1:
                    {
                        long min = 0;
                        long max = uint.MaxValue;
                        if (!minInclusive.Equals(MinimumInclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
#pragma warning disable 0612
                            min = (long)((NumberPartitionKeyComponent)FromHexEncodedBinaryString(minInclusive).Components[0]).Value;
#pragma warning restore 0612
                        }

                        if (!maxExclusive.Equals(MaximumExclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
#pragma warning disable 0612
                            max = (long)((NumberPartitionKeyComponent)FromHexEncodedBinaryString(maxExclusive).Components[0]).Value;
#pragma warning restore 0612
                        }

                        if (max - min < numberOfSubRanges)
                        {
                            throw new InvalidOperationException("Insufficient range width to produce " + numberOfSubRanges + " equal sub ranges.");
                        }

                        string[] ranges = new string[numberOfSubRanges - 1];
                        for (int i = 1; i < numberOfSubRanges; i++)
                        {
                            ranges[i - 1] = ToHexEncodedBinaryString(new[] { new NumberPartitionKeyComponent(min + (i * ((max - min) / numberOfSubRanges))) });
                        }

                        return ranges;
                    }

                default:
                    throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionVersion");
            }
        }

        public static double GetWidth(string minInclusive, string maxExclusive, PartitionKeyDefinition partitionKeyDefinition)
        {
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new InvalidOperationException("Can determine range width only for hash partitioning.");
            }

            switch (partitionKeyDefinition.Version ?? PartitionKeyDefinitionVersion.V1)
            {
                case PartitionKeyDefinitionVersion.V2:
                    {
                        UInt128 min = 0;
                        if (!minInclusive.Equals(MinimumInclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
                            byte[] minBytes = PartitionKeyInternal.HexStringToByteArray(minInclusive);
                            Array.Reverse(minBytes);
                            min = UInt128.FromByteArray(minBytes);
                        }

                        UInt128 max = UInt128.MaxValue;
                        if (!maxExclusive.Equals(MaximumExclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
                            byte[] maxBytes = PartitionKeyInternal.HexStringToByteArray(maxExclusive);
                            Array.Reverse(maxBytes);
                            max = UInt128.FromByteArray(maxBytes);
                        }

                        double width = (1.0 * (max.GetHigh() - min.GetHigh())) / (UInt128.MaxValue.GetHigh() + 1);
                        return width;
                    }
                case PartitionKeyDefinitionVersion.V1:
                    {
                        long min = 0;
                        long max = uint.MaxValue;
                        if (!minInclusive.Equals(MinimumInclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
#pragma warning disable 0612
                            min = (long)((NumberPartitionKeyComponent)FromHexEncodedBinaryString(minInclusive).Components[0]).Value;
#pragma warning restore 0612
                        }

                        if (!maxExclusive.Equals(MaximumExclusiveEffectivePartitionKey, StringComparison.Ordinal))
                        {
#pragma warning disable 0612
                            max = (long)((NumberPartitionKeyComponent)FromHexEncodedBinaryString(maxExclusive).Components[0]).Value;
#pragma warning restore 0612
                        }

                        double width = (1.0 * (max - min)) / ((long)(UInt32.MaxValue) + 1);
                        return width;
                    }

                default:
                    throw new InternalServerErrorException("Unexpected PartitionKeyDefinitionVersion");
            }
        }
    }
}
