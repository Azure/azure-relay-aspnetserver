// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.Relay.AspNetCore
{
    public class AzureRelayOptions
    {
        public AzureRelayOptions()
        {
        }

        public TokenProvider TokenProvider { get; set; }

        public UrlPrefixCollection UrlPrefixes { get; } = new UrlPrefixCollection();

        internal bool ThrowWriteExceptions { get; set; }

        internal long MaxRequestBodySize { get; set; }

        internal int RequestQueueLimit { get; set; }

        internal int? MaxConnections { get; set; }        
    }
}
