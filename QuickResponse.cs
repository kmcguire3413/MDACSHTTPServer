using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    /// <summary>
    /// This class represents an easier or maybe more ergonomic way to specify the header
    /// fields without having to manually create a dictionary and hand-type each field of
    /// the header. It performs no value checking.
    /// </summary>
    /// <seealso cref="IProxyHTTPEncoder.WriteQuickHeader(int, string)"/>
    /// <seealso cref="IProxyHTTPEncoder.WriteQuickHeaderAndStringBody(int, string, string)"/>
    public class QuickResponse
    {
        public int response_code;
        public string response_text;
        public Dictionary<string, string> header;
        public IProxyHTTPEncoder proxy;

        public QuickResponse(int code, string text, IProxyHTTPEncoder proxy)
        {
            response_code = code;
            response_text = text;
            header = new Dictionary<string, string>();
            this.proxy = proxy;
        }

        public QuickResponse AddHeader(string key, string value)
        {
            header.Add(key, value);
            return this;
        }

        public async Task<Task> SendStream(Stream s)
        {

            header.Add("$response_code", response_code.ToString());
            header.Add("$response_text", response_text);

            await proxy.WriteHeader(header);
            return await proxy.BodyWriteStream(s);
        }

        public async Task<Task> SendJsonFromObject(object obj)
        {
            header.Add("$response_code", response_code.ToString());
            header.Add("$response_text", response_text);

            await proxy.WriteHeader(header);
            await proxy.BodyWriteSingleChunk(
                JsonConvert.SerializeObject(obj)
            );

            return Task.CompletedTask;
        }

        public async Task<Task> SendNothing()
        {
            header.Add("$response_code", response_code.ToString());
            header.Add("$response_text", response_text);

            await proxy.WriteHeader(header);
            await proxy.BodyWriteSingleChunk("");

            return Task.CompletedTask;
        }

        public async Task<Task> SendString(string s)
        {
            header.Add("$response_code", response_code.ToString());
            header.Add("$response_text", response_text);

            await proxy.WriteHeader(header);
            await proxy.BodyWriteSingleChunk(s);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Specifying which web sites can participate in cross-origin resource sharing.
        /// </summary>
        public QuickResponse AccessControlAllowOrigin(string v) => AddHeader("Access-Control-Allow-Origin", v);
        /// <summary>
        /// Specifying which web sites can participate in cross-origin resource sharing.
        /// </summary>
        public QuickResponse AccessControlAllowCredentials(string v) => AddHeader("Access-Control-Allow-Credentials", v);
        /// <summary>
        /// Specifying which web sites can participate in cross-origin resource sharing.
        /// </summary>
        public QuickResponse AccessControlExposeHeaders(string v) => AddHeader("Access-Control-Expose-Headers", v);
        /// <summary>
        /// Specifying which web sites can participate in cross-origin resource sharing.
        /// </summary>
        public QuickResponse AccessControlMaxAge(string v) => AddHeader("Access-Control-Max-Age", v);
        /// <summary>
        /// Specifying which web sites can participate in cross-origin resource sharing.
        /// </summary>
        public QuickResponse AccessControlAllowMethods(string v) => AddHeader("Access-Control-Allow-Methods", v);
        /// <summary>
        /// Specifying which web sites can participate in cross-origin resource sharing.
        /// </summary>
        public QuickResponse AccessControlAllowHeaders(string v) => AddHeader("Access-Control-Allow-Headers", v);
        public QuickResponse AcceptPatch(string v) => AddHeader("Accept-Patch", v);
        public QuickResponse AcceptRanges(string v) => AddHeader("Accept-Ranges", v);
        public QuickResponse Age(string v) => AddHeader("Age", v);
        public QuickResponse Allow(string v) => AddHeader("Allow", v);
        public QuickResponse AltSvc(string v) => AddHeader("Alt-Svc", v);
        public QuickResponse CacheControl(string v) => AddHeader("Cache-Control", v);
        public QuickResponse Connection(string v) => AddHeader("Connection", v);
        public QuickResponse ContentDisposition(string v) => AddHeader("Content-Disposition", v);
        public QuickResponse ContentEncoding(string v) => AddHeader("Content-Encoding", v);
        public QuickResponse ContentLanguage(string v) => AddHeader("Content-Language", v);
        public QuickResponse ContentLength(string v) => AddHeader("Content-Length", v);
        public QuickResponse ContentLocation(string v) => AddHeader("Content-Location", v);
        public QuickResponse ContentMD5(string v) => AddHeader("Content-MD5", v);
        public QuickResponse ContentRange(string v) => AddHeader("Content-Range", v);
        public QuickResponse ContentType(string v) => AddHeader("Content-Type", v);
        public QuickResponse Date(string v) => AddHeader("Date", v);
        public QuickResponse ETag(string v) => AddHeader("ETag", v);
        public QuickResponse Expires(string v) => AddHeader("Expires", v);
        public QuickResponse LastModified(string v) => AddHeader("Last-Modified", v);
        public QuickResponse Link(string v) => AddHeader("Link", v);
        public QuickResponse Location(string v) => AddHeader("Location", v);
        public QuickResponse P3P(string v) => AddHeader("P3P", v);
        public QuickResponse Pragma(string v) => AddHeader("Pragma", v);
        public QuickResponse ProxyAuthenticate(string v) => AddHeader("Proxy-Authenticate", v);
        public QuickResponse PublicKeyPins(string v) => AddHeader("Public-Key-Pins", v);
        public QuickResponse RetryAfter(string v) => AddHeader("Retry-After", v);
        public QuickResponse Server(string v) => AddHeader("Server", v);
        public QuickResponse SetCookie(string v) => AddHeader("Set-Cookie", v);
        public QuickResponse StrictTransportSecurity(string v) => AddHeader("Strict-Transport-Security", v);
        public QuickResponse Trailer(string v) => AddHeader("Trailer", v);
        public QuickResponse TransferEncoding(string v) => AddHeader("Transfer-Encoding", v);
        public QuickResponse Tk(string v) => AddHeader("Tk", v);
        public QuickResponse Upgrade(string v) => AddHeader("Upgrade", v);
        public QuickResponse Vary(string v) => AddHeader("Vary", v);
        public QuickResponse Via(string v) => AddHeader("Via", v);
        public QuickResponse Warning(string v) => AddHeader("Warning", v);
        public QuickResponse WWWAuthenticate(string v) => AddHeader("WWW-Authenticate", v);
        public QuickResponse XFrameOptions(string v) => AddHeader("X-Frame-Options", v);
        public QuickResponse ContentSecurityPolicy(string v) => AddHeader("Content-Security-Policy", v);
        public QuickResponse XContentSecurityPolicy(string v) => AddHeader("X-Content-Security-Policy", v);
        public QuickResponse XWebKitCSP(string v) => AddHeader("X-WebKit-CSP", v);
        /// <summary>
        /// Used in redirection, or when a new resource has been created. This refresh redirects after 5 seconds. Header extension introduced by Netscape and supported by most web browsers.
        /// </summary>
        public QuickResponse Refresh(string v) => AddHeader("Refresh", v);
        /// <summary>
        /// CGI header field specifying the status of the HTTP response. Normal HTTP responses use a separate "Status-Line" instead, defined by RFC 7230.
        /// </summary>
        public QuickResponse Status(string v) => AddHeader("Status", v);
        /// <summary>
        /// Tells a server which (presumably in the middle of a HTTP -> HTTPS migration) hosts mixed content that the client would prefer redirection to HTTPS and can handle Content-Security-Policy: upgrade-insecure-requests. _Must not be used with HTTP/2_.
        /// </summary>
        public QuickResponse UpgradeInsecureRequests(string v) => AddHeader("Upgrade-Insecure-Requests", v);
        /// <summary>
        /// Provide the duration of the audio or video in seconds; only supported by Gecko browsers
        /// </summary>
        public QuickResponse XContentDuration(string v) => AddHeader("X-Content-Duration", v);
        /// <summary>
        /// The only defined value, "nosniff", prevents Internet Explorer from MIME-sniffing a response away from the declared content-type. This also applies to Google Chrome, when downloading extensions.
        /// </summary>
        public QuickResponse XContentTypeOptions(string v) => AddHeader("X-Content-Type-Options", v);
        /// <summary>
        /// Specifies the technology (e.g. ASP.NET, PHP, JBoss) supporting the web application (version details are often in X-Runtime, X-Version, or X-AspNet-Version)
        /// </summary>
        public QuickResponse XPoweredBy(string v) => AddHeader("X-Powered-By", v);
        /// <summary>
        /// Correlates HTTP requests between a client and server.
        /// </summary>
        public QuickResponse XRequestID(string v) => AddHeader("X-Request-ID", v);
        /// <summary>
        /// Correlates HTTP requests between a client and server.
        /// </summary>
        public QuickResponse XCorrelationID(string v) => AddHeader("X-Correlation-ID", v);
        /// <summary>
        /// Recommends the preferred rendering engine (often a backward-compatibility mode) to use to display the content. Also used to activate Chrome Frame in Internet Explorer.
        /// </summary>
        public QuickResponse XUACompatible(string v) => AddHeader("X-UA-Compatible", v);
        /// <summary>
        /// Cross-site scripting (XSS) filter.
        /// </summary>
        public QuickResponse XXSSProtection(string v) => AddHeader("X-XSS-Protection", v);
    }
}
