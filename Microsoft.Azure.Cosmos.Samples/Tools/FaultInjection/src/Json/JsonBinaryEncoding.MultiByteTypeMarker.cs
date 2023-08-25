// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// Struct to hold the a multibyte type marker.
        /// </summary>
        public readonly struct MultiByteTypeMarker
        {
            /// <summary>
            /// Initializes a new instance of the MultiByteTypeMarker struct.
            /// </summary>
            /// <param name="length">The length of the typemarker.</param>
            /// <param name="one">The first byte.</param>
            /// <param name="two">The second byte.</param>
            /// <param name="three">The third byte.</param>
            /// <param name="four">The fourth byte.</param>
            /// <param name="five">The fifth byte.</param>
            /// <param name="six">The sixth byte.</param>
            /// <param name="seven">The seventh byte.</param>
            public MultiByteTypeMarker(
                byte length,
                byte one = 0,
                byte two = 0,
                byte three = 0,
                byte four = 0,
                byte five = 0,
                byte six = 0,
                byte seven = 0)
            {
                this.Length = length;
                this.One = one;
                this.Two = two;
                this.Three = three;
                this.Four = four;
                this.Five = five;
                this.Six = six;
                this.Seven = seven;
            }

            public byte Length { get; }

            public byte One { get; }

            public byte Two { get; }

            public byte Three { get; }

            public byte Four { get; }

            public byte Five { get; }

            public byte Six { get; }

            public byte Seven { get; }
        }
    }
}
