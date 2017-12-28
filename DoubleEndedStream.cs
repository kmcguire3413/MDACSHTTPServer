//#define DOUBLE_ENDED_STREAM_DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public class DoubleEndedStream : Stream, IDisposable
    {
        class Chunk
        {
            public byte[] data;
            public int offset;
            public int actual;
        }

        AutoResetEvent rd;
        SemaphoreSlim wh;
        Queue<Chunk> chunks;
        long used;
        long pos;
        bool end_reached;

        public DoubleEndedStream()
        {
            chunks = new Queue<Chunk>();
            wh = new SemaphoreSlim(0);
            rd = new AutoResetEvent(false);
            pos = 0;
            used = 0;
            end_reached = false;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => used;

        public override long Position { get => pos; set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public long GetUsed() => used;

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(disposing);
                return;
            }

#if DOUBLE_ENDED_STREAM_DEBUG
            Console.WriteLine("{0}.Close: Closing stream.", this);
#endif

            Chunk chunk = new Chunk();
                
            chunk.data = null;
            chunk.actual = 0;
            chunk.offset = 0;

            lock (chunks)
            {
                chunks.Enqueue(chunk);
            }

            wh.Release();

            base.Dispose(disposing);
        }

        /// <summary>
        /// Asynchronously waits for a successful read to happen on this object then returns control.
        /// </summary>
        /// <returns></returns>
        public async Task WaitForReadAsync()
        {
            // Hopefully, this does not always spawn another thread, but I believe it will have too and I wish
            // a more efficient and true asynchronous way could be divised.
            await Task.Run(() =>
            {
                WaitForRead();
            });
        }

        /// <summary>
        /// Waits for a successful read to happen on this object then returns control.
        /// </summary>
        public void WaitForRead()
        {
            rd.WaitOne();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {

#if DOUBLE_ENDED_STREAM_DEBUG
            Console.WriteLine("{0}.Read({1}, {2}, {3})", this, buffer, offset, count);
#endif

            if (end_reached)
            {
                return 0;
            }
            
            wh.Wait();

#if DOUBLE_ENDED_STREAM_DEBUG
            Console.WriteLine("wh.Wait() completed; now locking chunks");
#endif

            lock (chunks)
            {

                if (chunks.Count < 1)
                {
#if DOUBLE_ENDED_STREAM_DEBUG
                    Console.WriteLine("{0}.Read: No chunks readable.", this);
#endif
                    return 0;
                }

                var chunk = chunks.Peek();

                rd.Set();

                if (chunk.data == null)
                {
#if DOUBLE_ENDED_STREAM_DEBUG
                    Console.WriteLine("{0}.Read: Stream closed on read.", this);
#endif
                    end_reached = true;
                    return 0;
                }

                if (count < chunk.actual)
                {
#if DOUBLE_ENDED_STREAM_DEBUG
                    Console.WriteLine("{0}.Read: Read amount less than current chunk. chunk.offset={1} chunk.actual={2}", this, chunk.offset, chunk.actual);
#endif
                    Array.Copy(chunk.data, chunk.offset, buffer, offset, count);

                    // Only take partial amount from the chunk.
                    chunk.offset += count;
                    chunk.actual -= count;

                    // Add another thread entry since this items was never actually removed.
                    wh.Release();

#if DOUBLE_ENDED_STREAM_DEBUG
                    Console.WriteLine($"chunk={chunk} chunk.offset={chunk.offset} chunk.actual={chunk.actual}");
#endif
                    used -= count;
                    pos += count;

                    return count;
                }
                else
                {
                    count = chunk.actual;
#if DOUBLE_ENDED_STREAM_DEBUG
                    Console.WriteLine("{0}.Read: Read amount equal or greater than current chunk.", this);
#endif
                    // Take the whole chunk, but no more to keep logic simple.
                    Array.Copy(chunk.data, chunk.offset, buffer, offset, count);
                    chunks.Dequeue();

                    used -= count;
                    pos += count;

                    return count;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Chunk chunk = new Chunk();

            chunk.data = new byte[count];

            Array.Copy(buffer, offset, chunk.data, 0, count);

            chunk.offset = 0;
            chunk.actual = count;

            lock (chunks)
            {
                used += count;
                chunks.Enqueue(chunk);
            }

#if DOUBLE_ENDED_STREAM_DEBUG
            Console.WriteLine("{0}.Write: Writing chunk of size {1} to stream.", this, count);
#endif

            wh.Release();
        }
    }
}
