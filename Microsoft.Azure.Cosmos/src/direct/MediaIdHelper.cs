//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Helper class to generate and parse media id (only used in frontend)
    /// </summary>
    internal sealed class MediaIdHelper
    {
        public static string NewMediaId(string attachmentId, byte storageIndex)
        {
            if (storageIndex == 0)
            {
                return attachmentId;
            }

            ResourceId attachmentResourceId = ResourceId.Parse(attachmentId);
            byte[] mediaId = new byte[ResourceId.Length + 1];
            attachmentResourceId.Value.CopyTo(mediaId, 0);
            mediaId[mediaId.Length - 1] = storageIndex;
            return ResourceId.ToBase64String(mediaId);
        }

        public static bool TryParseMediaId(string mediaId, out string attachmentId, out byte storageIndex)
        {
            storageIndex = 0;
            attachmentId = string.Empty;

            byte[] mediaIdBytes = null;

            try
            {
                mediaIdBytes = ResourceId.FromBase64String(mediaId);
            }
            catch(FormatException)
            {
                return false;
            }

            if (mediaIdBytes.Length != ResourceId.Length && mediaIdBytes.Length != (ResourceId.Length + 1))
            {
                return false;
            }

            if (mediaIdBytes.Length == ResourceId.Length)
            {
                storageIndex = 0;
                attachmentId = mediaId;
                return true;
            }

            storageIndex = mediaIdBytes[mediaIdBytes.Length - 1];
            attachmentId = ResourceId.ToBase64String(mediaIdBytes, 0, ResourceId.Length);
            return true;
        }
    }
}
