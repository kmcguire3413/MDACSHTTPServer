//#define DOUBLE_ENDED_STREAM_DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDACS.Server
{
    internal class SemaphoreSaturateSlim
    {
        private int start;
        private int max;
        private SemaphoreSlim sema;

        /// <summary>
        /// Creates a special semaphore that never throws an exception when a release would exceed the specified `max`. 
        /// Instead, the current count saturates at the maximum value.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="max"></param>
        public SemaphoreSaturateSlim(int start, int max)
        {
            this.start = start;
            this.max = max;
            this.sema = new SemaphoreSlim(this.start, this.max);
        }

        /// <summary>
        /// Adds one to the semaphore counter and saturates when the counter reached the maximum instead of throwing an
        /// exception like the `SemaphoreSlim`.
        /// </summary>
        public void Release()
        {
            // The lock is only for threads that call this method. It ensures that we do not exceed
            // the maximum count for the semaphore and by doing so it ensures that we do not need to
            // catch any exceptions. I prefer to not throw exceptions in normal code paths if I can
            // write code to avoid doing it. However, could the exception be more effcient in this case?
            lock (this.sema)
            {
                if (this.sema.CurrentCount < this.max)
                {
                    this.sema.Release();
                }
            }
        }

        /// <summary>
        /// Asynchronously wait on this semaphore.
        /// </summary>
        /// <returns></returns>
        public async Task WaitOneAsync()
        {
            await this.sema.WaitAsync();
        }
    }

    public class DoubleEndedStream : Stream, IDisposable
    {
        class Chunk
        {
            public byte[] data;
            public int offset;
            public int actual;
        }

        SemaphoreSaturateSlim rd;
        SemaphoreSlim wh;
        Queue<Chunk> chunks;
        long used;
        long pos;
        bool end_reached;

        public DoubleEndedStream()
        {
            chunks = new Queue<Chunk>();
            wh = new SemaphoreSlim(0);
            rd = new SemaphoreSaturateSlim(0, 1);
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
            Console.WriteLine($"{this}.Close: Closing stream.");
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
            await rd.WaitOneAsync();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException($"The synchronous version of Read for the stream {this} is not implemented.");
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default(CancellationToken))
        {

#if DOUBLE_ENDED_STREAM_DEBUG
            Console.WriteLine("{0}.Read({1}, {2}, {3})", this, buffer, offset, count);
#endif

            if (end_reached)
            {
                return 0;
            }

            await wh.WaitAsync();

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

                rd.Release();

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
            throw new NotImplementedException($"The synchronous version of Write for the stream {this} is not implemented.");
        }
        
        public override async Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
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
