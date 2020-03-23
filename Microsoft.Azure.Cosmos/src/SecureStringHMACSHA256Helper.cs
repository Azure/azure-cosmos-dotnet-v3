//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Security;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Manufactures SHA256 HMACs of byte payloads using a key. The key is a Base64-encoded SecureString.
    /// In keeping with the goals of SecureString, neither the original Base64 characters nor the decoded 
    /// bytes ever enters the managed heap, and they are kept decrypted in native memory for as short a 
    /// time as possible: just the duration of a single ComputeHash call.
    /// </summary>
    internal sealed class SecureStringHMACSHA256Helper : IComputeHash
    {
        private const uint SHA256HashOutputSizeInBytes = 32; // SHA256 => 256 bits => 32 bytes
        private readonly int keyLength;
        private IntPtr algorithmHandle;

        public SecureStringHMACSHA256Helper(SecureString base64EncodedKey)
        {
            this.Key = base64EncodedKey;
            // caching the length of SecureString as calling Length method on it everytime causes a performance hit
            this.keyLength = base64EncodedKey.Length;
            this.algorithmHandle = IntPtr.Zero;

            int status = NativeMethods.BCryptOpenAlgorithmProvider(out this.algorithmHandle,
                NativeMethods.BCRYPT_SHA256_ALGORITHM,
                IntPtr.Zero,
                NativeMethods.BCRYPT_ALG_HANDLE_HMAC_FLAG);
            if (status != 0)
            {
                throw new Win32Exception(status, "BCryptOpenAlgorithmProvider");
            }
        }

        public SecureString Key { get; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.algorithmHandle != null)
                {
                    int status = NativeMethods.BCryptCloseAlgorithmProvider(this.algorithmHandle, 0);
                    if (status != 0)
                    {
                        DefaultTrace.TraceError("Failed to close algorithm provider: {0}", status);
                    }

                    this.algorithmHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Decode the SecureString containing the Base64-encoded key into native memory, compute the
        /// SHA256 HMAC of the payload, and destroy the native memory containing the decoded key.
        /// </summary>
        /// <param name="bytesToHash">payload that is hashed</param>
        public byte[] ComputeHash(byte[] bytesToHash)
        {
            IntPtr hashHandle = IntPtr.Zero;

            try
            {
                this.InitializeBCryptHash(this.Key, this.keyLength, out hashHandle);
                this.AddData(hashHandle, bytesToHash);
                return this.FinishHash(hashHandle);
            }
            finally
            {
                if (hashHandle != IntPtr.Zero)
                {
                    int ignored = NativeMethods.BCryptDestroyHash(hashHandle);
                    hashHandle = IntPtr.Zero;
                }
            }
        }

        private void AddData(IntPtr hashHandle, byte[] data)
        {
            GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                int status = NativeMethods.BCryptHashData(hashHandle,
                    h.AddrOfPinnedObject(),
                    (uint)data.Length,
                    0);
                if (status != 0)
                {
                    throw new Win32Exception(status, "BCryptHashData");
                }
            }
            finally
            {
                h.Free();
            }
        }

        private byte[] FinishHash(IntPtr hashHandle)
        {
            byte[] finishedHash = new byte[SecureStringHMACSHA256Helper.SHA256HashOutputSizeInBytes];
            GCHandle h = GCHandle.Alloc(finishedHash, GCHandleType.Pinned);
            try
            {
                int status = NativeMethods.BCryptFinishHash(hashHandle,
                    h.AddrOfPinnedObject(),
                    (uint)finishedHash.Length,
                    0);
                if (status != 0)
                {
                    throw new Win32Exception(status, "BCryptFinishData");
                }

            }
            finally
            {
                h.Free();
            }

            return finishedHash;
        }

        private void InitializeBCryptHash(SecureString base64EncodedPassword, int base64EncodedPasswordLength, out IntPtr hashHandle)
        {
            IntPtr keyBytes = IntPtr.Zero;
            uint keyBytesLength = 0;
            try
            {
                Base64Helper.SecureStringToNativeBytes(base64EncodedPassword, base64EncodedPasswordLength, out keyBytes, out keyBytesLength);

                int status = NativeMethods.BCryptCreateHash(this.algorithmHandle,
                    out hashHandle,
                    IntPtr.Zero,
                    0,
                    keyBytes,
                    keyBytesLength,
                    0);
                if (status != 0)
                {
                    throw new Win32Exception(status, "BCryptCreateHash");
                }
            }
            finally
            {
                if (keyBytes != IntPtr.Zero)
                {
                    for (int n = 0; n < (int)keyBytesLength; n++)
                    {
                        Marshal.WriteByte(keyBytes, n, 0);
                    }

                    Marshal.FreeCoTaskMem(keyBytes);
                    keyBytes = IntPtr.Zero;
                    keyBytesLength = 0;
                }
            }
        }

        private static class NativeMethods
        {
            public const string BCRYPT_SHA256_ALGORITHM = "SHA256";
            public const uint BCRYPT_ALG_HANDLE_HMAC_FLAG = 0x00000008;

            [DllImport("Bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptOpenAlgorithmProvider(
                out IntPtr algorithmHandle,
                string algorithmId,
                IntPtr implementation,
                uint flags);

            [DllImport("Bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptCloseAlgorithmProvider(
                IntPtr algorithmHandle,
                uint flags);

            [DllImport("Bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptCreateHash(
                IntPtr algorithmHandle,
                out IntPtr hashHandle,
                IntPtr workingSpace, // optional, we just let BCrypt allocate
                uint workingSpaceSize,
                IntPtr keyBytes,
                uint keyBytesLength,
                uint flags);

            [DllImport("Bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptDestroyHash(
                IntPtr hashHandle);

            [DllImport("Bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptHashData(
                IntPtr hashHandle,
                IntPtr bytes,
                uint byteLength,
                uint flags);

            [DllImport("Bcrypt.dll", CharSet = CharSet.Unicode)]
            public static extern int BCryptFinishHash(
                IntPtr hashHandle,
                IntPtr byteOutputLocation,
                uint byteOutputLocationSize,
                uint flags);
        }
    }
}
