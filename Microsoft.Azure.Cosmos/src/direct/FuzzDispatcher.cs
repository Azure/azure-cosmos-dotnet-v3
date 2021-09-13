//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
#if DEBUG && !(NETSTANDARD15 || NETSTANDARD16)
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Class to fuzz TCP requests
    /// </summary>
    internal sealed class FuzzDispatcher : Dispatcher
    {
        private readonly bool sendFuzzedRequest;
        private readonly bool sendFuzzedContext;
        private readonly bool createFuzzLogFile;

        public FuzzDispatcher(Uri serverUri,
            UserAgentContainer userAgent,
            IConnectionStateListener connectionStateListener,
            string hostNameCertificateOverride,
            TimeSpan receiveHangDetectionTime,
            TimeSpan sendHangDetectionTime,
            TimerPool idleTimerPool,
            TimeSpan idleTimeout,
            bool _,
            bool fuzzRequest,
            bool fuzzContext)
            : base(serverUri,
                  userAgent,
                  connectionStateListener,
                  hostNameCertificateOverride,
                  receiveHangDetectionTime,
                  sendHangDetectionTime,
                  idleTimerPool,
                  idleTimeout,
                  true)
        {
            this.sendFuzzedRequest = fuzzRequest;
            this.sendFuzzedContext = fuzzContext;

            this.createFuzzLogFile = !string.IsNullOrEmpty(
                                Environment.GetEnvironmentVariable("FuzzLogLocation"));
        }

        //// build the context request and return a modified version
        //protected override byte[] BuildContextRequest(Guid activityId,
        //            UserAgentContainer userAgent,
        //            RntbdConstants.CallerId callerId,
        //            bool enableChannelMultiplexing)
        //{
        //    byte[] contextMessage = base.BuildContextRequest(activityId,
        //                                    userAgent,
        //                                    callerId);

        //    if (this.sendFuzzedContext)
        //    {
        //        byte[] fuzzedMessage = this.FuzzMessageBytes(new ArraySegment<byte>(contextMessage));

        //        if (this.createFuzzLogFile)
        //        {
        //            FuzzDispatcher.LogFuzzInfomation(new ArraySegment<byte>(contextMessage));
        //            FuzzDispatcher.LogFuzzInfomation(new ArraySegment<byte>(fuzzedMessage));
        //        }

        //        return fuzzedMessage;
        //    }

        //    return contextMessage;
        //}

        // build the document service request and return a modified version 
        protected override BufferProvider.DisposableBuffer BuildRequest(
                    DocumentServiceRequest request,
                    string pathAndQuery,
                    ResourceOperation resourceOperation,
                    Guid activityId,
                    BufferProvider bufferProvider,
                    out int headerSize,
                    out int bodySize)
        {
            BufferProvider.DisposableBuffer requestMessage = base.BuildRequest(
                            request,
                            pathAndQuery,
                            resourceOperation,
                            activityId,
                            bufferProvider,
                            out headerSize,
                            out bodySize);

            if (this.sendFuzzedRequest)
            {
                using (requestMessage)
                {
                    byte[] fuzzedMessage = this.FuzzMessageBytes(requestMessage.Buffer);

                    // only used for tracing. Modified to give somewhat reasonable output, but the whole message is fuzzed, so
                    // it's impossible to give a really correct answer.
                    headerSize = 0;
                    bodySize = fuzzedMessage.Length;

                    if (this.createFuzzLogFile)
                    {
                        FuzzDispatcher.LogFuzzInfomation(requestMessage.Buffer);
                        FuzzDispatcher.LogFuzzInfomation(new ArraySegment<byte>(fuzzedMessage));
                    }

                    return new BufferProvider.DisposableBuffer(fuzzedMessage);
                }
            }

            return requestMessage;
        }

        /// <summary>
        /// Get the path to the log file
        /// </summary>
        /// <returns>the path to the log file</returns>
        private static string GetFuzzLogPath()
        {
            // note that this method assumes that valid directory path is provided in App.config, and the file for this log have been created
            // by the fuzz test with name fuzzOutput_TestCaseIndex_TestCaseName_LogFileIndex.txt
            string fuzzLogLocation = Environment.GetEnvironmentVariable("FuzzLogLocation");
            DirectoryInfo di = new DirectoryInfo(fuzzLogLocation);
            FileInfo[] logFiles = di.GetFiles("fuzzOutput_*.txt");
            string logFileName = logFiles[logFiles.Length - 1].Name;
            return fuzzLogLocation + "\\" + logFileName;
        }

        /// <summary>
        /// Write the bytes and the corresponding ascii codes in log file, note that this method assumes that valid directory path is provided in App.config
        /// </summary>
        /// <param name="bytes">the bytes to write</param>
        private static void LogFuzzInfomation(ArraySegment<byte> bytes)
        {
            // log bytes(characters) in log file
            string filePath = FuzzDispatcher.GetFuzzLogPath();

            try
            {
                using (FileStream fileStream = File.Open(filePath, FileMode.Append))
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    writer.Write(bytes.Array, bytes.Offset, bytes.Count);
                    writer.Flush();
                }
            }
            catch (Exception)
            {
                throw;
            }

            // log ASCII codes of the bytes in log file
            StringBuilder sb = (new StringBuilder()).AppendLine();
            foreach (byte b in bytes)
            {
                string s = b.ToString(CultureInfo.CurrentCulture);
                sb.Append(s.PadLeft(4, ' '));
            }

            FuzzDispatcher.LogFuzzInfomation(sb.AppendLine().ToString());
        }

        /// <summary>
        /// Write given string in log file, note that this method assumes that valid directory path is provided in App.config
        /// </summary>
        /// <param name="s">the string to write</param>
        private static void LogFuzzInfomation(string s)
        {
            string filePath = FuzzDispatcher.GetFuzzLogPath();
            using (StreamWriter outFile = new StreamWriter(path: filePath, true))
            {
                outFile.WriteLine(s);
            }
        }

        /// <summary>
        /// Return a modifed copy of the given original byte array
        /// </summary>
        /// <param name="original">the original bytes</param>
        /// <returns>the modified bytes</returns>
        private byte[] FuzzMessageBytes(ArraySegment<byte> original)
        {
            byte[] modified = null;
            Random randomizer = new Random();
            const int fuzzLogicCountMaxValue = 4;
            string log = string.Empty;
            switch (randomizer.Next(fuzzLogicCountMaxValue))
            {
                case 0:
                    // insert a few bytes
                    byte[] bytesToInsert = new byte[randomizer.Next(1, 100)];
                    randomizer.NextBytes(bytesToInsert);
                    modified = new byte[original.Count + bytesToInsert.Length];
                    int indexToInsertBytes = randomizer.Next(original.Count);

                    log = string.Format(CultureInfo.CurrentCulture, "Insert {0} bytes at position {1}", bytesToInsert.Length, indexToInsertBytes);
                    Buffer.BlockCopy(original.Array, 0, modified, 0, indexToInsertBytes);
                    Buffer.BlockCopy(bytesToInsert, 0, modified, indexToInsertBytes, bytesToInsert.Length);
                    Buffer.BlockCopy(original.Array, indexToInsertBytes, modified, indexToInsertBytes + bytesToInsert.Length, original.Count - indexToInsertBytes);
                    break;
                case 1:
                    // modify a few bytes
                    byte[] newBytes = new byte[randomizer.Next(1, 10)];
                    randomizer.NextBytes(newBytes);
                    int indexToModifyBytes = randomizer.Next(original.Count - newBytes.Length);

                    log = string.Format(CultureInfo.CurrentCulture, "Replace {0} bytes at position {1}", newBytes.Length, indexToModifyBytes);
                    modified = new byte[original.Count];
                    Array.Copy(original.Array, 0, modified, 0, original.Count);
                    Buffer.BlockCopy(newBytes, 0, modified, indexToModifyBytes, newBytes.Length);
                    break;
                case 2:
                    // remove a few bytes
                    int indexToRemoveBytes = randomizer.Next(1, original.Count);
                    int numberOfBytesToRemove = randomizer.Next(1, original.Count - indexToRemoveBytes);

                    log = string.Format(CultureInfo.CurrentCulture, "Remove {0} bytes at position {1}", numberOfBytesToRemove, indexToRemoveBytes);
                    modified = new byte[original.Count - numberOfBytesToRemove];
                    Buffer.BlockCopy(original.Array, 0, modified, 0, indexToRemoveBytes);
                    Buffer.BlockCopy(original.Array, indexToRemoveBytes + numberOfBytesToRemove, modified, indexToRemoveBytes, original.Count - indexToRemoveBytes - numberOfBytesToRemove);
                    break;
                default:
                    // reverse a few bytes
                    int indexToReverseBytes = randomizer.Next(original.Count - 1);
                    int numberOfBytesToReverse = randomizer.Next(2, original.Count - indexToReverseBytes);

                    log = string.Format(CultureInfo.CurrentCulture, "Reverse {0} bytes at position {1}", numberOfBytesToReverse, indexToReverseBytes);
                    modified = new byte[original.Count];
                    Array.Copy(original.Array, 0, modified, 0, original.Count);
                    Array.Reverse(modified, indexToReverseBytes, numberOfBytesToReverse);
                    break;
            }

            if (this.createFuzzLogFile)
            {
                FuzzDispatcher.LogFuzzInfomation(log);
            }

            return modified;
        }
    }
#endif
}
