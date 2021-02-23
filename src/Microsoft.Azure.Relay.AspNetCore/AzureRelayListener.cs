// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Relay.AspNetCore
{
    /// <summary>
    /// An HTTP server wrapping the Http.Sys APIs that accepts requests.
    /// </summary>
    sealed class AzureRelayListener : IDisposable
    {
        readonly List<HybridConnectionListener> _relayListeners = new List<HybridConnectionListener>();
        readonly object _internalLock = new object();
        readonly Action<RequestContext> requestHandler;
        State _state = State.Stopped;
        BufferBlock<RequestContext> _pendingContexts;

        public AzureRelayListener(AzureRelayOptions options, ILoggerFactory loggerFactory)
            : this(options, loggerFactory, true)
        {
            this.requestHandler = HandleRequest;
        }

        public AzureRelayListener(AzureRelayOptions options, ILoggerFactory loggerFactory, Action<RequestContext> callback)
            : this(options, loggerFactory, true)
        {
            this.requestHandler = callback;
        }

        AzureRelayListener(AzureRelayOptions options, ILoggerFactory loggerFactory, bool priv)
        {
            _pendingContexts = new BufferBlock<RequestContext>();
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            Options = options;

            Logger = LogHelper.CreateLogger(loggerFactory, typeof(AzureRelayListener));
        }
        
        Task<bool> WebSocketAcceptHandler(RelayedHttpListenerContext arg)
        {
            return Task<bool>.FromResult(true);
        }

        void HandleRequest(RequestContext request)
        {
            _pendingContexts.Post(request);
        }

        public Task<RequestContext> AcceptAsync()
        {
            return _pendingContexts.ReceiveAsync();
        }

        internal enum State
        {
            Stopped,
            Started,
            Disposed,
        }

        ILogger Logger { get; }

        public AzureRelayOptions Options { get; }

        public bool IsListening
        {
            get { return _state == State.Started; }
        }        

        /// <summary>
        /// Start accepting incoming requests.
        /// </summary>
        public void Start()
        {
            CheckDisposed();

            LogHelper.LogInfo(Logger, nameof(Start));

            // Make sure there are no race conditions between Start/Stop/Abort/Close/Dispose.
            // Start needs to setup all resources. Abort/Stop must not interfere while Start is
            // allocating those resources.
            lock (_internalLock)
            {
                try
                {
                    CheckDisposed();
                    if (_state == State.Started)
                    {
                        return;
                    }

                    try
                    {
                        foreach (var urlPrefix in Options.UrlPrefixes)
                        {
                            var rcb = new RelayConnectionStringBuilder();

                            var tokenProvider = urlPrefix.TokenProvider != null ? urlPrefix.TokenProvider : Options.TokenProvider;
                            if ( tokenProvider == null )
                            {
                                throw new InvalidOperationException("No relay token provider defined.");
                            }
                            var relayListener = new HybridConnectionListener(
                                new UriBuilder(urlPrefix.FullPrefix) { Scheme = "sb", Port = -1 }.Uri, tokenProvider );
                            
                            // TODO: should i always set this?
                            if( Options.UseCustomProxy )
                                relayListener.Proxy = Options.Proxy;

                            relayListener.RequestHandler = (ctx) => requestHandler(new RequestContext(ctx, new Uri(urlPrefix.FullPrefix)));
                            // TODO: CR: An accept handler which simply returns true is the same as no handler at all.
                            // Would returning false and rejecting relayed connection requests be better? 
                            relayListener.AcceptHandler = WebSocketAcceptHandler;
                            _relayListeners.Add(relayListener);
                        }
                    }
                    catch (Exception exception)
                    {
                        LogHelper.LogException(Logger, ".Ctor", exception);
                        throw;
                    }
                    foreach (var listener in _relayListeners)
                    {
                        listener.OpenAsync().GetAwaiter().GetResult();
                    }
                    _state = State.Started;
                }
                catch (Exception exception)
                {
                    // Make sure the HttpListener instance can't be used if Start() failed.
                    _state = State.Disposed;
                    LogHelper.LogException(Logger, nameof(Start), exception);
                    throw;
                }
            }
        }

        private void Stop()
        {
            try
            {
                lock (_internalLock)
                {
                    CheckDisposed();
                    if (_state == State.Stopped)
                    {
                        return;
                    }

                    _state = State.Stopped;

                    foreach (var listener in _relayListeners)
                    {
                        listener.CloseAsync().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception exception)
            {
                LogHelper.LogException(Logger, nameof(Stop), exception);
                throw;
            }
        }

        /// <summary>
        /// Stop the server and clean up.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            lock (_internalLock)
            {
                try
                {
                    if (_state == State.Disposed)
                    {
                        return;
                    }
                    LogHelper.LogInfo(Logger, nameof(Dispose));

                    Stop();
                }
                catch (Exception exception)
                {
                    LogHelper.LogException(Logger, nameof(Dispose), exception);
                    throw;
                }
                finally
                {
                    _state = State.Disposed;
                }
            }
        }

        private void CheckDisposed()
        {
            if (_state == State.Disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}
