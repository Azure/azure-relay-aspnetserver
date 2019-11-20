using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Relay.AspNetCore
{
    class Response
    {
        private readonly RelayedHttpListenerResponse _innerResponse;
        private HeaderCollection _headers;

        public Response(RelayedHttpListenerResponse innerResponse, Uri baseUri)
        {
            _innerResponse = innerResponse;
            _headers = new HeaderCollection((key) => this.UpdateHeaders(key));
            foreach (var hdr in innerResponse.Headers.AllKeys)
            {
                _headers.Append(hdr, innerResponse.Headers[hdr]);
            }
        }

        public HeaderCollection Headers
        {
            get
            {
                return _headers;
            }
            set
            {
                _headers = value;
            }
        }

        public Stream Body => _innerResponse.OutputStream;

        public int StatusCode
        {
            get
            {
                return (int)_innerResponse.StatusCode;
            }
            set
            {
                if (value <= 100 || value > 999)
                {
                    throw new ArgumentOutOfRangeException();
                }
                _innerResponse.StatusCode = (HttpStatusCode)value;
            }
        }

        public string ReasonPhrase
        {
            get
            {
                return _innerResponse.StatusDescription;
            }
            set
            {
                _innerResponse.StatusDescription = value;
            }
        }

        public long? ContentLength { get; internal set; }

        internal async Task SendFileAsync(string path, long offset, long? length, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }
            using (var fs = File.OpenRead(path))
            {
                if (length.HasValue && length > fs.Length)
                {
                    throw new ArgumentOutOfRangeException("length");
                }
                if (offset > fs.Length)
                {
                    throw new ArgumentOutOfRangeException("offset");
                }
                long len = length.HasValue ? length.Value : fs.Length;
                byte[] buffer = new byte[81920];
                fs.Seek(offset, SeekOrigin.Begin);
                long sent = 0;
                do
                {
                    int read = await fs.ReadAsync(buffer, 0, (int)Math.Min((long)buffer.Length, Math.Min(len - sent, int.MaxValue)), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }
                    await this.Body.WriteAsync(buffer, 0, read);
                    sent += read;
                }
                while (sent < len);
            }
        }

        public TimeSpan CacheTtl { get; internal set; }
        public bool HasStarted { get; internal set; }

        public void Close()
        {
            _innerResponse.Close();
        }

        public Task CloseAsync()
        {
            return _innerResponse.CloseAsync();
        }

        internal void UpdateHeaders(string key)
        {
            if (key == null)
            {
                // entire collection was cleared
                _innerResponse.Headers.Clear();
            }
            else if (Headers.ContainsKey(key))
            {
                // key was updated
                _innerResponse.Headers[key] = Headers[key];
            }
            else
            {
                // key was removed
                _innerResponse.Headers.Remove(key);
            }
        }
    }
}
