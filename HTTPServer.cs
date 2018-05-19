using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MDACS.Server
{
    internal class Console
    {
        public static void WriteLine(string line)
        {
//#if DEBUG
//            System.Debug.WriteLine($"--->{line}");
//#endif
        }
    }

    public abstract class IHTTPServerHandler
    {
        public abstract IHTTPClient CreateClient(IHTTPDecoder decoder, IHTTPEncoder encoder); 
    }

    internal class HTTPServer<C> where C: IHTTPServerHandler
    {
        private String pfx_cert_path;
        private String cert_private_key_password;
        private C handler;

        public HTTPServer(C handler, String pfx_cert_path = null, String cert_private_key_password = null)
        {
            this.handler = handler;
            this.pfx_cert_path = pfx_cert_path;
            this.cert_private_key_password = cert_private_key_password;
        }

        public async Task Start(ushort port) {
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            listener.Start();

            X509Certificate2 x509 = null;

            if (pfx_cert_path != null)
            {
                x509 = new X509Certificate2(pfx_cert_path, cert_private_key_password);
            }

            while (true)
            {
                SslStream ssl_sock = null;

                var client = listener.AcceptTcpClient();
                var client_stream = client.GetStream();


                if (x509 != null)
                {
                    ssl_sock = new SslStream(client_stream, false);
                }

                Debug.WriteLine("Have new client.");

#pragma warning disable 4014
                Task.Run(async () =>
                {
                    try
                    {
                        HTTPDecoder http_decoder;
                        HTTPEncoder http_encoder;

                        if (ssl_sock != null)
                        {
                            Debug.WriteLine("Accepting SSL client.");
                            await ssl_sock.AuthenticateAsServerAsync(x509);

                            http_decoder = new HTTPDecoder(ssl_sock);
                            http_encoder = new HTTPEncoder(ssl_sock);
                        } else
                        {
                            Debug.WriteLine("Accepting client.");
                            http_decoder = new HTTPDecoder(client_stream);
                            http_encoder = new HTTPEncoder(client_stream);
                        }

                        var http_client = handler.CreateClient(http_decoder, http_encoder);

                        await http_client.Handle();

                        if (ssl_sock != null)
                        {
                            ssl_sock.Close();
                            ssl_sock.Dispose();
                        }

                        client.Close();
                        client.Dispose();
                    } catch (Exception e)
                    {
                        Console.WriteLine("Client Exception:");
                        Console.WriteLine($"Description: {e.ToString()}");
                        Console.WriteLine($"Stack:\n{e.StackTrace}");
                    }
                });
#pragma warning restore 4014
            }
        }
    }
}
