namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class contains utility methods for creation of TCP connections for testing purposes.
    /// </summary>
    public static class ConnectionTestUtils
    {
        public static X509Certificate2 CreateTestCertificate()
        {
            using RSA key = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        public static async Task StartProcessingConnectionAsync(
            TcpListener listener,
            X509Certificate2 certificate,
            string testText,
            CancellationToken cancellationToken)
        {
            try
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                using SslStream sslStream = new(client.GetStream(), leaveInnerStreamOpen: false);
                await sslStream.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.None,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    },
                    cancellationToken);
                byte[] message = Encoding.UTF8.GetBytes(testText);
                await sslStream.WriteAsync(BitConverter.GetBytes(message.Length), 0, sizeof(int), cancellationToken);
                await sslStream.WriteAsync(message, 0, message.Length, cancellationToken);

                // Keep the peer open for health checks until the test finishes.
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The test cancels both the accepted connection and the unused listener.
            }
        }

        public static TcpListener BuildTcpListener(
            out int port,
            out TcpListener tcpListener,
            out TcpListener ipV6TcpListener)
        {
            port = 15000;
            tcpListener = null;
            ipV6TcpListener = null;

            while (port < 15100)
            {
                try
                {
                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Loopback, port);
                    tcpListener = new TcpListener(ipEndPoint);
                    IPEndPoint ipV6EndPoint = new IPEndPoint(IPAddress.IPv6Loopback, port);
                    ipV6TcpListener = new TcpListener(ipV6EndPoint);
                    tcpListener.Start();
                    ipV6TcpListener.Start();
                    break;
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    tcpListener?.Stop();
                    ipV6TcpListener?.Stop();
                }

                port++;
            }

            if (port == 15100)
            {
                throw new Exception($"Failed to bind to a local port in range 15000 .. 15100");
            }

            return tcpListener;
        }

        public class TestMemoryStream : MemoryStream
        {

        }

        public class TestMemoryStreamPool : MemoryStreamPool
        {
            private bool generateStream;

            public TestMemoryStreamPool(bool generateStream)
            {
                this.generateStream = generateStream;
            }

            public bool TryGetMemoryStream(int capacity, out MemoryStream memoryStream)
            {
                if (this.generateStream)
                {
                    memoryStream = new TestMemoryStream();
                    return true;
                }
                else
                {
                    memoryStream = null;
                    return false;
                }
            }
        }

        public static bool SelfSignedForLocalhost(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}