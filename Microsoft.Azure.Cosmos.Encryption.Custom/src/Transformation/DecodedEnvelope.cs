// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    internal readonly struct DecodedEnvelope
    {
        public DecodedEnvelope(TypeMarker typeMarker, byte[] buffer, int offset, int length)
        {
            this.TypeMarker = typeMarker;
            this.Buffer = buffer;
            this.Offset = offset;
            this.Length = length;
        }

        public TypeMarker TypeMarker { get; }

        public byte[] Buffer { get; }

        public int Offset { get; }

        public int Length { get; }
    }
}
#endif
