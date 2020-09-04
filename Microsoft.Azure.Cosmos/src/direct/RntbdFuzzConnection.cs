//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
#if DEBUG
    using System;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Class to fuzz TCP requests
    /// </summary>
    internal sealed class RntbdFuzzConnection : RntbdConnection
    {
        private bool sendFuzzedRequest;
        private bool sendFuzzedContext;
        private bool createFuzzLogFile;
        
        public RntbdFuzzConnection(
            Uri fullUri,
            int requestTimeoutInSeconds, 
            string overrideHostNameInCertificate, 
            int openTimeoutInSeconds, 
            int idleTimeoutInSeconds,
            string poolKey,
            bool fuzzRequest, 
            bool fuzzContext,
            UserAgentContainer userAgent,
             TimerPool pool)
            : base(fullUri, 
                  0.1, // override request timeout to 100 ms 
                  overrideHostNameInCertificate, 
                  1, // override open timeout to 1 second 
                  idleTimeoutInSeconds, 
                  poolKey, 
                  userAgent)
        {
            this.sendFuzzedRequest = fuzzRequest;
            this.sendFuzzedContext = fuzzContext;

            this.createFuzzLogFile = ConfigurationManager.AppSettings["FuzzLogLocation"].Length > 0;
        }

        // build the context request and return a modified version
        protected override byte[] BuildContextRequest(Guid activityId)
        {
            byte[] contextMessage = base.BuildContextRequest(activityId);
            if (this.sendFuzzedContext)
            {
                byte[] fuzzedMessage = this.FuzzMessageBytes(contextMessage);

                if (this.createFuzzLogFile)
                {
                    RntbdFuzzConnection.LogFuzzInfomation(contextMessage);
                    RntbdFuzzConnection.LogFuzzInfomation(fuzzedMessage);
                }

                return fuzzedMessage;
            }

            return contextMessage;
        }
              
        // build the document service request and return a modified version 
        protected override byte[] BuildRequest(
            DocumentServiceRequest request,
            string replicaPath,
            ResourceOperation resourceOperation,
            out int headerAndMetadataSize,
            out int bodySize,
            Guid activityId)
        {
            byte[] requestMessage = base.BuildRequest(request, replicaPath, resourceOperation, out headerAndMetadataSize, out bodySize, activityId);

            if (this.sendFuzzedRequest)
            {
                byte[] fuzzedMessage = this.FuzzMessageBytes(requestMessage);

                // only used for tracing. Modified to give somewhat reasonable output, but the whole message is fuzzed, so
                // it's impossible to give a really correct answer.
                headerAndMetadataSize = 0;
                bodySize = fuzzedMessage.Length;

                if (this.createFuzzLogFile)
                {
                    RntbdFuzzConnection.LogFuzzInfomation(requestMessage);
                    RntbdFuzzConnection.LogFuzzInfomation(fuzzedMessage);
                }

                return fuzzedMessage;
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
            string fuzzLogLocation = ConfigurationManager.AppSettings["FuzzLogLocation"];
            DirectoryInfo di = new DirectoryInfo(fuzzLogLocation);
            FileInfo[] logFiles = di.GetFiles("fuzzOutput_*.txt");
            string logFileName = logFiles[logFiles.Length - 1].Name;
            return fuzzLogLocation + "\\" + logFileName;
        }

        /// <summary>
        /// Write the bytes and the corresponding ascii codes in log file, note that this method assumes that valid directory path is provided in App.config
        /// </summary>
        /// <param name="bytes">the bytes to write</param>
        private static void LogFuzzInfomation(byte[] bytes)
        {
            // log bytes(characters) in log file
            string filePath = RntbdFuzzConnection.GetFuzzLogPath();

            try
            {
                using (FileStream fileStream = File.Open(filePath, FileMode.Append))
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    writer.Write(bytes);
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

            RntbdFuzzConnection.LogFuzzInfomation(sb.AppendLine().ToString());
        }

        /// <summary>
        /// Write given string in log file, note that this method assumes that valid directory path is provided in App.config
        /// </summary>
        /// <param name="s">the string to write</param>
        private static void LogFuzzInfomation(string s)
        {
            string filePath = RntbdFuzzConnection.GetFuzzLogPath();
            using (StreamWriter outFile = new StreamWriter(filePath, true))
            {
                outFile.WriteLine(s);
            }
        }

        /// <summary>
        /// Return a modifed copy of the given original byte array
        /// </summary>
        /// <param name="original">the original bytes</param>
        /// <returns>the modified bytes</returns>
        private byte[] FuzzMessageBytes(byte[] original)
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
                    modified = new byte[original.Length + bytesToInsert.Length];
                    int indexToInsertBytes = randomizer.Next(original.Length);

                    log = string.Format(CultureInfo.CurrentCulture, "Insert {0} bytes at position {1}", bytesToInsert.Length, indexToInsertBytes);
                    Buffer.BlockCopy(original, 0, modified, 0, indexToInsertBytes);
                    Buffer.BlockCopy(bytesToInsert, 0, modified, indexToInsertBytes, bytesToInsert.Length);
                    Buffer.BlockCopy(original, indexToInsertBytes, modified, indexToInsertBytes + bytesToInsert.Length, original.Length - indexToInsertBytes);
                    break;
                case 1:
                    // modify a few bytes
                    byte[] newBytes = new byte[randomizer.Next(1, 10)];
                    randomizer.NextBytes(newBytes);
                    int indexToModifyBytes = randomizer.Next(original.Length - newBytes.Length);

                    log = string.Format(CultureInfo.CurrentCulture, "Replace {0} bytes at position {1}", newBytes.Length, indexToModifyBytes);
                    modified = original.Clone() as byte[];
                    Buffer.BlockCopy(newBytes, 0, modified, indexToModifyBytes, newBytes.Length);
                    break;
                case 2:
                    // remove a few bytes
                    int indexToRemoveBytes = randomizer.Next(1, original.Length);
                    int numberOfBytesToRemove = randomizer.Next(1, original.Length - indexToRemoveBytes);

                    log = string.Format(CultureInfo.CurrentCulture, "Remove {0} bytes at position {1}", numberOfBytesToRemove, indexToRemoveBytes);
                    modified = new byte[original.Length - numberOfBytesToRemove];
                    Buffer.BlockCopy(original, 0, modified, 0, indexToRemoveBytes);
                    Buffer.BlockCopy(original, indexToRemoveBytes + numberOfBytesToRemove, modified, indexToRemoveBytes, original.Length - indexToRemoveBytes - numberOfBytesToRemove);
                    break;
                default:
                    // reverse a few bytes
                    int indexToReverseBytes = randomizer.Next(original.Length - 1);
                    int numberOfBytesToReverse = randomizer.Next(2, original.Length - indexToReverseBytes);

                    log = string.Format(CultureInfo.CurrentCulture, "Reverse {0} bytes at position {1}", numberOfBytesToReverse, indexToReverseBytes);
                    modified = original.Clone() as byte[];
                    Array.Reverse(modified, indexToReverseBytes, numberOfBytesToReverse);
                    break;
            }

            if (this.createFuzzLogFile)
            {
                RntbdFuzzConnection.LogFuzzInfomation(log);
            }

            return modified;
        }
    }
#endif
}
