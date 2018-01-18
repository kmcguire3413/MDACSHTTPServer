# Example Code

```
using MDACS.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MDACS.Server.HTTPClient2;

namespace MDACSUniversalRegistry
{
    static class HandleSomething
    {
        public static async Task<Task> Action1(Object shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk("It works!");

            return Task.CompletedTask;
        }

        public static async Task<Task> Action2(Object shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            await encoder.WriteQuickHeader(200, "OK");

            var mystream = new DoubleEndedStream();

            // This is scheduled to run (without waiting on it), hence, the missing await.
            encoder.BodyWriteStream(mystream);

            await Task.Delay(5000);

            var something = "It works using the double ended stream!";
            var something_bytes = Encoding.UTF8.GetBytes(something);

            // The double ended stream can be used similarly to a stream.
            await mystream.WriteAsync(something_bytes, 0, something_bytes.Length);

            mystream.Dispose();

            // The client will get the response before this exists. It is waiting asynchronously.
            await Task.Delay(5000);

            return Task.CompletedTask;
        }

        public static async Task<Task> Action3(Object shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            await encoder.WriteQuickHeader(200, "OK");
            // Oppps.. I forgot to send the response, will it work? What will it do?

            return Task.CompletedTask;
        }

        public static async Task<Task> Action4(Object shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            await encoder.WriteQuickHeader(200, "OK");

            var mystream = new DoubleEndedStream();

            // This is scheduled to run (without waiting on it), hence, the missing await.
            encoder.BodyWriteStream(mystream);

            await Task.Yield();

            var something = "It works using the double ended stream!";
            var something_bytes = Encoding.UTF8.GetBytes(something);

            await mystream.WriteAsync(something_bytes, 0, something_bytes.Length);

            await Task.Yield();

            // We forgot to call dispose which is bad, but it should be closed for us.

            return Task.CompletedTask;

        }

        public static async Task<Task> Action5(Object shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            await encoder.WriteQuickHeader(200, "OK");

            var mystream = new DoubleEndedStream();

            // This is just one level more complicated that it looks. There is actually a return
            // value of Task<Task>. The first task consists of an ansynchronous blocking wait which
            // once complete creates a task and returns it, hence a Task as the result of a Task.
            var stream_copier = await encoder.BodyWriteStream(mystream);
            // The `stream_copier` is a new task (child) that works to copy from `mystream` out to
            // the transport/client/connection. If we abruptly exit without return it as a child task
            // or waiting on it then the system can, due to a race condition, think that we terminated
            // and it will try to send a response for us because it might detect no response having
            // been started.

            await Task.Yield();

            var something = "It works using the double ended stream!";
            var something_bytes = Encoding.UTF8.GetBytes(something);

            // This time we need another task to do something but we can not return like we
            // have before or the calling logic will think we are done. The way to do it is
            // to return a new Task object which will be awaited on by the calling code.

            var a_child_task = Task.Run(async () =>
            {
                await mystream.WriteAsync(something_bytes, 0, something_bytes.Length);
                mystream.Dispose();
            });

            // We forgot to call dispose which is bad, but it should be closed for us... however,
            // the system will try to detect this and call it implicitly.

            // Since we were using a stream we _do_ need to wait on it, but we also have a child task
            // so we need to wait on that child task too. Interestingly, in this specific case we could
            // omit the `a_child_task` since `stream_copier` will never complete until `a_child_task` calls
            // `Dispose` to close the double ended stream. But, for completeness we will await them both.
            return Task.WhenAll(stream_copier, a_child_task);

        }
    }

    class ServerState
    {

    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var handlers = new Dictionary<string, SimpleServer<ServerState>.SimpleHTTPHandler>();

            handlers.Add("/", HandleSomething.Action1);
            handlers.Add("/2", HandleSomething.Action2);
            handlers.Add("/3", HandleSomething.Action3);
            handlers.Add("/4", HandleSomething.Action4);
            handlers.Add("/5", HandleSomething.Action5);

            var server_state = new ServerState();

            var server_task = SimpleServer<ServerState>.Create(
                server_state, 
                handlers, 
                8080,
                null, //"test.pfx", 
                null //"hello"
            );

            server_task.Wait();

            // To build a PFX file from, for example, a Let's Encrypt set of files.
            // openssl crl2pkcs7 -nocrl -inkey privkey.pem -certfile fullchain.pem -out test.p7b
            // openssl pkcs7 -print_certs -in test.p7b -out test.cer
            // openssl pkcs12 -export -in test.cer -inkey privkey.pem -out test.pfx -nodes
        }
    }
}
```
