# Relayed ASP.NET Core self-host sample

This sample demonstrates a minimal self-hosted application that leverages the ASP.NET Core
base infrastructure. The application sets up a host via the Relay using the `UseAzureRelay` 
extension and then processes a simple request. 

Most ASP.NET Core based web app and web services frameworks and extensions should be able 
to work with the Relay extension. 

To run the sample you need an Azure Relay namespace and a Hybrid Connection Relay behind
which the application will be hosted. Applications can currently not own the root of 
the namespace, but need to be hosted on a path, similar to vdir applications in IIS or
other web services. The access details for the Relay namespace and the Hybrid Connections 
relay are summarized in a conection string.

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
