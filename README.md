ASP.NET Core Hosting for Azure Relay
=================

![](https://github.com/Azure/azure-relay-aspnetserver/workflows/azure-relay-aspnetserver/badge.svg?branch=dev)

This repo contains a web server for ASP.NET Core based on Azure Relay Hybrid Connections HTTP. A NuGet package produced from this repo is available on NuGet as [Microsoft.Azure.Relay.AspNetCore](https://www.nuget.org/packages/Microsoft.Azure.Relay.AspNetCore).

The integration supports most ASP.NET scenarios, with a few exceptions. WebSocket support will
be added in the near future, for instance.

To use the extension, take the following steps:

1. Add the [Microsoft.Azure.Relay.AspNetCore](https://www.nuget.org/packages/Microsoft.Azure.Relay.AspNetCore) assembly to your project.
2. [Create a Hybrid Connection](https://docs.microsoft.com/en-us/azure/service-bus-relay/relay-hybrid-connections-http-requests-dotnet-get-started)
3. After you have created the Hybrid Connection, find its "Shared Access Policies" portal tab and
add a new rule "listen" with the Listen-permission checked.
4. Once the rule has been created, select it and copy its connection string. 

The connection string contains all information to set up a listener. The [samples in the 
samples subfolder](./samples) take the connection string as input.

The `.UseAzureRelay()` hosting extension is available via the `Microsoft.Azure.Relay.AspNetCore` namespace.

You MUST remove all IISExpress references in the launchSettings.json of any ASP.NET Core application you create 
from a template. The IIS Express settings are incompatible with this extension.

```
using Microsoft.Azure.Relay.AspNetCore;


var host = new WebHostBuilder()
    .ConfigureLogging(factory => factory.AddConsole())
    .UseStartup<Startup>()
        .UseAzureRelay(options =>
        {
            options.UrlPrefixes.Add(connectionString);
        })
    .Build();

host.Run();
```

