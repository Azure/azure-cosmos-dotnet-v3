namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// This class sets that the EndianAwareBinaryReader can read from a little endian stream regaurdless of the architechture
    /// To test this you should run the tests twice (once on x86 and other on ARM).
    /// </summary>
    [TestClass]
    public class LittleEndianBinaryReaderTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            // Put test init code here
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            // put class init code here
        }

        [TestMethod]
        [Owner("brchon")]
        public void BoolTest()
        {
            byte[] trueBytes = { 1 };
            byte[] falseBytes = { 0 };

            this.VerifyEndianAwareBinaryReader<bool>(trueBytes, true, true);
            this.VerifyEndianAwareBinaryReader<bool>(falseBytes, false, false);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ByteTest()
        {
            byte value = 42;
            this.VerifyEndianAwareBinaryReader<byte>(new byte[1] { value }, value, value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            byte[] littleEndianBytes = { 0x00, 0x00, 0x00, 0x00, 0x00, 0xE4, 0x94, 0x40 };
            byte[] bigEndianBytes = new byte[8];
            double littleEndianValue = 1337;
            littleEndianBytes.CopyTo(bigEndianBytes, 0);
            Array.Reverse(bigEndianBytes);
            double bigEndianValue = BitConverter.ToDouble(bigEndianBytes, 0);

            this.VerifyEndianAwareBinaryReader<double>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int16Test()
        {
            byte[] littleEndianBytes = { 0x04, 0x00 };
            short littleEndianValue = 4;
            short bigEndianValue = 1024;
            this.VerifyEndianAwareBinaryReader<short>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int32Test()
        {
            byte[] littleEndianBytes = { 0x04, 0x00, 0x00, 0x00 };
            int littleEndianValue = 4;
            int bigEndianValue = 67108864;
            this.VerifyEndianAwareBinaryReader<int>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int64Test()
        {
            byte[] littleEndianBytes = { 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            long littleEndianValue = 4;
            long bigEndianValue = 288230376151711744;
            this.VerifyEndianAwareBinaryReader<long>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SByteTest()
        {
            byte[] littleEndianBytes = { 0x04 };
            sbyte littleEndianValue = 4;
            sbyte bigEndianValue = 4;
            this.VerifyEndianAwareBinaryReader<sbyte>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SingleTest()
        {
            byte[] littleEndianBytes = { 0x00, 0x20, 0xA7, 0x44 };
            float littleEndianValue = 1337;
            float bigEndianValue = 2.998739e-39F;
            this.VerifyEndianAwareBinaryReader<float>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void UInt16Test()
        {
            byte[] littleEndianBytes = { 0x04, 0x00 };
            ushort littleEndianValue = 4;
            ushort bigEndianValue = 1024;
            this.VerifyEndianAwareBinaryReader<ushort>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void UInt32Test()
        {
            byte[] littleEndianBytes = { 0x04, 0x00, 0x00, 0x00 };
            uint littleEndianValue = 4;
            uint bigEndianValue = 67108864;
            this.VerifyEndianAwareBinaryReader<uint>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        [TestMethod]
        [Owner("brchon")]
        public void UInt64Test()
        {
            byte[] littleEndianBytes = { 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            ulong littleEndianValue = 4;
            ulong bigEndianValue = 288230376151711744;
            this.VerifyEndianAwareBinaryReader<ulong>(littleEndianBytes, littleEndianValue, bigEndianValue);
        }

        private LittleEndianBinaryReader CreateReader(byte[] buffer)
        {
            return new LittleEndianBinaryReader(new MemoryStream(buffer));
        }

        private void SetIsLittleEndianField(bool isLittleEndian)
        {
            Type bitConverterType = typeof(BitConverter);
            FieldInfo isLittleEndianField = bitConverterType.GetField("IsLittleEndian");
            isLittleEndianField.SetValue(null, isLittleEndian);
        }

        private void CompareActualOutput(LittleEndianBinaryReader endianAwareBinaryReader, object expectedOutput)
        {
            Type expectedOutputType = expectedOutput.GetType();

            if (expectedOutputType == typeof(bool))
            {
                bool actualOutput = endianAwareBinaryReader.ReadBoolean();
                Assert.AreEqual((bool)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(byte))
            {
                byte actualOutput = endianAwareBinaryReader.ReadByte();
                Assert.AreEqual((byte)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(double))
            {
                double actualOutput = endianAwareBinaryReader.ReadDouble();
                Assert.AreEqual((double)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(short))
            {
                short actualOutput = endianAwareBinaryReader.ReadInt16();
                Assert.AreEqual((short)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(int))
            {
                int actualOutput = endianAwareBinaryReader.ReadInt32();
                Assert.AreEqual((int)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(long))
            {
                long actualOutput = endianAwareBinaryReader.ReadInt64();
                Assert.AreEqual((long)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(sbyte))
            {
                sbyte actualOutput = endianAwareBinaryReader.ReadSByte();
                Assert.AreEqual((sbyte)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(float))
            {
                float actualOutput = endianAwareBinaryReader.ReadSingle();
                Assert.AreEqual((float)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(ushort))
            {
                ushort actualOutput = endianAwareBinaryReader.ReadUInt16();
                Assert.AreEqual((ushort)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(uint))
            {
                uint actualOutput = endianAwareBinaryReader.ReadUInt32();
                Assert.AreEqual((uint)expectedOutput, actualOutput);
            }
            else if (expectedOutputType == typeof(ulong))
            {
                ulong actualOutput = endianAwareBinaryReader.ReadUInt64();
                Assert.AreEqual((ulong)expectedOutput, actualOutput);
            }
            else
            {
                Assert.Fail(string.Format("Unexpected type {0}", expectedOutputType));
            }
        }

        private LittleEndianBinaryReader CreateLittleEndianBinaryReader(Stream input, bool isLittleEndian)
        {
            ConstructorInfo constructorInfo = typeof(LittleEndianBinaryReader).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(Stream), typeof(Encoding), typeof(bool), typeof(bool) },
                null);
            object[] paramaters = new object[] { input, Encoding.UTF8, false, isLittleEndian };

            return (LittleEndianBinaryReader)constructorInfo.Invoke(paramaters);
        }

        private void VerifyEndianAwareBinaryReader<T>(byte[] input, T littleEndianOutput, T bigEndianOutput)
        {
            // Verify when reader is on a little endian machine
            LittleEndianBinaryReader littleEndianReaderOnLittleEndianMachine = this.CreateLittleEndianBinaryReader(new MemoryStream(input), true);
            this.CompareActualOutput(littleEndianReaderOnLittleEndianMachine, littleEndianOutput);

            // Verify when the reader is on a big endian machine
            LittleEndianBinaryReader littleEndianReaderOnBigEndianMachine = this.CreateLittleEndianBinaryReader(new MemoryStream(input), false);
            this.CompareActualOutput(littleEndianReaderOnBigEndianMachine, bigEndianOutput);
        }
    }
}
