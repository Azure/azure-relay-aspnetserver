# Relayed ASP.NET MVC Server Application Sample

This application shows how to run a regular ASP.NET Core application through the
Azure Relay.

The application is a a completely regular ASP.NET Core application, with the following changes applied:

* References to the NuGet packages [Microsoft.Azure.Relay.AspNetCore] and [Microsoft.Azure.Relay]
  * As checked in, the sample references the source code inside this repo but can be easily altered
    to refer to the signed NuGet assembly
* In *Program.cs*, the `UseAzureRelay()` extension was added with a reference to an Azure Relay connection string.
* The IISExpress configuration was removed from the launch settings since the application listens remotely
  and not locally.
  
To run the sample you need an Azure Relay namespace and a Hybrid Connection Relay behind
which the application will be hosted. Applications can currently not own the root of 
the namespace, but need to be hosted on a path, similar to vdir applications in IIS or
other web services. The access details for the Relay namespace and the Hybrid Connections 
relay are summarized in a connection string.

From the client-side, you will be able to access the application via

`https://{namespace}.servicebus.windows.net/{hybrid-connection-name}` 

Vanity domains are currently not supported, and you cannot CNAME the domain name in DNS. 

## How do I get a connection string?

To run this sample, you need a connection string that includes the entity path of a Hybrid Connection entity 
in the connection string: `Endpoint=sb://mynamespace.servicebus.windows.net;...;EntityPath=app

You can copy that connection string, including all the required key information, from the portal.

Here's how you get there:

1. [Create a Relay namespace](https://docs.microsoft.com/en-us/azure/service-bus-relay/relay-create-namespace-portal)
   if you don't have one yet.
2. Go to the **Overview** tab of your new namespace and choose "**+ Hybrid Connection**"
3. In the panel that opens, enter a name for your Relay and, for this sample, **uncheck** the "**Requires Client 
   Authorization**" box. This will allow clients (including browsers) to interact with your server without 
   having to present an access token to the Relay itself. Click "Create".
4. Once the new Relay has been created, select it from the list in the "**Overview**" tab to navigate to 
   its detail page. There, find the "**Shared access policies**" tab and click it.
5. Click "**+ Add**", and in the panel that opens, enter the name "sendlisten" for a new policy rule and check the 
   "Send" and "Listen" boxes for the rights associated with the new rule. Click "Create".
6. Wait while the rule is being created. When it appears, select it. In the panel that opens, find the
   "**Connection String - Primary Key**" box and click the button next to it to copy the connection string
   to your clipboard. That is the connection string you'll need.

To make the sample work, you can pass the connection string as the sole argument on the command line, or you 
can set the SB_HC_CONNECTIONSTRING environment variable before running the sample.


