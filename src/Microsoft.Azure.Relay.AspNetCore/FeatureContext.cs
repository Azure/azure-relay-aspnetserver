// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#if !NETSTANDARD2_0
using System.IO.Pipelines;
#endif

namespace Microsoft.Azure.Relay.AspNetCore
{
    class FeatureContext :
        IHttpRequestFeature,
        IHttpConnectionFeature,
        IHttpResponseFeature,
        IHttpRequestIdentifierFeature,
#if NETSTANDARD2_0
        IHttpBufferingFeature,
        IHttpSendFileFeature,
#else
        IHttpResponseBodyFeature,
#endif
        IHttpUpgradeFeature,
        IHttpWebSocketFeature
    {
        private readonly RequestContext _requestContext;
        private readonly IFeatureCollection _features;

        private Stream _requestBody;
        private IHeaderDictionary _requestHeaders;
        private string _scheme;
        private string _httpMethod;
        private string _httpProtocolVersion;
        private string _query;
        private string _pathBase;
        private string _path;
        private string _rawTarget;
        private IPAddress _remoteIpAddress;
        private IPAddress _localIpAddress;
        private int _remotePort;
        private int _localPort;
        private string _connectionId;
        private string _traceIdentitfier;
        private Stream _responseStream;
#if !NETSTANDARD2_0
        private PipeWriter _pipeWriter;
#endif

        private Fields _initializedFields;

        private List<Tuple<Func<object, Task>, object>> _onStartingActions = new List<Tuple<Func<object, Task>, object>>();
        private List<Tuple<Func<object, Task>, object>> _onCompletedActions = new List<Tuple<Func<object, Task>, object>>();
        private bool _responseStarted;
        private bool _completed;

        internal FeatureContext(RequestContext requestContext)
        {
            _requestContext = requestContext;
            _features = new FeatureCollection(new StandardFeatureCollection(this));

            // Pre-initialize any fields that are not lazy at the lower level.
            _requestHeaders = Request.Headers;
            _httpMethod = Request.Method;
            _path = Request.Path;
            _pathBase = Request.PathBase;
            _query = Request.QueryString;
            _rawTarget = Request.RawUrl;
            _scheme = Request.Scheme;

            _responseStream = new ResponseStream(requestContext.Response.Body, OnResponseStart);
#if !NETSTANDARD2_0
            _pipeWriter = PipeWriter.Create(_responseStream, new StreamPipeWriterOptions(leaveOpen: true));
#endif
        }

        internal IFeatureCollection Features => _features;

        internal object RequestContext => _requestContext;

        private Request Request => _requestContext.Request;

        private Response Response => _requestContext.Response;

        [Flags]
        // Fields that may be lazy-initialized
        private enum Fields
        {
            None = 0x0,
            Protocol = 0x1,
            RequestBody = 0x2,
            RequestAborted = 0x4,
            LocalIpAddress = 0x8,
            RemoteIpAddress = 0x10,
            LocalPort = 0x20,
            RemotePort = 0x40,
            ConnectionId = 0x80,
            ClientCertificate = 0x100,
            TraceIdentifier = 0x200,
        }

        private bool IsNotInitialized(Fields field)
        {
            return (_initializedFields & field) != field;
        }

        private void SetInitialized(Fields field)
        {
            _initializedFields |= field;
        }

        Stream IHttpRequestFeature.Body
        {
            get
            {
                if (IsNotInitialized(Fields.RequestBody))
                {
                    _requestBody = Request.Body;
                    SetInitialized(Fields.RequestBody);
                }
                return _requestBody;
            }
            set
            {
                _requestBody = value;
                SetInitialized(Fields.RequestBody);
            }
        }

        IHeaderDictionary IHttpRequestFeature.Headers
        {
            get { return _requestHeaders; }
            set { _requestHeaders = value; }
        }

        string IHttpRequestFeature.Method
        {
            get { return _httpMethod; }
            set { _httpMethod = value; }
        }

        string IHttpRequestFeature.Path
        {
            get { return _path; }
            set { _path = value; }
        }

        string IHttpRequestFeature.PathBase
        {
            get { return _pathBase; }
            set { _pathBase = value; }
        }

        string IHttpRequestFeature.Protocol
        {
            get
            {
                if (IsNotInitialized(Fields.Protocol))
                {
                    _httpProtocolVersion = "HTTP/1.1";
                    SetInitialized(Fields.Protocol);
                }
                return _httpProtocolVersion;
            }
            set
            {
                _httpProtocolVersion = value;
                SetInitialized(Fields.Protocol);
            }
        }

        string IHttpRequestFeature.QueryString
        {
            get { return _query; }
            set { _query = value; }
        }

        string IHttpRequestFeature.RawTarget
        {
            get { return _rawTarget; }
            set { _rawTarget = value; }
        }

        string IHttpRequestFeature.Scheme
        {
            get { return _scheme; }
            set { _scheme = value; }
        }

        IPAddress IHttpConnectionFeature.LocalIpAddress
        {
            get
            {
                if (IsNotInitialized(Fields.LocalIpAddress))
                {
                    _localIpAddress = null;
                    SetInitialized(Fields.LocalIpAddress);
                }
                return _localIpAddress;
            }
            set
            {
                _localIpAddress = value;
                SetInitialized(Fields.LocalIpAddress);
            }
        }

        IPAddress IHttpConnectionFeature.RemoteIpAddress
        {
            get
            {
                if (IsNotInitialized(Fields.RemoteIpAddress))
                {
                    _remoteIpAddress = null;
                    SetInitialized(Fields.RemoteIpAddress);
                }
                return _remoteIpAddress;
            }
            set
            {
                _remoteIpAddress = value;
                SetInitialized(Fields.RemoteIpAddress);
            }
        }

        int IHttpConnectionFeature.LocalPort
        {
            get
            {
                if (IsNotInitialized(Fields.LocalPort))
                {
                    _localPort = -1;
                    SetInitialized(Fields.LocalPort);
                }
                return _localPort;
            }
            set
            {
                _localPort = value;
                SetInitialized(Fields.LocalPort);
            }
        }

        int IHttpConnectionFeature.RemotePort
        {
            get
            {
                if (IsNotInitialized(Fields.RemotePort))
                {
                    _remotePort = -1;
                    SetInitialized(Fields.RemotePort);
                }
                return _remotePort;
            }
            set
            {
                _remotePort = value;
                SetInitialized(Fields.RemotePort);
            }
        }

        string IHttpConnectionFeature.ConnectionId
        {
            get
            {
                if (IsNotInitialized(Fields.ConnectionId))
                {
                    _connectionId = Request.GetHashCode().ToString(CultureInfo.InvariantCulture);
                    SetInitialized(Fields.ConnectionId);
                }
                return _connectionId;
            }
            set
            {
                _connectionId = value;
                SetInitialized(Fields.ConnectionId);
            }
        }

        Stream IHttpResponseFeature.Body
        {
            get { return _responseStream; }
            set { _responseStream = value; }
        }

        IHeaderDictionary IHttpResponseFeature.Headers
        {
            get { return Response.Headers; }
            set { Response.Headers = new HeaderCollection(value, (key) => Response.UpdateHeaders(key)); }
        }

        bool IHttpResponseFeature.HasStarted => false;

        void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            if (_onStartingActions == null)
            {
                throw new InvalidOperationException("Cannot register new callbacks, the response has already started.");
            }

            _onStartingActions.Add(new Tuple<Func<object, Task>, object>(callback, state));
        }

        void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            if (_onCompletedActions == null)
            {
                throw new InvalidOperationException("Cannot register new callbacks, the response has already completed.");
            }

            _onCompletedActions.Add(new Tuple<Func<object, Task>, object>(callback, state));
        }

        string IHttpResponseFeature.ReasonPhrase
        {
            get { return Response.ReasonPhrase; }
            set { Response.ReasonPhrase = value; }
        }

        int IHttpResponseFeature.StatusCode
        {
            get { return (int)Response.StatusCode; }
            set { Response.StatusCode = value; }
        }
        
        string IHttpRequestIdentifierFeature.TraceIdentifier
        {
            get
            {
                if (IsNotInitialized(Fields.TraceIdentifier))
                {
                    _traceIdentitfier = _requestContext.GetHashCode().ToString();
                    SetInitialized(Fields.TraceIdentifier);
                }
                return _traceIdentitfier;
            }
            set
            {
                _traceIdentitfier = value;
                SetInitialized(Fields.TraceIdentifier);
            }
        }

        internal async Task OnResponseStart()
        {
            if (_responseStarted)
            {
                return;
            }
            _responseStarted = true;
            await NotifiyOnStartingAsync();
        }

        private async Task NotifiyOnStartingAsync()
        {
            var actions = _onStartingActions;
            _onStartingActions = null;
            if (actions == null)
            {
                return;
            }

            actions.Reverse();
            // Execute last to first. This mimics a stack unwind.
            foreach (var actionPair in actions)
            {
                await actionPair.Item1(actionPair.Item2);
            }
        }

        internal Task OnCompleted()
        {
            if (_completed)
            {
                return Task.CompletedTask;
            }
            _completed = true;
            Response.Close();
            return NotifyOnCompletedAsync();
        }

        private async Task NotifyOnCompletedAsync()
        {
            var actions = _onCompletedActions;
            _onCompletedActions = null;
            if (actions == null)
            {
                return;
            }

            actions.Reverse();
            // Execute last to first. This mimics a stack unwind.
            foreach (var actionPair in actions)
            {
                await actionPair.Item1(actionPair.Item2);
            }
        }

        bool IHttpUpgradeFeature.IsUpgradableRequest => true;

        bool IHttpWebSocketFeature.IsWebSocketRequest => false;

        Task<Stream> IHttpUpgradeFeature.UpgradeAsync()
        {
            throw new NotImplementedException();
        }

        Task<WebSocket> IHttpWebSocketFeature.AcceptAsync(WebSocketAcceptContext context)
        {
            throw new NotImplementedException();
        }
#if NETSTANDARD2_0
        async Task IHttpSendFileFeature.SendFileAsync(string path, long offset, long? length, CancellationToken cancellation)
        {
            await OnResponseStart();
            await Response.SendFileAsync(path, offset, length, cancellation);
        }

        void IHttpBufferingFeature.DisableRequestBuffering()
        {
            // There is no request buffering.
        }

        void IHttpBufferingFeature.DisableResponseBuffering()
        {
            // TODO: What about native buffering?
        }
#endif

#if !NETSTANDARD2_0
        Stream IHttpResponseBodyFeature.Stream => _responseStream;

        PipeWriter IHttpResponseBodyFeature.Writer => _pipeWriter;

        void IHttpResponseBodyFeature.DisableBuffering()
        {
            throw new NotImplementedException();
        }

        Task IHttpResponseBodyFeature.StartAsync(CancellationToken cancellationToken)
        {
            return OnResponseStart();
        }

        async Task IHttpResponseBodyFeature.SendFileAsync(string path, long offset, long? length, CancellationToken cancellation)
        {
            await OnResponseStart();
            await Response.SendFileAsync(path, offset, length, cancellation);
        }

        Task IHttpResponseBodyFeature.CompleteAsync()
        {
            return OnCompleted();
        }
#endif
    }
}
