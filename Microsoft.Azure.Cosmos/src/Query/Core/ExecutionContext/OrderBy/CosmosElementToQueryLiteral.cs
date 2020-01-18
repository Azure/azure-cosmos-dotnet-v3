// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Newtonsoft.Json;

    internal sealed class CosmosElementToQueryLiteral : ICosmosElementVisitor
    {
        private readonly StringBuilder stringBuilder;
        private readonly CosmosNumberToQueryLiteral cosmosNumberToQueryLiteral;

        public CosmosElementToQueryLiteral(StringBuilder stringBuilder)
        {
            this.stringBuilder = stringBuilder ?? throw new ArgumentNullException(nameof(stringBuilder));
            this.cosmosNumberToQueryLiteral = new CosmosNumberToQueryLiteral(stringBuilder);
        }

        public void Visit(CosmosArray cosmosArray)
        {
            this.stringBuilder.Append("[");

            for (int i = 0; i < cosmosArray.Count; i++)
            {
                if (i > 0)
                {
                    this.stringBuilder.Append(",");
                }

                CosmosElement arrayItem = cosmosArray[i];
                arrayItem.Accept(this);
            }

            this.stringBuilder.Append("]");
        }

        public void Visit(CosmosBinary cosmosBinary)
        {
            this.stringBuilder.AppendFormat("C_Binary(\"0x{0}\")", HexStringBuilder.ByteArrayToHexViaLookup32Unsafe(cosmosBinary.Value.Span));
        }

        public void Visit(CosmosBoolean cosmosBoolean)
        {
            this.stringBuilder.Append(cosmosBoolean.Value ? "true" : "false");
        }

        public void Visit(CosmosGuid cosmosGuid)
        {
            this.stringBuilder.AppendFormat("C_Guid(\"{0}\")", cosmosGuid.Value);
        }

        public void Visit(CosmosNull cosmosNull)
        {
            this.stringBuilder.Append("null");
        }

        public void Visit(CosmosNumber cosmosNumber)
        {
            cosmosNumber.Accept(this.cosmosNumberToQueryLiteral);
        }

        public void Visit(CosmosObject cosmosObject)
        {
            this.stringBuilder.Append("{");

            string separator = string.Empty;
            foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
            {
                this.stringBuilder.Append(separator);
                separator = ",";

                CosmosString.Create(kvp.Key).Accept(this);
                this.stringBuilder.Append(":");
                kvp.Value.Accept(this);
            }

            this.stringBuilder.Append("}");
        }

        public void Visit(CosmosString cosmosString)
        {
            this.stringBuilder.Append(JsonConvert.SerializeObject(cosmosString.Value, DefaultJsonSerializationSettings.Value));
        }

        private sealed class CosmosNumberToQueryLiteral : ICosmosNumberVisitor
        {
            private readonly StringBuilder stringBuilder;

            public CosmosNumberToQueryLiteral(StringBuilder stringBuilder)
            {
                this.stringBuilder = stringBuilder ?? throw new ArgumentNullException(nameof(stringBuilder));
            }

            public void Visit(CosmosFloat32 cosmosFloat32)
            {
                this.stringBuilder.AppendFormat("C_Float32({0:G7})", cosmosFloat32.GetValue());
            }

            public void Visit(CosmosFloat64 cosmosFloat64)
            {
                this.stringBuilder.AppendFormat("C_Float64({0:R})", cosmosFloat64.GetValue());
            }

            public void Visit(CosmosInt16 cosmosInt16)
            {
                this.stringBuilder.AppendFormat("C_Int16({0})", cosmosInt16.GetValue());
            }

            public void Visit(CosmosInt32 cosmosInt32)
            {
                this.stringBuilder.AppendFormat("C_Int32({0})", cosmosInt32.GetValue());
            }

            public void Visit(CosmosInt64 cosmosInt64)
            {
                this.stringBuilder.AppendFormat("C_Int64({0})", cosmosInt64.GetValue());
            }

            public void Visit(CosmosInt8 cosmosInt8)
            {
                this.stringBuilder.AppendFormat("C_Int8({0})", cosmosInt8.GetValue());
            }

            public void Visit(CosmosNumber64 cosmosNumber64)
            {
                this.stringBuilder.Append(cosmosNumber64.GetValue());
            }

            public void Visit(CosmosUInt32 cosmosUInt32)
            {
                this.stringBuilder.AppendFormat("C_UInt32({0})", cosmosUInt32.GetValue());
            }
        }

        /// <summary>
        /// Stole this from stackoverflow:
        /// https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa
        /// https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/24343727#24343727
        /// </summary>
        private unsafe static class HexStringBuilder
        {
            private const int MaxStackAllocLength = 4096;

            private static readonly uint[] lookup32Unsafe = CreateLookup32Unsafe();
            private static readonly uint* lookup32UnsafeP = (uint*)GCHandle.Alloc(lookup32Unsafe, GCHandleType.Pinned).AddrOfPinnedObject();

            private static uint[] CreateLookup32Unsafe()
            {
                uint[] result = new uint[256];
                for (int i = 0; i < 256; i++)
                {
                    string s = i.ToString("X2");
                    if (BitConverter.IsLittleEndian)
                        result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
                    else
                        result[i] = ((uint)s[1]) + ((uint)s[0] << 16);
                }

                return result;
            }

            public static string ByteArrayToHexViaLookup32Unsafe(ReadOnlySpan<byte> bytes)
            {
                uint* lookupP = lookup32UnsafeP;
                int lengthRequired = bytes.Length * 2;
                Span<char> result = lengthRequired < MaxStackAllocLength ? stackalloc char[lengthRequired] : new char[lengthRequired];
                fixed (byte* bytesP = bytes)
                fixed (char* resultP = result)
                {
                    uint* resultP2 = (uint*)resultP;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        resultP2[i] = lookupP[bytesP[i]];
                    }

                    return new string(resultP, startIndex: 0, length: result.Length);
                }
            }
        }
    }
}
