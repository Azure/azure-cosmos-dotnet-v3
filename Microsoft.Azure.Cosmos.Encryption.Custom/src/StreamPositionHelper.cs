//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;

    internal static class StreamPositionHelper
    {
        public static bool TryResetToStart(Stream stream)
        {
            if (stream == null)
            {
                return false;
            }

            try
            {
                if (!stream.CanSeek)
                {
                    return false;
                }

                stream.Position = 0;
                return true;
            }
            catch
            {
                // Stream implementations can throw arbitrary exceptions from Position.
                // Callers use this result to preserve the primary parse or decrypt failure.
                return false;
            }
        }

        public static void ResetToStart(Stream stream, string parameterName)
        {
            if (!TryResetToStart(stream))
            {
                throw new ArgumentException("Stream must support resetting its position.", parameterName);
            }
        }
    }
}
