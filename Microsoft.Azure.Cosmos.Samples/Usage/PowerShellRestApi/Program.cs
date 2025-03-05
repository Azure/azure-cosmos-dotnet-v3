

namespace PowerShellRestApi
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using System.Threading.Tasks;

    // ----------------------------------------------------------------------------------------------------------
    // This class runs the power shell scripts for database/container/items basic operations.
    // Sole purpose of this is user can take helps from the scripts if they having issue 
    // in running REST API via PowerShell.
    // However we recommend to use CosmosClient SDK for any user interaction with Cosmos DB services
    // instead of directly using REST API.
    // ----------------------------------------------------------------------------------------------------------
    public class Program
    {
        public static async Task Main(string[] _)
        {
            try
            {
                Console.WriteLine($"\n1. Database crud operations started.");
                await DatabaseOperations();
                Console.WriteLine($"\n2. Container crud operations started.");
                await ContainerOperations();
                Console.WriteLine($"\n3. Item crud operations started.");
                await ItemOperations();
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("End of demo, press any key to exit.");
                    Console.ReadKey();
                }
            }

        }

        private static async Task DatabaseOperations()
        {
            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\CreateDB.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("CreateDBSuccess"))
                {
                    Console.WriteLine("CreateDatabase successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\ReadDB.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("ReadDBSuccess"))
                {
                    Console.WriteLine("ReadDatabase successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\DeleteDB.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("DeleteDBSuccess"))
                {
                    Console.WriteLine("DeleteDatabase successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };
        }

        private static async Task ContainerOperations()
        {
            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\CreateDB.ps1"));
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\CreateContainer.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("CreateContainerSuccess"))
                {
                    Console.WriteLine("CreateContainer successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\ReplaceContainer.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("ReplaceContainerSuccess"))
                {
                    Console.WriteLine("ReplaceContainer successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\ReadContainer.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("ReadContainerSuccess"))
                {
                    Console.WriteLine("ReadContainer successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\DeleteContainer.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("DeleteContainerSuccess"))
                {
                    Console.WriteLine("DeleteContainer successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };
        }

        private static async Task ItemOperations()
        {
            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\CreateContainer.ps1"));

                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\CreateItem.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("CreateItemSuccess"))
                {
                    Console.WriteLine("CreateItem successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\ReplaceItem.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("ReplaceItemSuccess"))
                {
                    Console.WriteLine("ReplaceItem successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\ReadItem.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("ReadItemSuccess"))
                {
                    Console.WriteLine("ReadItem successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.AddScript(File.ReadAllText("PowerShellScripts\\DeleteItem.ps1"));
                PSDataCollection<PSObject> PSOutput = await powerShell.InvokeAsync();
                if (PSOutput.Count == 1 && PSOutput[0].ToString().Equals("DeleteItemSuccess"))
                {
                    Console.WriteLine("DeleteItem successful");
                }
                else
                {
                    throw new Exception(PSOutput[0].ToString());
                }
            };
        }
    }
}
