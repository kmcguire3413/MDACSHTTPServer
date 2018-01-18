using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public interface IHTTPClient
    {
        Task Handle();
        Task<Task> HandleRequest(Dictionary<string, string> header, Stream body, IProxyHTTPEncoder encoder);
    }
}