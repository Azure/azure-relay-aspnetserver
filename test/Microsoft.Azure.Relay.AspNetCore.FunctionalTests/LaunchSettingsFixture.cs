using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Azure.Relay.AspNetCore
{
    public class LaunchSettingsFixture : IDisposable
    {
        public LaunchSettingsFixture()
        {
            string filename = "Properties\\launchSettings.json";
            if (File.Exists(filename))
            {
                using (var file = File.Open(filename, FileMode.Open))
                {
                    var json = JsonDocument.Parse(file).RootElement;

                    var variables = json.
                        GetProperty("profiles").EnumerateObject()
                        //select a proper profile here
                        .SelectMany(profile => profile.Value.EnumerateObject())
                        .Where(prop => prop.Name == "environmentVariables")
                        .SelectMany(prop => prop.Value.EnumerateObject())
                        .ToList();

                    foreach (var variable in variables)
                    {
                        Environment.SetEnvironmentVariable(variable.Name, variable.Value.GetString());
                    }
                }
            }
        }

        public void Dispose()
        {
            // ... clean up
        }
    }
}
