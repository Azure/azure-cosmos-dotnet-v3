//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;

    internal static class SecurityUtility
    {
        /// <summary>
        /// Computes a keyed hash of a given text and returns. It fills the buffer "hash" with computed hash value.
        /// </summary>
        /// <param name="plainText">Plain text bytes whose hash has to be computed.</param>
        /// <param name="key">key used for the HMAC.</param>
        /// <param name="hash">Output buffer where the computed hash value is stored. If it is less than 32 bytes, the hash is truncated.</param>
        internal static void GetHMACWithSHA256(byte[] plainText, byte[] key, byte[] hash)
        {
            const int MaxSHA256HashBytes = 32;

            Debug.Assert(key != null && plainText != null);
            Debug.Assert(hash.Length != 0 && hash.Length <= MaxSHA256HashBytes);

            using HMACSHA256 hmac = new (key);
            byte[] computedHash = hmac.ComputeHash(plainText);

            // Truncate the hash if needed
            Buffer.BlockCopy(computedHash, 0, hash, 0, hash.Length);
        }

        /// <summary>
        /// Computes SHA256 hash of a given input.
        /// </summary>
        /// <param name="input">input byte array which needs to be hashed.</param>
        /// <returns>Returns SHA256 hash in a string form.</returns>
        internal static string GetSHA256Hash(byte[] input)
        {
            Debug.Assert(input != null);

            using (SHA256 sha256 = SHA256.Create())
            {
#pragma warning disable CA1850 // Prefer static 'HashData' method over 'ComputeHash'
                byte[] hashValue = sha256.ComputeHash(input);
#pragma warning restore CA1850 // Prefer static 'HashData' method over 'ComputeHash'
                return GetHexString(hashValue);
            }
        }

        /// <summary>
        /// Generates cryptographically random bytes.
        /// </summary>
        /// <param name="randomBytes">Buffer into which cryptographically random bytes are to be generated.</param>
        internal static void GenerateRandomBytes(byte[] randomBytes)
        {
            // Generate random bytes cryptographically.
#if NET8_0_OR_GREATER
            RandomNumberGenerator.Fill(randomBytes);
#else
            using RNGCryptoServiceProvider rngCsp = new ();
            rngCsp.GetBytes(randomBytes);
#endif
        }

        /// <summary>
        /// Compares two byte arrays and returns true if all bytes are equal.
        /// </summary>
        /// <param name="buffer1">input buffer</param>
        /// <param name="buffer2">another buffer to be compared against</param>
        /// <param name="buffer2Index">index in second buffer</param>
        /// <param name="lengthToCompare">length to compare</param>
        /// <returns>returns true if both the arrays have the same byte values else returns false</returns>
        internal static bool CompareBytes(byte[] buffer1, byte[] buffer2, int buffer2Index, int lengthToCompare)
        {
            if (buffer1 == null || buffer2 == null)
            {
                return false;
            }

            Debug.Assert(buffer1.Length >= lengthToCompare, "invalid lengthToCompare");
            Debug.Assert(buffer2Index > -1 && buffer2Index < buffer2.Length, "invalid index");
            if ((buffer2.Length - buffer2Index) < lengthToCompare)
            {
                return false;
            }

            for (int index = 0; index < buffer1.Length && index < lengthToCompare; ++index)
            {
                if (buffer1[index] != buffer2[buffer2Index + index])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets hex representation of byte array.
        /// <param name="input">input byte array</param>
        /// </summary>
        private static string GetHexString(byte[] input)
        {
            Debug.Assert(input != null);

            StringBuilder str = new ();
            foreach (byte b in input)
            {
                str.AppendFormat(b.ToString(@"X2"));
            }

            return str.ToString();
        }
    }
}
