//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Common.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for NetUtil.GetEthernetIPv6Address() method.
    /// </summary>
    [TestClass]
    public sealed class NetUtilGetEthernetIPv6AddressTests
    {
        #region Test Infrastructure

        private sealed class TestNetworkInterface : NetworkInterface
        {
            public TestNetworkInterface(NetworkInterfaceType type, OperationalStatus status, long speed, bool isReceiveOnly = false)
            {
                this.NetworkInterfaceType = type;
                this.OperationalStatus = status;
                this.Speed = speed;
                this.IsReceiveOnly = isReceiveOnly;
                this.Addresses = new List<TestUnicastIPAddressInformation>();
                this.Gateways = new List<TestGatewayIPAddressInformation>();
            }

            public override NetworkInterfaceType NetworkInterfaceType { get; }
            
            public override OperationalStatus OperationalStatus { get; }
            
            public override long Speed { get; }
            
            public override bool IsReceiveOnly { get; }
            
            // REQUIRED by .NET Framework
            
            public override string Id => "TestNic-Id";
            
            public override string Name => "TestNic-Name";
            
            public override string Description => "Test network interface (unit test)";
            
            public override PhysicalAddress GetPhysicalAddress() => PhysicalAddress.None;
            
            public override IPv4InterfaceStatistics GetIPv4Statistics() => throw new NotSupportedException("IPv4 statistics not used in tests.");
            
            public override bool Supports(NetworkInterfaceComponent component) => false;
            
            public List<TestUnicastIPAddressInformation> Addresses { get; }
            
            public List<TestGatewayIPAddressInformation> Gateways { get; }

            public TestNetworkInterface AddAddress(string ip, bool isDnsEligible = true, bool isTransient = false, 
                DuplicateAddressDetectionState state = DuplicateAddressDetectionState.Preferred)
            {
                this.Addresses.Add(new TestUnicastIPAddressInformation(IPAddress.Parse(ip), isDnsEligible, isTransient, state));
                return this;
            }

            public TestNetworkInterface AddGateway(string gateway)
            {
                this.Gateways.Add(new TestGatewayIPAddressInformation(IPAddress.Parse(gateway)));
                return this;
            }

            public override IPInterfaceProperties GetIPProperties() => new TestIPInterfaceProperties(this.Addresses, this.Gateways);
        }

        private sealed class TestUnicastIPAddressInformation : UnicastIPAddressInformation
        {
            public TestUnicastIPAddressInformation(IPAddress address, bool isDnsEligible, bool isTransient, DuplicateAddressDetectionState state)
            {
                this.Address = address;
                this.IsDnsEligible = isDnsEligible;
                this.IsTransient = isTransient;
                this.DuplicateAddressDetectionState = state;
            }

            public override IPAddress Address { get; }
            
            public override bool IsDnsEligible { get; }
            
            public override bool IsTransient { get; }
            
            public override DuplicateAddressDetectionState DuplicateAddressDetectionState { get; }

            // Required abstract members on .NET Framework
            public override PrefixOrigin PrefixOrigin => PrefixOrigin.Manual;
            
            public override SuffixOrigin SuffixOrigin => SuffixOrigin.Manual;
            
            public override long AddressPreferredLifetime => long.MaxValue;
            
            public override long DhcpLeaseLifetime => long.MaxValue;
            
            public override long AddressValidLifetime => long.MaxValue;
            
            public override IPAddress IPv4Mask => null;
        }

        private sealed class TestGatewayIPAddressInformation : GatewayIPAddressInformation
        {
            public TestGatewayIPAddressInformation(IPAddress address) => this.Address = address;
            
            public override IPAddress Address { get; }
        }

        private sealed class TestIPInterfaceProperties : IPInterfaceProperties
        {
            public TestIPInterfaceProperties(List<TestUnicastIPAddressInformation> unicast, List<TestGatewayIPAddressInformation> gateways)
            {
                this.UnicastAddresses = new TestUnicastCollection(unicast);
                this.GatewayAddresses = new TestGatewayCollection(gateways);
            }

            public override UnicastIPAddressInformationCollection UnicastAddresses { get; }
            
            public override GatewayIPAddressInformationCollection GatewayAddresses { get; }
            
            public override string DnsSuffix => string.Empty;
            
            public override IPv4InterfaceProperties GetIPv4Properties() => throw new NotSupportedException("IPv4 properties not supported in test");
            
            public override IPAddressInformationCollection AnycastAddresses => throw new NotSupportedException("Anycast addresses not supported in test");
            
            public override MulticastIPAddressInformationCollection MulticastAddresses => throw new NotSupportedException("Multicast addresses not supported in test");
            
            public override IPAddressCollection DnsAddresses => throw new NotSupportedException("DNS addresses not supported in test");
            
            public override IPAddressCollection DhcpServerAddresses => throw new NotSupportedException("DHCP server addresses not supported in test");
            
            public override IPAddressCollection WinsServersAddresses => throw new NotSupportedException("WINS server addresses not supported in test");
            
            public override bool IsDnsEnabled => true;
            
            public override bool IsDynamicDnsEnabled => false;
            
            public override IPv6InterfaceProperties GetIPv6Properties() => throw new NotSupportedException("IPv6 properties not supported in test");
        }

        private sealed class TestUnicastCollection : UnicastIPAddressInformationCollection
        {
            private readonly List<TestUnicastIPAddressInformation> items;
            
            public TestUnicastCollection(List<TestUnicastIPAddressInformation> items) => this.items = items;
            
            public override int Count => this.items.Count;
            
            public override UnicastIPAddressInformation this[int index] => this.items[index];
            
            public override IEnumerator<UnicastIPAddressInformation> GetEnumerator() => this.items.Cast<UnicastIPAddressInformation>().GetEnumerator();
            
            public override bool IsReadOnly => true;
            
            public override void Add(UnicastIPAddressInformation item) => throw new NotSupportedException("Collection is read-only");
            
            public override void Clear() => throw new NotSupportedException("Collection is read-only");
            
            public override bool Contains(UnicastIPAddressInformation item) => this.items.Cast<UnicastIPAddressInformation>().Contains(item);
            
            public override void CopyTo(UnicastIPAddressInformation[] array, int arrayIndex) => this.items.Cast<UnicastIPAddressInformation>().ToArray().CopyTo(array, arrayIndex);
            
            public override bool Remove(UnicastIPAddressInformation item) => throw new NotSupportedException("Collection is read-only");
        }

        private sealed class TestGatewayCollection : GatewayIPAddressInformationCollection
        {
            private readonly List<TestGatewayIPAddressInformation> items;
            
            public TestGatewayCollection(List<TestGatewayIPAddressInformation> items) => this.items = items;
            
            public override int Count => this.items.Count;
            
            public override GatewayIPAddressInformation this[int index] => this.items[index];
            
            public override IEnumerator<GatewayIPAddressInformation> GetEnumerator() => this.items.Cast<GatewayIPAddressInformation>().GetEnumerator();
            
            public override bool IsReadOnly => true;
            
            public override void Add(GatewayIPAddressInformation item) => throw new NotSupportedException("Collection is read-only");
            
            public override void Clear() => throw new NotSupportedException("Collection is read-only");
            
            public override bool Contains(GatewayIPAddressInformation item) => this.items.Cast<GatewayIPAddressInformation>().Contains(item);
            
            public override void CopyTo(GatewayIPAddressInformation[] array, int arrayIndex) => this.items.Cast<GatewayIPAddressInformation>().ToArray().CopyTo(array, arrayIndex);
            
            public override bool Remove(GatewayIPAddressInformation item) => throw new NotSupportedException("Collection is read-only");
        }

        #endregion

        #region Test Builders

        private static TestNetworkInterface Ethernet(OperationalStatus status = OperationalStatus.Up, long speed = 1000000000) =>
            new TestNetworkInterface(NetworkInterfaceType.Ethernet, status, speed);

        private static TestNetworkInterface Wireless(OperationalStatus status = OperationalStatus.Up, long speed = 100000000) =>
            new TestNetworkInterface(NetworkInterfaceType.Wireless80211, status, speed);

        private static TestNetworkInterface ReceiveOnly() =>
            new TestNetworkInterface(NetworkInterfaceType.Ethernet, OperationalStatus.Up, 1000000000, isReceiveOnly: true);

        #endregion

        #region Core Algorithm Tests

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_NoOperationalInterfaces_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet(OperationalStatus.Down).AddAddress("2001:db8::1") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when no operational interfaces exist");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_OnlyNonEthernet_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Wireless().AddAddress("2001:db8::1") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when only non-Ethernet interfaces exist");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_ReceiveOnlyInterface_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { ReceiveOnly().AddAddress("2001:db8::1") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when only receive-only interfaces exist");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_IPv4Only_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("192.168.1.100") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when only IPv4 addresses are available");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_LinkLocalOnly_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("fe80::1") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when only link-local addresses are available");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_MulticastOnly_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("ff02::1") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when only multicast addresses are available");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_NotDnsEligible_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("2001:db8::1", isDnsEligible: false) };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when addresses are not DNS eligible");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_TransientAddress_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("2001:db8::1", isTransient: true) };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when only transient addresses are available");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_NonPreferredState_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("2001:db8::1", state: DuplicateAddressDetectionState.Duplicate) };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when addresses are not in preferred state");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_ValidGlobalAddress_ReturnsTrue()
        {
            NetworkInterface[] interfaces = new[] { Ethernet().AddAddress("2001:db8::1") };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsTrue(result, "Should find a suitable address");
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address, "Should return the valid global address");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_GatewayBonus_WinsOverGlobal()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("2001:db8::1"), // Global, no gateway
                Ethernet().AddAddress("2001:db8::2").AddGateway("2001:db8::ff") // Global, with gateway
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.AreEqual(IPAddress.Parse("2001:db8::2"), address, "Interface with IPv6 gateway should be selected");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_GlobalBonus_WinsOverULA()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("fd00::1"), // ULA
                Ethernet().AddAddress("2001:db8::1") // Global
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address, "Global address should be selected over ULA");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_EqualScore_ChoosesFirst()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("2001:db8::1"), // First address
                Ethernet().AddAddress("2001:db8::2") // Second address
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address, "First address should be selected when scores are equal");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_LinkLocalGateway_DoesNotCount()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("2001:db8::1").AddGateway("fe80::1"), // Link-local gateway
                Ethernet().AddAddress("2001:db8::2") // No gateway
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address, "First address should be selected when link-local gateway doesn't provide bonus");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_CompleteScoring_GatewayWins()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("2001:db8::1"), // Global = 10 points
                Ethernet().AddAddress("fd00::1").AddGateway("2001:db8::ff") // Gateway = 100 points
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.AreEqual(IPAddress.Parse("fd00::1"), address, "IPv6 gateway bonus should provide enough points to beat global address");
        }
        
        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_ULAOnly_ReturnsTrue()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("fc00::1"), // ULA fc00::/7
                Ethernet().AddAddress("fd12:3456::1") // ULA fd00::/8
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsTrue(result, "Should return true when ULA addresses are available");
            Assert.AreEqual(IPAddress.Parse("fc00::1"), address, "Should return the first ULA address when both have equal scores");
        }
        
        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_AllInvalidAddresses_ReturnsFalse()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("fe80::1").AddAddress("ff02::1"), // Link-local + multicast
                Ethernet().AddAddress("192.168.1.100"), // IPv4 only
                Ethernet().AddAddress("fd00::1", isDnsEligible: false) // ULA, not DNS eligible
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsFalse(result, "Should return false when all addresses are filtered out");
        }
        
        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_MixedInvalidAddresses_ReturnsLoopback()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("fe80::1").AddAddress("ff02::1"), // Link-local + multicast
                Ethernet().AddAddress("::1").AddAddress("192.168.1.100"), // IPv6 Loopback + IPv4
                Ethernet().AddAddress("fd00::1", isDnsEligible: false) // ULA, not DNS eligible
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsTrue(result, "Should find IPv6 loopback address");
            Assert.AreEqual(IPAddress.Parse("::1"), address, "Should return IPv6 loopback address");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_MultipleAddressesPerInterface_ChoosesBest()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("fe80::1").AddAddress("2001:db8::1").AddAddress("fd00::1") // Mix of link-local, global, ULA
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.IsTrue(result, "Should find the valid global address");
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address, "Should select the global address over ULA");
        }

        [TestMethod]
        [Owner("luying")]
        public void GetEthernetIPv6Address_IPv4Gateway_DoesNotCount()
        {
            NetworkInterface[] interfaces = new[]
            {
                Ethernet().AddAddress("2001:db8::1").AddGateway("192.168.1.1"), // IPv4 gateway
                Ethernet().AddAddress("2001:db8::2") // No gateway
            };
            bool result = NetUtil.TryGetEthernetIPv6Address(interfaces, out IPAddress address);
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), address, "First address should be selected when IPv4 gateway doesn't provide bonus");
        }

        #endregion

        #region Emulated Mode Test

        [TestMethod]
        [Owner("luying")]
        public void GetLocalIPv6Address_EmulatedMode_ReturnsLoopback()
        {
            bool result = NetUtil.TryGetLocalIPv6Address(isEmulated: true, out IPAddress address);
            Assert.IsTrue(result, "Emulated mode should always succeed");
            Assert.AreEqual(IPAddress.IPv6Loopback, address, "Emulated mode should return IPv6 loopback address");
        }

        #endregion
    }
}