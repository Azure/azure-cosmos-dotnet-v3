//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionAddressInfoTests
    {
        [TestMethod]
        [Owner("kirankk")]
        public void ProtocolMapTest()
        {
            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation(
                    protocol: Protocol.Https,
                    physicalUri: "rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/131979370849449715p",
                    isPublic: false,
                    isPrimary: true,
                    isAuxiliary: false),
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: "rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/141979370849449715p",
                    isPublic: false,
                    isPrimary: false,
                    isAuxiliary: false),
            };

            PartitionAddressInformation pi = new PartitionAddressInformation(addresses);
            Assert.IsNotNull(pi.Get(Protocol.Https));
            Assert.IsNotNull(pi.Get(Protocol.Tcp));
            CollectionAssert.AreEquivalent(addresses, pi.AllAddresses.ToArray());

            AddressInformation[] httpsAddresses = addresses.Take(1).ToArray();
            pi = new PartitionAddressInformation(httpsAddresses);
            Assert.IsNotNull(pi.Get(Protocol.Https));
            Assert.IsNotNull(pi.Get(Protocol.Tcp));
            CollectionAssert.AreEquivalent(httpsAddresses, pi.AllAddresses.ToArray());
        }

        [TestMethod]
        [Owner("kirankk")]
        public void PerProtocolNoPublicEpTest()
        {
            Uri[] replicaAddresses = new Uri[]
            {
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/131979370849449715p"),
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/141979370849449715s"),
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/151979370849449715s"),
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[0].ToString(),
                    isPublic: false,
                    isPrimary: true,
                    isAuxiliary: false),
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[1].ToString(),
                    isPublic: false,
                    isPrimary: false,
                    isAuxiliary: false),
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[2].ToString(),
                    isPublic: false,
                    isPrimary: false,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: null,
                    protocol: Protocol.Tcp,
                    isPublic: false,
                    isPrimary: false,
                    isAuxiliary: false),
                new AddressInformation( // For filtering out 
                    protocol: Protocol.Https,
                    physicalUri: replicaAddresses[0].ToString(),
                    isPublic: false,
                    isPrimary: true,
                    isAuxiliary: false)
            };

            PerProtocolPartitionAddressInformation protocolInfo = new PerProtocolPartitionAddressInformation(Protocol.Tcp, addresses.ToList().AsReadOnly());
            CollectionAssert.AreEquivalent(addresses.Take(3).ToArray(), protocolInfo.ReplicaAddresses.ToArray());

            CollectionAssert.AreEquivalent(replicaAddresses, protocolInfo.ReplicaUris.ToArray());
            CollectionAssert.AreEquivalent(
                replicaAddresses.Skip(1).Select(x => new TransportAddressUri(x)).ToArray(),
                protocolInfo.NonPrimaryReplicaTransportAddressUris.ToArray());

            Assert.AreEqual(replicaAddresses[0], protocolInfo.PrimaryReplicaUri);
            Assert.AreEqual(Protocol.Tcp, protocolInfo.Protocol);
        }

        [TestMethod]
        [Owner("kirankk")]
        public void PerProtocolOnlyPublicEpTest()
        {
            Uri[] replicaAddresses = new Uri[]
            {
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/131979370849449715p"),
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/141979370849449715s"),
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[0].ToString(),
                    isPublic: true,
                    isPrimary: true,
                    isAuxiliary: false),
                new AddressInformation( // For filtering out 
                    protocol: Protocol.Https,
                    physicalUri: replicaAddresses[1].ToString(),
                    isPublic: false,
                    isPrimary: true,
                    isAuxiliary: false)
            };

            PerProtocolPartitionAddressInformation protocolInfo = new PerProtocolPartitionAddressInformation(Protocol.Tcp, addresses);
            CollectionAssert.AreEquivalent(addresses.Take(1).ToArray(), protocolInfo.ReplicaAddresses.ToArray());

            CollectionAssert.AreEquivalent(replicaAddresses.Take(1).ToArray(), protocolInfo.ReplicaUris.ToArray());
            CollectionAssert.AreEquivalent(new TransportAddressUri[] { }, protocolInfo.NonPrimaryReplicaTransportAddressUris.ToArray());

            Assert.AreEqual(replicaAddresses[0], protocolInfo.PrimaryReplicaUri);
            Assert.AreEqual(Protocol.Tcp, protocolInfo.Protocol);
        }

        [TestMethod]
        [Owner("kirankk")]
        public void PerProtocolPrimaryUriTest()
        {
            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: "rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/131979370849449715p",
                    isPublic: true,
                    isPrimary: true,
                    isAuxiliary: false),
            };

            PerProtocolPartitionAddressInformation protocolInfo = new PerProtocolPartitionAddressInformation(Protocol.Tcp, addresses);

            Assert.AreEqual(Protocol.Tcp, protocolInfo.Protocol);
            Assert.AreEqual(addresses[0].PhysicalUri, protocolInfo.PrimaryReplicaUri.ToString());

            addresses = new AddressInformation[]
            {
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: "rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/131979370849449715p[",
                    isPublic: true,
                    isPrimary: true,
                    isAuxiliary: false)
            };

            protocolInfo = new PerProtocolPartitionAddressInformation(Protocol.Tcp, addresses);

            Assert.AreEqual(Protocol.Tcp, protocolInfo.Protocol);
            Assert.AreEqual(null, protocolInfo.PrimaryReplicaUri);
        }

        [TestMethod]
        [Owner("kirankk")]
        public void PerProtocolPrimaryUriHookTest()
        {
            Uri[] replicaAddresses = new Uri[]
            {
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/131979370849449715p"),
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/141979370849449715s"),
                new Uri("rntbd://10.0.75.1:26921/apps/DocDbApp/services/DocDbMaster3/partitions/2c3b721d-98bc-40af-9a08-006b1188cbe8/replicas/151979370849449715s"),
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[0].ToString(),
                    isPublic: false,
                    isPrimary: true,
                    isAuxiliary: false),
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[1].ToString(),
                    isPublic: false,
                    isPrimary: false,
                    isAuxiliary: false),
                new AddressInformation(
                    protocol: Protocol.Tcp,
                    physicalUri: replicaAddresses[2].ToString(),
                    isPublic: false,
                    isPrimary: false,
                    isAuxiliary: false),
            };

            PerProtocolPartitionAddressInformation protocolInfo = new PerProtocolPartitionAddressInformation(Protocol.Tcp, addresses);

            Assert.AreEqual(Protocol.Tcp, protocolInfo.Protocol);
            Assert.AreEqual(replicaAddresses[0], protocolInfo.PrimaryReplicaUri);

            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // DefaultReplicaIndex NOT SET => PrimaryReplicaUri
            Uri primary = protocolInfo.GetPrimaryAddressUri(request).Uri;
            Assert.AreEqual(replicaAddresses[0], primary);

            // DefaultReplicaIndex SET & 0 => PrimaryReplicaUri
            request.DefaultReplicaIndex = 0;
            primary = protocolInfo.GetPrimaryAddressUri(request).Uri;
            Assert.AreEqual(replicaAddresses[0], primary);

            // DefaultReplicaIndex SET & in bounds => replicaAddresses[DefaultReplicaIndex]
            request.DefaultReplicaIndex = 1;
            primary = protocolInfo.GetPrimaryAddressUri(request).Uri;
            Assert.AreEqual(replicaAddresses[1], primary);

            // DefaultReplicaIndex SET & out-of-bounds => GoneException
            try
            {
                request.DefaultReplicaIndex = (uint)addresses.Length + 1;
                primary = protocolInfo.GetPrimaryAddressUri(request).Uri;
                Assert.AreEqual(replicaAddresses[1], primary);
            }
            catch (GoneException ex)
            {
                Assert.IsTrue(ex.Message.Contains("The requested resource is no longer available at the server. Returned addresses are"));
            }
        }
    }
}