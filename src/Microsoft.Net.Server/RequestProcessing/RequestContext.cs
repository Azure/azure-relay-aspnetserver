// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING
// WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF
// TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR
// NON-INFRINGEMENT.
// See the Apache 2 License for the specific language governing
// permissions and limitations under the License.

//------------------------------------------------------------------------------
// <copyright file="HttpListenerContext.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;

namespace Microsoft.Net.Server
{
    using OpaqueFunc = Func<IDictionary<string, object>, Task>;

    public sealed class RequestContext : IDisposable
    {
        private WebListener _server;
        private Request _request;
        private Response _response;
        private NativeRequestContext _memoryBlob;
        private OpaqueFunc _opaqueCallback;
        private bool _disposed;
        private CancellationTokenSource _requestAbortSource;
        private CancellationToken? _disconnectToken;

        internal RequestContext(WebListener httpListener, NativeRequestContext memoryBlob)
        {
            // TODO: Verbose log
            _server = httpListener;
            _memoryBlob = memoryBlob;
            _request = new Request(this, _memoryBlob);
            _response = new Response(this);
            _request.ReleasePins();
        }

        public Request Request
        {
            get
            {
                return _request;
            }
        }

        public Response Response
        {
            get
            {
                return _response;
            }
        }

        public IPrincipal User
        {
            get { return _request.User; }
        }

        public CancellationToken DisconnectToken
        {
            get
            {
                // Create a new token per request, but link it to a single connection token.
                // We need to be able to dispose of the registrations each request to prevent leaks.
                if (!_disconnectToken.HasValue)
                {
                    var connectionDisconnectToken = _server.RegisterForDisconnectNotification(this);

                    if (connectionDisconnectToken.CanBeCanceled)
                    {
                        _requestAbortSource = CancellationTokenSource.CreateLinkedTokenSource(connectionDisconnectToken);
                        _disconnectToken = _requestAbortSource.Token;
                    }
                    else
                    {
                        _disconnectToken = CancellationToken.None;
                    }
                }
                return _disconnectToken.Value;
            }
        }

        internal WebListener Server
        {
            get
            {
                return _server;
            }
        }

        internal ILogger Logger
        {
            get { return Server.Logger; }
        }

        internal SafeHandle RequestQueueHandle
        {
            get
            {
                return _server.RequestQueueHandle;
            }
        }

        internal ulong RequestId
        {
            get
            {
                return Request.RequestId;
            }
        }
        /*
        public bool TryGetOpaqueUpgrade(ref Action<IDictionary<string, object>, OpaqueFunc> value)
        {
            if (_request.IsUpgradable)
            {
                value = OpaqueUpgrade;
                return true;
            }
            return false;
        }

        public bool TryGetChannelBinding(ref ChannelBinding value)
        {
            value = Server.GetChannelBinding(Request.ConnectionId, Request.IsSecureConnection);
            return value != null;
        }
        */

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            // TODO: Verbose log
            try
            {
                if (_requestAbortSource != null)
                {
                    _requestAbortSource.Dispose();
                }
                _response.Dispose();
            }
            finally
            {
                _request.Dispose();
            }
        }

        public void Abort()
        {
            // May be called from Dispose() code path, don't check _disposed.
            // TODO: Verbose log
            _disposed = true;
            if (_requestAbortSource != null)
            {
                try
                {
                    _requestAbortSource.Cancel();
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(Logger, "Abort", ex);
                }
                _requestAbortSource.Dispose();
            }
            ForceCancelRequest(RequestQueueHandle, _request.RequestId);
            _request.Dispose();
        }

        // This is only called while processing incoming requests.  We don't have to worry about cancelling 
        // any response writes.
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Justification =
            "It is safe to ignore the return value on a cancel operation because the connection is being closed")]
        internal static void CancelRequest(SafeHandle requestQueueHandle, ulong requestId)
        {
            UnsafeNclNativeMethods.HttpApi.HttpCancelHttpRequest(requestQueueHandle, requestId,
                IntPtr.Zero);
        }

        // The request is being aborted, but large writes may be in progress. Cancel them.
        internal void ForceCancelRequest(SafeHandle requestQueueHandle, ulong requestId)
        {
            try
            {
                uint statusCode = UnsafeNclNativeMethods.HttpApi.HttpCancelHttpRequest(requestQueueHandle, requestId,
                    IntPtr.Zero);

                // Either the connection has already dropped, or the last write is in progress.
                // The requestId becomes invalid as soon as the last Content-Length write starts.
                // The only way to cancel now is with CancelIoEx.
                if (statusCode == UnsafeNclNativeMethods.ErrorCodes.ERROR_CONNECTION_INVALID)
                {
                    _response.CancelLastWrite(requestQueueHandle);
                }
            }
            catch (ObjectDisposedException)
            {
                // RequestQueueHandle may have been closed
            }
        }
        /*
        internal void OpaqueUpgrade(IDictionary<string, object> parameters, OpaqueFunc callback)
        {
            // Parameters are ignored for now
            if (Response.SentHeaders)
            {
                throw new InvalidOperationException();
            }
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            // Set the status code and reason phrase
            Response.StatusCode = (int)HttpStatusCode.SwitchingProtocols;
            Response.ReasonPhrase = HttpReasonPhrase.Get(HttpStatusCode.SwitchingProtocols);

            // Store the callback and process it after the stack unwind.
            _opaqueCallback = callback;
        }

        // Called after the AppFunc completes for any necessary post-processing.
        internal unsafe Task ProcessResponseAsync()
        {
            // If an upgrade was requested, perform it
            if (!Response.SentHeaders && _opaqueCallback != null
                && Response.StatusCode == (int)HttpStatusCode.SwitchingProtocols)
            {
                Response.SendOpaqueUpgrade();

                IDictionary<string, object> opaqueEnv = CreateOpaqueEnvironment();
                return _opaqueCallback(opaqueEnv);
            }

            return Helpers.CompletedTask();
        }

        private IDictionary<string, object> CreateOpaqueEnvironment()
        {
            IDictionary<string, object> opaqueEnv = new Dictionary<string, object>();

            opaqueEnv[Constants.OpaqueVersionKey] = Constants.OpaqueVersion;
            // TODO: Separate CT?
            // opaqueEnv[Constants.OpaqueCallCancelledKey] = Environment.CallCancelled;

            Request.SwitchToOpaqueMode();
            Response.SwitchToOpaqueMode();
            opaqueEnv[Constants.OpaqueStreamKey] = new OpaqueStream(Request.Body, Response.Body);

            return opaqueEnv;
        }
        */
    }
}