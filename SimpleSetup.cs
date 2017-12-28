﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Server
{
    public class SimpleHTTPClient<T> : HTTPClient2
    {
        private T user_argument;
        private Dictionary<String, SimpleServer<T>.SimpleHTTPHandler> handlers;

        public SimpleHTTPClient(
            T user_argument,
            HTTPDecoder decoder,
            HTTPEncoder encoder,
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
        public override async Task HandleRequest2(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            try
            {
                Console.WriteLine($"url={request.url}");

                if (!this.handlers.ContainsKey(request.url_absolute))
                {
                    throw new NotImplementedException();
                }

                await this.handlers[request.url_absolute](this.user_argument, request, body, encoder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("==== EXCEPTION ====");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// Provides an easy to use and backward compatible way to create an HTTP or HTTPS server.
    /// </summary>
    public class SimpleServer<T>: IHTTPServerHandler
    {
        public delegate Task SimpleHTTPHandler(T user_argument, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder);

        private Dictionary<string, SimpleServer<T>.SimpleHTTPHandler> handlers;
        private T user_argument;

        private SimpleServer(
            T user_argument,
            Dictionary<string, SimpleHTTPHandler> handlers
        )
        {
            this.user_argument = user_argument;
            this.handlers = handlers;
        }

        public override HTTPClient CreateClient(HTTPDecoder decoder, HTTPEncoder encoder)
        {
            return new SimpleHTTPClient<T>(
                user_argument: user_argument,
                decoder: decoder,
                encoder: encoder,
                handlers: handlers
            );
        }

        public static Task Create<T>(
            T user_argument, 
            Dictionary<string, SimpleServer<T>.SimpleHTTPHandler> handlers,
            ushort port, 
            string ssl_cert_path, 
            string ssl_cert_pass
        )
        {
            var handler = new SimpleServer<T>(user_argument, handlers);
            var server = new HTTPServer<SimpleServer<T>>(handler, ssl_cert_path, ssl_cert_pass);
            return server.Start(port);
        }
    }
}
