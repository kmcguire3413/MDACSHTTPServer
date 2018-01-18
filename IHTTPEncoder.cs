using System.Collections.Generic;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public interface IHTTPEncoder
    {
        Task BodyWriteFirstChunk(byte[] buf, int offset, int length);
        Task BodyWriteNextChunk(byte[] buf, int offset, int length);
        Task BodyWriteNoChunk();
        Task BodyWriteSingleChunk(byte[] chunk, int offset, int length);
        Task DoHeaders();
        Task WriteHeader(Dictionary<string, string> header);
    }
}