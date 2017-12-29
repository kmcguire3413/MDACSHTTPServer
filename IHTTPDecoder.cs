using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public interface IHTTPDecoder
    {
        Task<(Stream, Task)> ReadBody(HTTPDecoderBodyType body_type);
        Task<List<string>> ReadHeader();
    }
}