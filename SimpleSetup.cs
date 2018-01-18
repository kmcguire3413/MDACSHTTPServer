using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Server
{
    public static class Util
    {
        /// <summary>
        /// Reads a stream asynchronously providing both a maximum read amount and an intervaled amount to
        /// do an asynchronous `Task.Yield` at. Finally, treats the data as UTF8 JSON encoded and returns an
        /// object deserialized using a Newtonsoft.Json compatible decoder.
        /// </summary>
        /// <typeparam name="T">The JSON representing type.</typeparam>
        /// <param name="s">The stream.</param>
        /// <param name="max_amount"></param>
        /// <param name="yield_amount"></param>
        /// <returns>Returns null if read was aborted or the object if successful.</returns>
        public static async Task<T> ReadJsonObjectFromStreamAsync<T>(Stream s, long max_amount, long yield_amount = 1024 * 1024)
        {
            var (aborted, data_bytes) = await ReadStreamUntilEndAsync(s, max_amount, yield_amount);

            if (aborted)
            {
                return default(T);
            }

            var data_utf8 = Encoding.UTF8.GetString(data_bytes);

            return JsonConvert.DeserializeObject<T>(data_utf8);
        }

        /// <summary>
        /// Reads a stream asynchronously and collects all data but enforces an upper limit on the amount
        /// of data to read from the stream before the method exits.
        /// </summary>
        /// <param name="s">The stream.</param>
        /// <param name="max_amount">The method forcefully returns once it has read this many bytes.</param>
        /// <param name="yield_amount">The number of bytes to read before a forced `Task.Yield` is used.</param>
        /// <returns>A tuple with a bool indicating a forced exit and a task object containing a byte array.</returns>
        public static async Task<(bool, byte[])> ReadStreamUntilEndAsync(Stream s, long max_amount, long yield_amount = 1024 * 1024)
        {
            var mb = new MemoryStream();
            var buf = new byte[1024];
            int cnt = 0;
            long amount = 0;
            long amount_total = 0;

            while ((cnt = await s.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                amount += cnt;
                amount_total += cnt;

                await mb.WriteAsync(buf, 0, cnt);

                if (amount_total > max_amount)
                {
                    return (true, mb.GetBuffer());
                }

                if (amount > yield_amount)
                {
                    amount = 0;
                    await Task.Yield();
                }
            }

            return (false, mb.GetBuffer());
        }

        /// <summary>
        /// Reads a stream asynchronously and discards all data from the stream. Also, provides the ability to
        /// perform an asynchronous yield each time so many bytes are read, thus, preventing starvation of other
        /// tasks if the stream never ends.
        /// </summary>
        /// <param name="s">The stream.</param>
        /// <param name="yield_amount">The number of bytes to read before a forced `Task.Yield` is used.</param>
        /// <returns>A task object.</returns>
        public static async Task ReadStreamUntilEndAndDiscardDataAsync(Stream s, long yield_amount = 1024 * 1024)
        {
            var buf = new byte[1024];
            int cnt = 0;
            long amount = 0;

            while ((cnt = await s.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                amount += cnt;

                if (amount > yield_amount)
                {
                    amount = 0;
                    await Task.Yield();
                }
            }
        }
    }

    class SimpleHTTPClient<T> : HTTPClient2
    {
        private T user_argument;
        private Dictionary<String, SimpleServer<T>.SimpleHTTPHandler> handlers;

        public SimpleHTTPClient(
            T user_argument,
            IHTTPDecoder decoder,
            IHTTPEncoder encoder,
            Dictionary<String, SimpleServer<T>.SimpleHTTPHandler> handlers
        ) : base(decoder, encoder)
        {
            this.user_argument = user_argument;
            this.handlers = handlers;
        }

        /// <summary>
        /// The entry point for route handling. Provides common error response from exception propogation.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns>Asynchronous task object.</returns>
        public override async Task<Task> HandleRequest2(HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            try
            {
                Console.WriteLine($"url={request.url}");

                if (!this.handlers.ContainsKey(request.url_absolute))
                {
                    await encoder.WriteQuickHeader(404, "Not Found");
                    await encoder.BodyWriteSingleChunk("The request resource is not avaliable.");

                    return Task.CompletedTask;
                }

                return await this.handlers[request.url_absolute](this.user_argument, request, body, encoder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("==== EXCEPTION ====");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);

                throw;
            }
        }
    }

    /// <summary>
    /// Provides an easy to use and backward compatible way to create an HTTP or HTTPS server.
    /// </summary>
    public class SimpleServer<UserArgumentType>: IHTTPServerHandler
    {
        public delegate Task<Task> SimpleHTTPHandler(UserArgumentType user_argument, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder);

        private Dictionary<string, SimpleServer<UserArgumentType>.SimpleHTTPHandler> handlers;
        private UserArgumentType user_argument;

        private SimpleServer(
            UserArgumentType user_argument,
            Dictionary<string, SimpleHTTPHandler> handlers
        )
        {
            this.user_argument = user_argument;
            this.handlers = handlers;
        }

        public override IHTTPClient CreateClient(IHTTPDecoder decoder, IHTTPEncoder encoder)
        {
            return new SimpleHTTPClient<UserArgumentType>(
                user_argument: user_argument,
                decoder: decoder,
                encoder: encoder,
                handlers: handlers
            );
        }

        public static Task Create(
            UserArgumentType user_argument, 
            Dictionary<string, SimpleServer<UserArgumentType>.SimpleHTTPHandler> handlers,
            ushort port, 
            string ssl_cert_path, 
            string ssl_cert_pass
        )
        {
            var handler = new SimpleServer<UserArgumentType>(user_argument, handlers);
            var server = new HTTPServer<SimpleServer<UserArgumentType>>(handler, ssl_cert_path, ssl_cert_pass);
            return server.Start(port);
        }
    }
}
