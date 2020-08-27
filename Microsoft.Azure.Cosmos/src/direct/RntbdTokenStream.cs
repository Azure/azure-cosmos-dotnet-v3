//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
#if COSMOSCLIENT
    using System.Buffers;
#endif
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;

    internal abstract class RntbdTokenStream<T>
        where T : Enum
    {
        private static Dictionary<ushort, int> TokenPositionMap;
        internal RntbdToken[] tokens;

        // Ideally we could use MemoryPool but a lot of the APIs for GetBytes() for
        // System.Text.Encoding and Write/Read for stream don't take Memory<> in NetStandard
        // so we have to use ArrayPool instead.
#if COSMOSCLIENT
        private ArrayPool<byte> arrayPool = ArrayPool<byte>.Create();
        private List<byte[]> borrowedBytes = new List<byte[]>();
#endif

        protected void SetTokens(RntbdToken[] t)
        {
            Debug.Assert(t != null);
            Debug.Assert(this.tokens == null);
            this.tokens = t;
            if (TokenPositionMap == null)
            {
                TokenPositionMap = RntbdTokenStream<T>.GetTokenPositions(t);
            }
        }

        /// <summary>
        /// Gets a byte[] of at least <see cref="length"/> bytes from a pool.
        /// </summary>
        /// <param name="length">The length of bytes to retrieve</param>
        /// <remarks>
        /// The byte array returned is put back in the pool when the TokenStream is Reset().
        /// Typically this is done when the request is returned to a shared pool of RNTBD requests.
        /// </remarks>
        public byte[] GetBytes(int length)
        {
#if COSMOSCLIENT
            byte[] bytes = this.arrayPool.Rent(length);
            this.borrowedBytes.Add(bytes);
            return bytes;
#else
            return new byte[length];
#endif
        }

        public void Reset()
        {
            for (int i = 0; i < this.tokens.Length; i++)
            {
                this.tokens[i].isPresent = false;
            }

#if COSMOSCLIENT
            foreach (byte[] bytes in this.borrowedBytes)
            {
                this.arrayPool.Return(bytes);
            }

            this.borrowedBytes.Clear();
#endif
        }

        private static Dictionary<ushort, int> GetTokenPositions(RntbdToken[] t)
        {
            Debug.Assert(t != null);
            Dictionary<ushort, int> tokenPositions = new Dictionary<ushort, int>(t.Length);
            for (int i = 0; i < t.Length; i++)
            {
                tokenPositions[t[i].GetTokenIdentifier()] = i;
            }

            return tokenPositions;
        }

        public int CalculateLength()
        {
            int total = 0;
            foreach(RntbdToken token in this.tokens)
            {
                if (!token.isPresent)
                {
                    continue;
                }

                total += sizeof(RntbdTokenTypes); // type
                total += 2; // identifier

                // value
                switch(token.GetTokenType())
                {
                    case RntbdTokenTypes.Byte:
                        total += 1;
                        break;
                    case RntbdTokenTypes.UShort:
                        total += 2;
                        break;
                    case RntbdTokenTypes.ULong:
                    case RntbdTokenTypes.Long:
                        total += 4;
                        break;
                    case RntbdTokenTypes.ULongLong:
                    case RntbdTokenTypes.LongLong:
                        total += 8;
                        break;
                    case RntbdTokenTypes.Float:
                        total += 4;
                        break;
                    case RntbdTokenTypes.Double:
                        total += 8;
                        break;
                    case RntbdTokenTypes.Guid:
                        total += 12;
                        break;
                    case RntbdTokenTypes.SmallBytes:
                    case RntbdTokenTypes.SmallString:
                        total += 1;
                        total += token.value.valueBytes.Length;
                        break;
                    case RntbdTokenTypes.Bytes:
                    case RntbdTokenTypes.String:
                        total += 2;
                        total += token.value.valueBytes.Length;
                        break;
                    case RntbdTokenTypes.ULongBytes:
                    case RntbdTokenTypes.ULongString:
                        total += 4;
                        total += token.value.valueBytes.Length;
                        break;
                    default:
                        Debug.Assert(false, "Unexpected RntbdTokenType", "Unexpected RntbdTokenType to serialize: {0}", 
                            token.GetTokenType());
                        throw new BadRequestException();
                }
            }

            return total;
        }

        public void SerializeToBinaryWriter(ref BytesSerializer writer, out int tokensLength)
        {
            tokensLength = 0;
            foreach(RntbdToken token in this.tokens)
            {
                int tokenLength = 0;
                token.SerializeToBinaryWriter(ref writer, out tokenLength);
                tokensLength += tokenLength;
            }
        }

        public void ParseFrom(BinaryReader reader)
        {
            while(reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ushort identifier = reader.ReadUInt16();
                RntbdTokenTypes type = (RntbdTokenTypes)reader.ReadByte();

                RntbdToken token;
                if (TokenPositionMap.TryGetValue(identifier, out int tokenPosition))
                {
                    token = this.tokens[tokenPosition];
                }
                else
                {
                    token = new RntbdToken(false, type, identifier); // read the token content to a temp, if the token isn't recognized
                }

                if (token.isPresent)
                {
                    DefaultTrace.TraceError("Duplicate token with identifier {0} type {1} found in RNTBD token stream",
                        token.GetTokenIdentifier(), token.GetTokenType());

                    throw new InternalServerErrorException(RMResources.InternalServerError, this.GetValidationFailureHeader());
                }

                switch (token.GetTokenType())
                {
                    case RntbdTokenTypes.Byte:
                        token.value.valueByte = reader.ReadByte();
                        break;
                    case RntbdTokenTypes.UShort:
                        token.value.valueUShort = reader.ReadUInt16();
                        break;
                    case RntbdTokenTypes.ULong:
                        token.value.valueULong = reader.ReadUInt32();
                        break;
                    case RntbdTokenTypes.Long:
                        token.value.valueLong = reader.ReadInt32();
                        break;
                    case RntbdTokenTypes.ULongLong:
                        token.value.valueULongLong = reader.ReadUInt64();
                        break;
                    case RntbdTokenTypes.LongLong:
                        token.value.valueLongLong = reader.ReadInt64();
                        break;
                    case RntbdTokenTypes.Float:
                        token.value.valueFloat = reader.ReadSingle();
                        break;
                    case RntbdTokenTypes.Double:
                        token.value.valueDouble = reader.ReadDouble();
                        break;
                    case RntbdTokenTypes.Guid:
                        token.value.valueGuid = new Guid(reader.ReadBytes(16));
                        break;
                    case RntbdTokenTypes.SmallBytes:
                    case RntbdTokenTypes.SmallString:
                        {
                            byte length = reader.ReadByte();
                            token.value.valueBytes = reader.ReadBytes(length);
                            break;
                        }
                    case RntbdTokenTypes.Bytes:
                    case RntbdTokenTypes.String:
                        {
                            ushort length = reader.ReadUInt16();
                            token.value.valueBytes = reader.ReadBytes(length);
                            break;
                        }
                    case RntbdTokenTypes.ULongBytes:
                    case RntbdTokenTypes.ULongString:
                        {
                            UInt32 length = reader.ReadUInt32();
                            token.value.valueBytes = reader.ReadBytes((int)length);
                            break;
                        }
                    default:
                        DefaultTrace.TraceError("Unrecognized token type {0} with identifier {1} found in RNTBD token stream",
                            token.GetTokenType(), token.GetTokenIdentifier());

                        throw new InternalServerErrorException(RMResources.InternalServerError, this.GetValidationFailureHeader());
                }

                token.isPresent = true;
            }

            foreach(RntbdToken token in this.tokens)
            {
                if(!token.isPresent && token.IsRequired())
                {
                    DefaultTrace.TraceError("Required token with identifier {0} not found in RNTBD token stream",
                        token.GetTokenIdentifier());

                    throw new InternalServerErrorException(RMResources.InternalServerError, this.GetValidationFailureHeader());
                }
            }
        }

        private INameValueCollection GetValidationFailureHeader()
        {
            INameValueCollection validationFailureResponseHeader = new DictionaryNameValueCollection();
            validationFailureResponseHeader.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
            return validationFailureResponseHeader;
        }
    }
}
