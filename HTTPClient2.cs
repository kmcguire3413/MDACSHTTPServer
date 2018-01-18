using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public enum HTTPRequestMethod
    {
        GET,
        POST,
        UNKNOWN,
    }

    /// <summary>
    /// The request object handled to the next level handler by HTTPClient2.
    /// </summary>
    public struct HTTPRequest
    {
        public String url;
        public String url_absolute;
        public Dictionary<String, String> query;
        public String query_string;
        public HTTPRequestMethod method;
        public Dictionary<String, String> internal_headers;
    }
    /// <summary>
    /// A slightly higher level implementation of the client. Handles parsing of the resource string.
    /// </summary>
    internal class HTTPClient2 : HTTPClient
    {
        public HTTPClient2(IHTTPDecoder decoder, IHTTPEncoder encoder) : base(decoder, encoder)
        {
        }

        /// <summary>
        /// Must be implemented by sub-class.
        /// </summary>
        /// <remarks>May come back and refactor this to use interfaces instead of subclases.</remarks>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
        public virtual async Task<Task> HandleRequest2(HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            throw new Exception("Not Implemented");
        }

        /// <summary>
        /// Performs higher-level interpretation of application layer data and provides that in a higher level form
        /// for consumption by the upper layers.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
        /// <remarks>This implementation needs cleaning on variable names for query string processing.</remarks>
        public override async Task<Task> HandleRequest(Dictionary<String, String> header, Stream body, IProxyHTTPEncoder encoder)
        {
            var outheader = new Dictionary<String, String>();

            // Ensure the URL parameter actually exists instead of crashing.
            if (!header.ContainsKey("$url"))
            {
                outheader.Add("$response_code", "500");
                outheader.Add("$response_text", "ERROR");
                await encoder.WriteHeader(outheader);
                await encoder.BodyWriteSingleChunk("The request did not specify a URL.");
                return Task.CompletedTask;
            }

            var url = header["$url"];
            var url_absolute = url;
            var query_string = new Dictionary<String, String>();
            String query_as_string = null;

            // Break down any query string into its key and value parts.
            if (url.IndexOf("?") > -1)
            {
                var qsndx = url.IndexOf("?");
                query_as_string = url.Substring(qsndx + 1);
                var qstring_parts = query_as_string.Split('&');

                foreach (var part in qstring_parts)
                {
                    var eqndx = part.IndexOf("=");

                    if (eqndx < 0)
                    {
                        query_string.Add(part, part);
                    }
                    else
                    {
                        query_string.Add(part.Substring(0, eqndx), part.Substring(eqndx + 1));
                    }
                }

                url_absolute = url.Substring(0, qsndx);
            }

            HTTPRequest request;

            switch (header["$method"].ToLower()) {
                case "get":
                    request.method = HTTPRequestMethod.GET;
                    break;
                case "post":
                    request.method = HTTPRequestMethod.POST;
                    break;
                default:
                    request.method = HTTPRequestMethod.UNKNOWN;
                    break;
            }

            // Package everything up nice. Hide away the messy implementation details.
            request.internal_headers = header;
            request.query = query_string;
            request.url = url;
            request.url_absolute = url_absolute;
            request.query_string = query_as_string;

            return await HandleRequest2(request, body, encoder);
        }
    }
}
