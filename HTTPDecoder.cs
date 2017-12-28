using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    // 
    public class StreamReaderHelper
    {
        public Stream stream;
        public byte[] buf;
        public int ndx;

        public StreamReaderHelper(Stream stream, int maxbufsize)
        {
            this.stream = stream;
            this.buf = new byte[maxbufsize];
            this.ndx = 0;
        }

        public int BufSizeNow() => ndx;

        public async Task<int> ReadSome()
        {
            if (BufSpaceLeft() < 1)
            {
                throw new InvalidOperationException();
            }

            int cnt = 0;

            try
            {
                cnt = await stream.ReadAsync(buf, ndx, buf.Length - ndx);
            } catch (IOException ex)
            {
                throw new InvalidOperationException("I/O exception when trying to read", ex);
            }

            if (cnt < 1)
            {
                throw new InvalidOperationException("I/O read returned less than one byte");
            }

            ndx += cnt;

            return cnt;
        }

        public int BufSpaceLeft() => buf.Length - ndx;

        public void ShiftBufBack(int amount)
        {
            Array.Copy(buf, amount, buf, 0, ndx - amount);
            ndx -= amount;
        }

        public async Task<byte[]> ReadLine()
        {
            int k = 0;

            do
            {
                k = Array.IndexOf(buf, (byte)10, 0, ndx);

                if (k < 0)
                    await ReadSome();
            } while (k < 0);

            var ret = new byte[k];

            Array.Copy(buf, 0, ret, 0, k);

            // Get line and the \n at the end (plus one below).
            ShiftBufBack(k + 1);

            return ret;
        }

        public async Task<byte[]> ReadSpecificSize(int size)
        {
            var tmp = new byte[size];
            int got = 0;

            while (got < size)
            {
                if (BufSizeNow() < 1)
                    await ReadSome();

                if (got + ndx > size)
                {
                    // Take only part of intermediate buffer.
                    var only = size - got;

                    Array.Copy(buf, 0, tmp, got, only);

                    got += only;

                    ShiftBufBack(only);
                } else
                {
                    // Take entire intermediate buffer.
                    Array.Copy(buf, 0, tmp, got, ndx);
                    got += ndx;
                    ndx = 0;
                }
            }

            return tmp;
        }
    }

    public class HTTPDecoderBodyType
    {
        public enum MyType
        {
            NoBody,
            ChunkedEncoding,
            ContentLength,
        }

        public MyType type;
        public long size;

        private HTTPDecoderBodyType(MyType type, long size)
        {
            this.type = type;
            this.size = size;
        }

        public static HTTPDecoderBodyType NoBody()
        {
            return new HTTPDecoderBodyType(MyType.NoBody, 0);
        }

        public static HTTPDecoderBodyType ChunkedEncoding()
        {
            return new HTTPDecoderBodyType(MyType.ChunkedEncoding, 0);
        }

        public static HTTPDecoderBodyType ContentLength(long size)
        {
            return new HTTPDecoderBodyType(MyType.ContentLength, size);
        }
    }

    public class HTTPDecoder
    {
        private Stream s;
        private StreamReaderHelper s_helper;

        public HTTPDecoder(Stream s)
        {
            this.s = s;
            this.s_helper = new StreamReaderHelper(s, 1024 * 16);
        }

        /// <summary>
        /// Returns null if the connection is lost, otherwise, the header is returned as a list of strings for each line of the header.
        /// </summary>
        /// <returns>Null if connection is lost or list of strings representing the lines of the header.</returns>
        public async Task<List<String>> ReadHeader()
        {
            var header = new List<String>();
            String line_utf8_str;

            do
            {
                var line = await s_helper.ReadLine();

                line_utf8_str = Encoding.UTF8.GetString(line).TrimEnd();

                if (line_utf8_str.Length > 0)
                {
                    Console.WriteLine("line_utf8_str={0}", line_utf8_str);
                    header.Add(line_utf8_str);
                }
            } while (line_utf8_str.Length > 0);

            return header;
        }

        public async Task<(Stream, Task)> ReadBody(HTTPDecoderBodyType body_type)
        {
            var os = new DoubleEndedStream();
            Task spawned_task;

            const long max_buffer = 1024 * 1024 * 32;

            switch (body_type.type)
            {
                case HTTPDecoderBodyType.MyType.NoBody:
                    os.Dispose();
                    return (os as Stream, null);
                case HTTPDecoderBodyType.MyType.ChunkedEncoding:
#pragma warning disable 4014
                    spawned_task = Task.Run(async () =>
                    {
                        do
                        {
                            var line_bytes = await s_helper.ReadLine();
                            var line = Encoding.UTF8.GetString(line_bytes).TrimEnd();

                            Console.WriteLine("HTTPDecoder.ChunkedDecoder: line={0}", line);

                            if (line.Length == 0)
                            {
                                continue;
                            }

                            Console.WriteLine("AAA");
                            int chunk_size = Convert.ToInt32(line, 16);
                            Console.WriteLine("BBB");

                            if (chunk_size == 0)
                            {
                                os.Dispose();
                                await s_helper.ReadLine();
                                break;
                            }

                            Console.WriteLine("CCC");

                            // Wait without polling for the buffer to decrease from reading from it.
                            while (os.GetUsed() + chunk_size > max_buffer)
                            {
                                Console.WriteLine("AAA");
                                await os.WaitForReadAsync();
                                Console.WriteLine("BBB");
                            }

                            var chunk = await s_helper.ReadSpecificSize(chunk_size);
                            os.Write(chunk, 0, chunk.Length);
                        } while (true);
                        Console.WriteLine("chunked stream done");
                    });
#pragma warning restore 4014
                    return (os, spawned_task);
                case HTTPDecoderBodyType.MyType.ContentLength:
#pragma warning disable 4014
                    spawned_task = Task.Run(async () =>
                    {
                        Console.WriteLine("reading content-length body");

                        long got = 0;
                        int amount;
                        const int chunksize = 4096;

                        do
                        {
                            if (body_type.size - got > 0x7fffffff)
                            {
                                amount = chunksize;
                            }
                            else
                            {
                                amount = Math.Min(chunksize, (int)(body_type.size - got));
                            }

                            // Wait without polling for the buffer to decrease from reading from it.
                            while (os.GetUsed() + amount > max_buffer)
                            {
                                await os.WaitForReadAsync();
                            }

                            var chunk = await s_helper.ReadSpecificSize(amount);

                            got += chunk.Length;

                            os.Write(chunk, 0, chunk.Length);
                        } while (got < body_type.size);

                        os.Dispose();
                    });
#pragma warning restore 4014
                    return (os, spawned_task);
                default:
                    os.Dispose();
                    return (os, null);
            }
        }
    }
}
