// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.Relay.AspNetCore
{
    internal static class Utilities
    {
        public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(1);

        internal static MessagePump CreateHttpServer(out string baseAddress, RequestDelegate app, TokenProvider tokenProvider = null)
        {
            string root;
            return CreateDynamicHttpServer(string.Empty, out root, out baseAddress, options => { }, app);
        }

        internal static MessagePump CreateHttpServer(out string baseAddress, Action<AzureRelayOptions> configureOptions, RequestDelegate app, TokenProvider tp = null)
        {
            string root;
            return CreateDynamicHttpServer(string.Empty, out root, out baseAddress, configureOptions, app);
        }

        internal static MessagePump CreateHttpServerReturnRoot(string path, out string root, RequestDelegate app, TokenProvider tp = null)
        {
            string baseAddress;
            return CreateDynamicHttpServer(String.Empty, out root, out baseAddress, options => { }, app);
        }

        internal static IWebHost CreateDynamicHost(out string root, RequestDelegate app, TokenProvider tp = null)
        {
            return CreateDynamicHost(string.Empty, out root, out var baseAddress, options =>
            {
            }, app);
        }

        internal static IWebHost CreateDynamicHost(string basePath, out string root, out string baseAddress, Action<AzureRelayOptions> configureOptions, RequestDelegate app, TokenProvider tp = null)
        {
            var prefix = UrlPrefix.Create(new Uri(new Uri(Utilities.GetRelayUrl()), basePath).AbsoluteUri, tp);
            root = prefix.FullPrefix;
            baseAddress = prefix.ToString();

            var builder = new WebHostBuilder()
                .UseAzureRelay(options =>
                {
                    options.UrlPrefixes.Add(prefix);
                    configureOptions(options);
                })
                .Configure(appBuilder => appBuilder.Run(app));

            var host = builder.Build();
            host.Start();
            return host;
        }

        internal static MessagePump CreatePump(TokenProvider tokenProvider = null)
            => new MessagePump(Options.Create(new AzureRelayOptions() { TokenProvider = tokenProvider }), new LoggerFactory(), new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())));

        internal static MessagePump CreateDynamicHttpServer(string basePath, out string root, out string baseAddress, Action<AzureRelayOptions> configureOptions, RequestDelegate app, TokenProvider tp = null)
        {
            var rootUri = new Uri(GetRelayUrl());
            var prefix = UrlPrefix.Create(new Uri(rootUri, basePath).AbsoluteUri, tp);
            root = rootUri.ToString();
            baseAddress = prefix.FullPrefix;

            var server = CreatePump(CreateTokenProvider());
            server.Features.Get<IServerAddressesFeature>().Addresses.Add(baseAddress);
            configureOptions(server.Listener.Options);
            server.StartAsync(new DummyApplication(app), CancellationToken.None).Wait();
            return server;
        }

        internal static MessagePump CreateHttpsServer(RequestDelegate app, TokenProvider tp = null)
        {
            return CreateServer(GetRelayUrl(), app, tp);
        }

        private static MessagePump CreateServer(string url, RequestDelegate app, TokenProvider tp)
        {
            return CreateServer(url, string.Empty, app, tp);
        }

        internal static MessagePump CreateServer(string url, string path, RequestDelegate app, TokenProvider tp = null)
        {

            var server = CreatePump(tp);
            server.Features.Get<IServerAddressesFeature>().Addresses.Add(
                UrlPrefix.Create(new Uri(new Uri(url), path).AbsoluteUri, tp != null ? tp : Utilities.CreateTokenProvider()).ToString());
            server.StartAsync(new DummyApplication(app), CancellationToken.None).Wait();
            return server;
        }

        internal static TokenProvider CreateTokenProvider()
        {
            var connectionString = Environment.GetEnvironmentVariable("RELAY_TEST_CONNECTIONSTRING_NOAUTH");
            Assert.False(string.IsNullOrEmpty(connectionString), "RELAY_TEST_CONNECTIONSTRING_NOAUTH must be set");

            var cb = new RelayConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(cb.SharedAccessSignature))
            {
                return TokenProvider.CreateSharedAccessSignatureTokenProvider(cb.SharedAccessSignature);
            }
            else
            {
                return TokenProvider.CreateSharedAccessSignatureTokenProvider(cb.SharedAccessKeyName, cb.SharedAccessKey);
            }
        }

        internal static string GetRelayUrl()
        {
            var connectionString = Environment.GetEnvironmentVariable("RELAY_TEST_CONNECTIONSTRING_NOAUTH");
            Assert.False(string.IsNullOrEmpty(connectionString), "RELAY_TEST_CONNECTIONSTRING_NOAUTH must be set");

            var cb = new RelayConnectionStringBuilder(connectionString);

            Assert.NotNull(connectionString);
            Assert.False(string.IsNullOrEmpty(connectionString));

            return new Uri(new UriBuilder(cb.Endpoint){Scheme=Uri.UriSchemeHttps}.Uri, cb.EntityPath.EndsWith("/")?cb.EntityPath:cb.EntityPath+"/").AbsoluteUri;
        }
    }
}
