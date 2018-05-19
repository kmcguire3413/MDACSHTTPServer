using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    internal enum HTTPEncoderState
    {
        SendingHeaders,
        SendingChunkedBody,
        SendingContentLengthBody,
        SendingBody,
    }

    internal class HTTPEncoder : IHTTPEncoder
    {
        private Stream s;
        private HTTPEncoderState state;
        private Dictionary<String, String> header;

        public HTTPEncoder(Stream s)
        {
            this.s = s;
        }

        public async Task WriteHeader(Dictionary<String, String> header)
        {
            this.header = header;
        }

        public async Task DoHeaders() {
            if (state != HTTPEncoderState.SendingHeaders)
            {
                throw new Exception("State should have been sending headers, but was something else.");
            }

            String response_code = "500";
            String response_text = "ERROR";

            if (header.ContainsKey("$response_code"))
            {

                if (!header.TryGetValue("$response_code", out response_code))
                {
                    response_code = "500";
                }

                if (!header.TryGetValue("$response_text", out response_text))
                {
                    response_text = "ERROR";
                }
            }

            header.Add("Access-Control-Allow-Origin", "*");
            header.Add("Access-Control-Allow-Methods", "GET, POST");
            header.Add("Access-Control-Allow-Headers", "content-type");

            String _line = String.Format("HTTP/1.1 {0} {1}\r\n", response_code, response_text);
            byte[] _line_bytes = Encoding.UTF8.GetBytes(_line);
            await s.WriteAsync(_line_bytes, 0, _line_bytes.Length);

            foreach (var pair in header)
            {
                if (pair.Key[0] == '$')
                {
                    continue;
                }

                String line = String.Format("{0}: {1}\r\n", pair.Key, pair.Value);

                byte[] line_bytes = Encoding.UTF8.GetBytes(line);

                await s.WriteAsync(line_bytes, 0, line_bytes.Length);
            }

            await s.FlushAsync();

            this.header = null;

            state = HTTPEncoderState.SendingBody;
        }

        public async Task BodyWriteSingleChunk(byte[] chunk, int offset, int length)
        {
            if (state != HTTPEncoderState.SendingHeaders)
            {
                throw new Exception("State should have been sending of headers, but was other.");
            }
            
            //if (header.ContainsKey("content-length"))
            //{
                //header.Remove("content-length");
            //    header["content-length"] = length.ToString();
            //} else
            //{
            //    header.Add("content-length", length.ToString());
            //}

            //header["transfer-encoding"] = "bytes";

            //await DoHeaders();

            // TODO: fix incompatability with Python pycommon or fix pycommon
            // BUG: fix incompatability with Python pycommon or fix pycommon
            /*byte[] tmp = new byte[2];
            tmp[0] = (byte)'\r';
            tmp[1] = (byte)'\n';

            Debug.WriteLine($"offset={offset} length={length} chunk={chunk}");

            await s.WriteAsync(tmp, 0, tmp.Length);

            await s.WriteAsync(chunk, offset, length);

            await s.WriteAsync(tmp, 0, tmp.Length);

            await s.FlushAsync();*/

            // This will implicitly send the headers.
            await BodyWriteFirstChunk(chunk, offset, length);

            // If there is a total content length of zero for this call then the above
            // method just auto-terminated the chunked encoding response, therefore, there
            // is no need to call this method.
            if (length > 0)
            {
                await BodyWriteNoChunk();
            }

            state = HTTPEncoderState.SendingHeaders;
        }

        public async Task BodyWriteFirstChunk(byte[] buf, int offset, int length)
        {
            if (header == null)
            {
                throw new Exception("A valid header must have been set first.");
            }

            if (state != HTTPEncoderState.SendingHeaders)
            {
                throw new Exception("Expected to send headers but state was not as expected.");
            }

            if (header.ContainsKey("transfer-encoding"))
            {
                header["Transfer-Encoding"] = "chunked";
            }
            else
            {
                header.Add("Transfer-Encoding", "chunked");
            }

            header.Add("server", "ok.com");

            await DoHeaders();

            byte[] tmp = new byte[2];
            tmp[0] = (byte)'\r';
            tmp[1] = (byte)'\n';

            await s.WriteAsync(tmp, 0, tmp.Length);

            var chunk_header_str = String.Format("{0:x}\r\n", length);
            byte[] chunk_header = Encoding.UTF8.GetBytes(chunk_header_str);

            await s.WriteAsync(chunk_header, 0, chunk_header.Length);
            await s.WriteAsync(buf, offset, length);

            await s.WriteAsync(tmp, 0, tmp.Length);

            state = HTTPEncoderState.SendingChunkedBody;
        }

        public async Task BodyWriteNextChunk(byte[] buf, int offset, int length)
        {
            if (state == HTTPEncoderState.SendingContentLengthBody || state == HTTPEncoderState.SendingHeaders)
            {
                throw new Exception("State should have been sending of chunked response, but was content-length or headers expecting.");
            }

            var chunk_header_str = String.Format("{0:x}\r\n", length);
            byte[] chunk_header = Encoding.UTF8.GetBytes(chunk_header_str);

            await s.WriteAsync(chunk_header, 0, chunk_header.Length);
            await s.WriteAsync(buf, offset, length);
            byte[] tmp = new byte[2];
            tmp[0] = (byte)'\r';
            tmp[1] = (byte)'\n';
            await s.WriteAsync(tmp, 0, tmp.Length);

            state = HTTPEncoderState.SendingChunkedBody;
        }

        public async Task BodyWriteNoChunk()
        {
            if (state == HTTPEncoderState.SendingContentLengthBody || state == HTTPEncoderState.SendingHeaders)
            {
                throw new Exception("State should have been sending of chunked response, but was content-length or headers expecting.");
            }

            byte[] chunk_header = Encoding.UTF8.GetBytes("0\r\n\r\n");
            await s.WriteAsync(chunk_header, 0, chunk_header.Length);

            await s.FlushAsync();

            state = HTTPEncoderState.SendingHeaders;
        }
    }
}
