//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Util
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    internal class HashingExtension
    {
        /// <summary>
        /// Hash a passed Value
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns>hashed Value</returns>
        internal static string ComputeHash(string rawData)
        {
            if (string.IsNullOrEmpty(rawData))
            {
                throw new ArgumentNullException(nameof(rawData));
            }

            // Create a SHA256    
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                Array.Resize(ref bytes, 16);

                // Convert byte array to a string   
                return new Guid(bytes).ToString();
            }
        }
    }
}
