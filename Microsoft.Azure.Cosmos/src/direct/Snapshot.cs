//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a snapshot in the Azure Cosmos DB service.
    /// A snapshot represents a point in time logical snapshot of all data in an Azure Cosmos DB container. 
    /// </summary>
    /// <remarks>
    /// An Azure Cosmos DB container is a repository for documents. A snapshot represents the state of
    /// this container at a certain point in time.
    /// Refer to <see>http://azure.microsoft.com/documentation/articles/documentdb-resources/#snapshots</see> for more details on snapshots.
    /// </remarks>
    /// <example>
    /// The example below starts a creation of a snapshot for a specific container called "myContainer" 
    /// which lives under a database named "myDatabase".
    /// <code language="c#">
    /// <![CDATA[
    /// Snapshot snapshot = await client.CreateSnapshotAsync(
    ///     new Snapshot 
    ///     { 
    ///         Id = "MySnapshot",
    ///         ResourceLink = "dbs/myDatabase/colls/myContainer"
    ///     }).Result;
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
#if COSMOSCLIENT
    internal
#else
    internal
#endif
    class Snapshot : Resource
    {
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
#pragma warning disable IDE0044 // Add readonly modifier
        private static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword

        private SnapshotContent snapshotContent;

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot"/> class for the Azure Cosmos DB service.
        /// </summary>
        public Snapshot()
        {
        }

        /// <summary> 
        /// Gets or sets the link of the container resource for which the snapshot was created in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The name link of the container resource to which the snapshot applies, for example, "dbs/myDbName/colls/myCollName".
        /// </value>
        /// <example>
        /// dbs/myDbName/colls/myCollName
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.ResourceLink)]
        public string ResourceLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ResourceLink);
            }
            set
            {
                base.SetValue(Constants.Properties.ResourceLink, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="SnapshotState"/> associated with the snapshot from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The <see cref="SnapshotState"/> of this snapshot resource.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.State)]
        public SnapshotState State
        {
            get
            {
                string strValue = base.GetValue<string>(Constants.Properties.State);
                if (string.IsNullOrEmpty(strValue))
                {
                    return SnapshotState.Invalid;
                }

                SnapshotState state = (SnapshotState)Enum.Parse(typeof(SnapshotState), strValue, true);
                return state;
            }
            internal set
            {
                base.SetValue(Constants.Properties.State, value.ToString());
            }
        }

        /// <summary>
        /// Gets the <see cref="SnapshotKind"/> associated with the snapshot from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The <see cref="SnapshotKind"/> of this snapshot resource.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Kind)]
        public SnapshotKind Kind
        {
            get
            {
                string strValue = base.GetValue<string>(Constants.Properties.Kind);
                if (string.IsNullOrEmpty(strValue))
                {
                    return SnapshotKind.Invalid;
                }

                SnapshotKind kind = (SnapshotKind)Enum.Parse(typeof(SnapshotKind), strValue, true);
                return kind;
            }
            internal set
            {
                base.SetValue(Constants.Properties.Kind, value.ToString());
            }
        }

        /// <summary>
        /// Gets the snapshot's timestamp from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The timestamp at which this snapshot was created.</value>
        [JsonProperty(PropertyName = Constants.SnapshotProperties.SnapshotTimestamp)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime SnapshotTimestamp
        {
            get
            {
                // Add seconds to the unix start time
                return UnixStartTime.AddSeconds(base.GetValue<double>(Constants.SnapshotProperties.SnapshotTimestamp));
            }
            set
            {
                base.SetValue(Constants.SnapshotProperties.SnapshotTimestamp, (ulong)(value - UnixStartTime).TotalSeconds);
            }
        }

        /// <summary>
        /// Gets the <see cref="ResourceId"/> string of the container for which the snapshot was created.
        /// </summary>
        /// <value>The resource ID of the container for which this snapshot was created.</value>
        [JsonProperty(PropertyName = Constants.Properties.OwnerResourceId)]
        internal string OwnerResourceId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.OwnerResourceId);
            }
            set
            {
                base.SetValue(Constants.Properties.OwnerResourceId, value);
            }
        }

        /// <summary>
        /// Gets the size of collection snapshot in kilobytes.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SizeInKB)]
        public ulong SizeInKB
        {
            get
            {
                return base.GetValue<ulong>(Constants.Properties.SizeInKB);
            }
            internal set
            {
                base.SetValue(Constants.Properties.SizeInKB, value);
            }
        }

        /// <summary>
        /// Gets the compressed size of collection snapshot in kilobytes.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.CompressedSizeInKB)]
        public ulong CompressedSizeInKB
        {
            get
            {
                return base.GetValue<ulong>(Constants.Properties.CompressedSizeInKB);
            }
            internal set
            {
                base.SetValue(Constants.Properties.CompressedSizeInKB, value);
            }
        }

        /// <summary>
        /// Gets the LSN of the snapshot resource.
        /// </summary>
        [JsonProperty(PropertyName = Constants.SnapshotProperties.LSN)]
        internal long LSN
        {
            get
            {
                return base.GetValue<long>(Constants.SnapshotProperties.LSN);
            }
            set
            {
                base.SetValue(Constants.SnapshotProperties.LSN, value);
            }
        }

        /// <summary>
        /// Content of the Snapshot
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Content)]
        internal SnapshotContent Content
        {
            get
            {
                if (this.snapshotContent == null)
                {
                    this.snapshotContent = base.GetObject<SnapshotContent>(Constants.Properties.Content);
                }
                return this.snapshotContent;
            }
            set
            {
                this.snapshotContent = value;
                base.SetObject(Constants.Properties.Content, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="ParentResourceId"/> string of the parent for which the snapshot was created.
        /// </summary>
        /// <value>The resource ID of the parent for which this snapshot was created.</value>
        [JsonProperty(PropertyName = Constants.Properties.ParentResourceId)]
        internal string ParentResourceId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ParentResourceId);
            }
            set
            {
                base.SetValue(Constants.Properties.ParentResourceId, value);
            }
        }

        internal override void OnSave()
        {
            base.OnSave();

            if (this.snapshotContent != null)
            {
                this.snapshotContent.OnSave();
                base.SetObject(Constants.Properties.Content, this.snapshotContent);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<string>(Constants.Properties.ResourceLink);
            base.GetValue<string>(Constants.Properties.State);
            base.GetValue<string>(Constants.Properties.Kind);
            base.GetValue<double>(Constants.SnapshotProperties.SnapshotTimestamp);
            base.GetValue<string>(Constants.Properties.OwnerResourceId);
            base.GetValue<ulong>(Constants.Properties.SizeInKB);
            base.GetValue<long>(Constants.SnapshotProperties.LSN);
            base.GetValue<ulong>(Constants.Properties.CompressedSizeInKB);
            base.GetValue<string>(Constants.Properties.ParentResourceId);

            if (this.Content != null)
            {
                this.Content.Validate();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot"/> class from existing snapshot object.
        /// </summary>
        /// <param name="existingSnapshot"></param>
        /// <param name="operationType"></param>
        /// <param name="inheritSnapshotTimestamp"></param>
        internal static Snapshot CloneSystemSnapshot(
            Snapshot existingSnapshot,
            OperationType operationType,
            bool inheritSnapshotTimestamp)
        {
            if (existingSnapshot.Kind != SnapshotKind.System)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid snapshot kind {0}", existingSnapshot.Kind));
            }

            Snapshot snapshot = new Snapshot();

            snapshot.Kind = existingSnapshot.Kind;
            snapshot.OwnerResourceId = existingSnapshot.OwnerResourceId;
            snapshot.ResourceLink = existingSnapshot.ResourceLink;
            snapshot.Content = new SnapshotContent()
            {
                OperationType = operationType,
                SerializedDatabase = existingSnapshot.Content.SerializedDatabase,
                SerializedCollection = existingSnapshot.Content.SerializedCollection,
                SerializedOffer = existingSnapshot.Content.SerializedOffer,
                SerializedPartitionKeyRanges = existingSnapshot.Content.SerializedPartitionKeyRanges,
                GeoLinkIdToPKRangeRid = existingSnapshot.Content.GeoLinkIdToPKRangeRid
            };

            if (inheritSnapshotTimestamp)
            {
                snapshot.SnapshotTimestamp = existingSnapshot.SnapshotTimestamp;
            }
            else
            {
                // Setting to Timestamp to UnixStartTime so that the actual epoch timestamp gets zero.
                // If actual timestamp is zero, then backend is going to auto-populate the snapshotTimestamp to current timestamp.
                snapshot.SnapshotTimestamp = UnixStartTime;
            }

            snapshot.State = SnapshotState.Completed;

            return snapshot;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot"/> class from existing snapshot object.
        /// </summary>
        /// <param name="existingSnapshot"></param>
        /// <param name="updatedSnapshotContent"></param>
        internal static Snapshot CloneSystemSnapshot(
            Snapshot existingSnapshot,
            SnapshotContent updatedSnapshotContent)
        {
            if (existingSnapshot.Kind != SnapshotKind.System)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid snapshot kind {0}", existingSnapshot.Kind));
            }

            Snapshot newSnapshot;
            byte[] byteArray = existingSnapshot.ToByteArray();
            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                newSnapshot = JsonSerializable.LoadFrom<Snapshot>(stream);
            }

            newSnapshot.Content = updatedSnapshotContent;
            return newSnapshot;
        }
    }
}
