#define PROXY_HTTP_ENCODER_DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public class ProxyHTTPEncoder
    {
        public HTTPEncoder encoder;
        public AsyncManualResetEvent ready;
        public AsyncManualResetEvent done;
        public bool close_connection;

        public ProxyHTTPEncoder(HTTPEncoder encoder, bool close_connection)
        {
            this.encoder = encoder;
            this.ready = new AsyncManualResetEvent();
            this.done = new AsyncManualResetEvent();
            this.close_connection = close_connection;
        }

        public async Task Death()
        {

        }

        public async Task WriteQuickHeader(int code, String text)
        {
            var header = new Dictionary<String, String>();

            header.Add("$response_code", code.ToString());
            header.Add("$response_text", text);

            await WriteHeader(header);
        }

        /// <summary>
        /// Write the HTTP headers to the remote endpoint. The actual writing of the headers may or may
        /// not be delayed - depending on the implementation. The headers are likely to be sent once some
        /// response data has been written.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <returns></returns>
        public async Task WriteHeader(Dictionary<String, String> header)
        {
            if (close_connection)
            {
                if (header.ContainsKey("connection"))
                {
                    header["connection"] = "close";
                }
                else
                {
                    header.Add("connection", "close");
                }
            }
            else
            {
                if (header.ContainsKey("connection"))
                {
                    header["connection"] = "keep-alive";
                }
                else
                {
                    header.Add("connection", "keep-alive");
                }
            }

            Console.WriteLine("!!! waiting on ready");
            await this.ready.WaitAsync();
            Console.WriteLine("!!! ready was good");
            await encoder.WriteHeader(header);
        }

        public async Task BodyWriteSingleChunk(String chunk)
        {
            byte[] chunk_bytes = Encoding.UTF8.GetBytes(chunk);
            await BodyWriteSingleChunk(chunk_bytes, 0, chunk_bytes.Length);
        }

        /// <summary>
        /// This will send a single chunk and use the content-length field of the HTTP response.
        /// </summary>
        /// <param name="chunk">The data to send.</param>
        /// <param name="offset">The offset within the data array.</param>
        /// <param name="length">The length of the chunk within the data array starting at the offset specified.</param>
        /// <returns></returns>
        public async Task BodyWriteSingleChunk(byte[] chunk, int offset, int length)
        {
            await this.ready.WaitAsync();
            await this.encoder.BodyWriteSingleChunk(chunk, offset, length);
            this.done.Set();
        }

        private async Task BodyWriteStreamInternal(Stream inpstream)
        {
            byte[] buf = new byte[1024 * 4];
            bool first_chunk = true;

#if PROXY_HTTP_ENCODER_DEBUG
            Console.WriteLine("{0}.BodyWriteStreamInternal: Now running.", this);
#endif

            do
            {
#if PROXY_HTTP_ENCODER_DEBUG
                Console.WriteLine("{0}.BodyWriteStreamInternal: Doing ReadAsync on stream.", this);
#endif
                var cnt = await inpstream.ReadAsync(buf, 0, buf.Length);

#if PROXY_HTTP_ENCODER_DEBUG
                Console.WriteLine("{0}.BodyWriteStreamInternal: Read cnt={1} buf={2} buf.Length={3}", this, cnt, buf, buf.Length);
#endif

                if (cnt < 1)
                {
#if PROXY_HTTP_ENCODER_DEBUG
                    Console.WriteLine("{0}.BodyWriteStreamInternal: End of stream.", this);
#endif
                    break;
                }

                if (first_chunk)
                {
#if PROXY_HTTP_ENCODER_DEBUG
                    Console.WriteLine("{0}.BodyWriteStreamInternal: First chunk.", this);
#endif
                    await this.encoder.BodyWriteFirstChunk(buf, 0, cnt);
                    first_chunk = false;
                }
                else
                {
#if PROXY_HTTP_ENCODER_DEBUG
                    Console.WriteLine("{0}.BodyWriteStreamInternal: Next chunk.", this);
#endif

                    await this.encoder.BodyWriteNextChunk(buf, 0, cnt);
                }
            } while (true);

            await this.encoder.BodyWriteNoChunk();

            this.done.Set();
        }

        /// <summary>
        /// This will spawn an asynchronous task which will continually read from the stream until
        /// it reaches the end. Each chunk of data read from the stream will be send as a chunk of
        /// a transfer-encoding chunked response.
        /// </summary>
        /// <param name="inpstream">The stream to read chunks from.</param>
        /// <returns></returns>
        public async Task<Task> BodyWriteStream(Stream inpstream)
        {
#if PROXY_HTTP_ENCODER_DEBUG
            Console.WriteLine("{0}.BodyWriteStream: Starting task to copy from stream into the real encoder.", this);
#endif
            await this.ready.WaitAsync();

            // Control needs to return to the caller. Do not `await` the result of this task.
            var runner = Task.Run(async () => {
                await BodyWriteStreamInternal(inpstream);
            });

            return runner;
        }
    }

    public class HTTPClient
    {
        private HTTPDecoder decoder;
        private HTTPEncoder encoder;

        public HTTPClient(HTTPDecoder decoder, HTTPEncoder encoder)
        {
            this.decoder = decoder;
            this.encoder = encoder;
        }

        private HTTPClient()
        {

        }

        private Dictionary<String, String> LineHeaderToDictionary(List<String> line_header)
        {
            var tmp = new Dictionary<String, String>();

            foreach (var line in line_header)
            {
                var sep_ndx = line.IndexOf(':');

                if (sep_ndx > -1)
                {
                    var key = line.Substring(0, sep_ndx).Trim();
                    var value = line.Substring(sep_ndx + 1).Trim();

                    tmp.Add(key.ToLower(), value.ToLower());
                } else
                {
                    var space_ndx_0 = line.IndexOf(' ');
                    var space_ndx_1 = line.IndexOf(' ', space_ndx_0);

                    if (space_ndx_0 > -1 && space_ndx_1 > -1)
                    {
                        var parts = line.Split(' ');

                        tmp["$method"] = parts[0];
                        tmp["$url"] = parts[1];
                        tmp["$version"] = parts[2];
                    }
                }
            }

            return tmp;
        }

        /// <summary>
        /// The function responsible for handling each request. This function can be implemented by subclassing
        /// of this class and using override. The function is provided with the request header, request body, and
        /// the response encoder which allows setting of headers and data if any.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
        public virtual async Task HandleRequest(Dictionary<String, String> header, Stream body, ProxyHTTPEncoder encoder)
        {
            var outheader = new Dictionary<String, String>();

            outheader.Add("$response_code", "200");
            outheader.Add("$response_text", "OK");

            Console.WriteLine("Sending response header now.");

            await encoder.WriteHeader(outheader);

            Console.WriteLine("Sending response body now.");

            MemoryStream ms = new MemoryStream();

            byte[] something = Encoding.UTF8.GetBytes("hello world\n");

            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);

            ms.Position = 0;

            //await encoder.BodyWriteSingleChunk("response test body");
            await encoder.BodyWriteStream(ms);

            Console.WriteLine("Response has been sent.");
        }

        public async Task Handle()
        {
            var q = new Queue<ProxyHTTPEncoder>();

            var qchanged = new SemaphoreSlim(0);

            // TODO,BUG: Do these need some kind of volatile marking? How to mark them as such?
            bool runner_exit = false;
            bool runner_abort = false;

            // This task watches the `q` queue and starts and removes
            // proxy objects representing the HTTP encoder. Each request
            // gets its own proxy object and all methods on the proxy either
            // block or buffer until the proxy object becomes ready.
#pragma warning disable 4014
            var runner_task = Task.Run(async () =>
            {
                while (true)
                {
                    ProxyHTTPEncoder phe;

                    Console.WriteLine("waiting on qchanged");
                    await qchanged.WaitAsync();

                    // Only lock long enough to get the first item.
                    lock (q)
                    {
                        if (runner_abort)
                        {
                            Console.WriteLine("runner has aborted");
                            return;
                        }

                        if (runner_exit && q.Count == 0)
                        {
                            Console.WriteLine("runner has exited");
                            return;
                        }

                        phe = q.Peek();
                    }

                    if (phe == null)
                    {
                        // The exit signal.
                        Console.WriteLine("peeked null; now exiting");
                        break;
                    }

                    // Signal this object that it is ready to do work.
                    phe.ready.Set();
                    Console.WriteLine("signaling phe as ready");

                    // Wait until it is done.
                    await phe.done.WaitAsync();

                    Console.WriteLine("phe is done");

                    phe.Death();

                    // Remove it, and signal the next to go.
                    q.Dequeue();
                    Console.WriteLine("phe dequeued");
                }
            });
#pragma warning restore 4014

            bool close_connection = false;

            while (!close_connection)
            {
                Console.WriteLine("###### Handling the next request. ######");
                // Need a way for this to block (await) until the body has been completely
                // read. This logic could be implemented inside the decoder.
                List<String> line_header;
                try
                {
                    line_header = await decoder.ReadHeader();
                }
                catch (InvalidOperationException ex)
                {
                    // This happens if the remote closes their transmit channel, yet, we must be careful not to
                    // exit since the remote receive channel may still be open.
                    break;
                }

                Console.WriteLine("Header to dictionary.");

                if (line_header == null)
                {
                    Console.WriteLine("Connection has been lost.");
                    break;
                }

                var header = LineHeaderToDictionary(line_header);

                Stream body;

                Console.WriteLine("Got header.");

                Task body_reading_task;

                if (header.ContainsKey("content-length"))
                {
                    Console.WriteLine("Got content-length.");
                    // Content length specified body follows.
                    long content_length = (long)Convert.ToUInt32(header["content-length"]);

                    (body, body_reading_task) = await decoder.ReadBody(HTTPDecoderBodyType.ContentLength(content_length));
                }
                else if (header.ContainsKey("transfer-encoding"))
                {
                    Console.WriteLine("Got chunked.");
                    // Chunked encoded body follows.
                    (body, body_reading_task) = await decoder.ReadBody(HTTPDecoderBodyType.ChunkedEncoding());
                }
                else
                {
                    Console.WriteLine("Got no body.");
                    // No body follows. (Not using await to allow pipelining.)
                    (body, body_reading_task) = await decoder.ReadBody(HTTPDecoderBodyType.NoBody());
                }

                if (header.ContainsKey("connection") && !header["connection"].ToLower().Equals("close"))
                {
                    close_connection = false;
                } else
                {
                    close_connection = true;
                }

                Console.WriteLine($"close_connection={close_connection}");

                var phe = new ProxyHTTPEncoder(encoder, close_connection);

                q.Enqueue(phe);

                qchanged.Release();

                Console.WriteLine("Allowing handling of request.");

                // A couple of scenarios are possible here.
                // (1) The request can complete and exit before the data it writes is actually sent.
                // (2) The request can complete after the response has been sent.
                // (3) If we do not await then we can service other requests (pipeline).
                //
                // To keep things more deterministic and less performance oriented
                // you can see that we await the request handler to complete before
                // moving onward.
                var handle_req_task = HandleRequest(header, body, phe);

                await handle_req_task;

                // Clever way to handle exceptions since otherwise things continue onward like
                // everything is normal. This can leave the runner stuck waiting at the `phe.done`
                // signal.
                if (handle_req_task.Exception != null)
                {
                    // We might should think of a way to salvage the connection.. but if the headers have
                    // been sent and we are in the middle of a content-length response then I am not sure
                    // how to gracefully let the client know that things have gone wrong without shutting
                    // the connection down. It is possible to detect if its a chunked-encoding and then use
                    // a trailer header, maybe? Pretty much like a CGI script?
                    Console.WriteLine("exception in request handler; shutting down connection");
                    // Ensure the proxy is marked as done. So the runner will throw it away, and also
                    // drop the connection because we are not sure how to proceed now.
                    runner_abort = true;
                    // The runner is likely waiting on the `done` signal. Set the signal to ensure it
                    // gets past that point and then sees the `runner_abort`.
                    phe.done.Set();
                    // Come out of this loop and wait on runner to complete.
                    break;
                }

                // We must wait until both the HandleRequest returns and
                // that the task, if any, which is reading the body also
                // completes.
                //
                // This should not usually happen but it is possible and has happened. For some reason,
                // the handler exits, yet the body is still being read, therefore, we really also need
                // to sort of dump the data here or take a Task that is returned by HandleRequest so
                // we know when all possible handlers are dead.
                if (body_reading_task != null)
                {
                    // BUG, TODO: unsufficient... will deadlock if nothing is reading the `body` as the buffer may fill
                    //       for the body
                    // Do not block while waiting.
                    await body_reading_task;

                    if (body_reading_task.Exception != null)
                    {
                        Console.WriteLine("exception in body reader; shutting down connection");
                        Console.WriteLine(body_reading_task.Exception);
                        runner_abort = true;
                        phe.done.Set();
                    }
                }
            }

            Console.WriteLine("httpclient handler trying to exit; once runner has exited");
            // Signal the runner that it is time to exit.
            runner_exit = true;
            // Ensure the runner can continue.
            qchanged.Release();
            runner_task.Wait();

            Console.WriteLine("done waiting on runner; now exiting handler");

            // The stream is (expected to be) closed once this method is exited.
        }
    }
}
