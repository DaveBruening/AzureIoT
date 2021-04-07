// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using CommandLine;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace Microsoft.Azure.Devices.Client.Samples {
    public class Program    {
        /* DTDL interface used: 
        https://github.com/Azure/iot-plugandplay-models/blob/main/dtmi/com/example/thermostat-1.json */
        /*original: private const string ModelId = "dtmi:com:example:Thermostat;1"; */
        //Davie changed from 1 to 2 to try to use Thermostat V2 device template:
        //Davie changed from 2 to 3 to try to use Thermostat V3 device template:
        private const string ModelId = "dtmi:com:example:Thermostat;8"; 
        private static ILogger s_logger;
        public static async Task Main(string[] args)  {
            /* Made by DavidB for tinkering:
             string strArgs = "les args: ";
            for (int i = 0; i < args.Length; i++)
                strArgs += args[i] + ", ";
            Console.WriteLine(strArgs);
            Console.ReadLine(); */
        // Parse application parameters
        Parameters parameters = null;
            ParserResult<Parameters> result = Parser.Default.ParseArguments<Parameters>(args)
                .WithParsed(parsedParams =>     {
                    parameters = parsedParams;
                })
                .WithNotParsed(errors =>   {
                    Environment.Exit(1);
                });
            s_logger = InitializeConsoleDebugLogger();
             if (!parameters.Validate(s_logger))   {
                 throw new ArgumentException("Required parameters are not set. Please recheck required " +
                     "variables by using \"--help\"");
             }
            /* Made by precious DavieB to test the logger: 
            s_logger.LogInformation("Like, uuummmm, this stuff is written by s_logger:");
            s_logger.LogWarning("Holy blizzard, my hair is on fire!!!");
            s_logger.LogCritical("Oh no, my computer is shooting sparks!!");
            s_logger.LogError("OMG, I forgot my name!"); */
            /* Made by sweet DavieB to see what's in parameters:*/
            s_logger.LogInformation($"DeviceId = {parameters.DeviceId}"); //sample-device-01
            //foreach (var parm in parameters )   //not enumerable
            s_logger.LogWarning($"DpsIdScope = {parameters.DpsIdScope}"); //0ne0026D59D
            //global.azure-devices-provisioning.net:
            s_logger.LogDebug($"DpsEndpoint = {parameters.DpsEndpoint}");   
            s_logger.LogWarning($"DeviceSecurityType={parameters.DeviceSecurityType}");  //DPS
            //v1roDh7dUDcP/iQNdEI3JL8oFaZl895MkVLvleZ/iac=:
            s_logger.LogCritical($"DeviceSymmetricKey = {parameters.DeviceSymmetricKey}");    
            //Console.WriteLine($"ApplicationRunningTime = {parameters.ApplicationRunningTime}");   //blank
            var runningTime = parameters.ApplicationRunningTime != null
                ? TimeSpan.FromSeconds((double)parameters.ApplicationRunningTime)
                : Timeout.InfiniteTimeSpan;
            s_logger.LogInformation("Press Control+C to quit the sample.");
            using var cts = new CancellationTokenSource(runningTime);
            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                cts.Cancel();
                s_logger.LogInformation("Sample execution cancellation requested; will exit.");
            };
            //Console.ReadLine();   added by Darling DavieB
            s_logger.LogDebug($"Set up the device client.");
            using DeviceClient deviceClient = await SetupDeviceClientAsync(parameters, cts.Token);
            var sample = new ThermostatSample(deviceClient, s_logger);
            await sample.PerformOperationsAsync(cts.Token);  
        }
        private static ILogger InitializeConsoleDebugLogger()  {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                /*.AddFilter(level => level >= LogLevel.Debug)  
                 Davie Darling replaced the line above w/ the one below
                since I guessing they do the same thing, just to be a smart alec!*/
                .SetMinimumLevel(LogLevel.Debug) 
                .AddConsole(options => {
                    //Gorgeous Davie changed from "[MM-dd-yyyy HH:mm:ss]"
                    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]"; 
                })
            ;});
            return loggerFactory.CreateLogger<ThermostatSample>();
        }
        private static async Task<DeviceClient> SetupDeviceClientAsync(Parameters parameters, 
            CancellationToken cancellationToken)   {
            DeviceClient deviceClient;
            switch (parameters.DeviceSecurityType.ToLowerInvariant())    {
                case "dps":     //Georgeous Davie's note: seems to use DPS, not connection string
                    s_logger.LogDebug($"Initializing via DPS");
                    DeviceRegistrationResult dpsRegistrationResult = 
                        await ProvisionDeviceAsync(parameters, cancellationToken);
                    var authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(
                        dpsRegistrationResult.DeviceId, parameters.DeviceSymmetricKey);
                    deviceClient = InitializeDeviceClient(dpsRegistrationResult.AssignedHub, authMethod);
                    break;
                case "connectionstring":
                    s_logger.LogDebug($"Initializing via IoT Hub connection string");
                    deviceClient = InitializeDeviceClient(parameters.PrimaryConnectionString);
                    break;
                default:
                    throw new ArgumentException($"Unrecognized value for device provisioning received: " + 
                    $"{parameters.DeviceSecurityType}." +
                        $" It should be either \"dps\" or \"connectionString\" (case-insensitive).");
            }
            return deviceClient;
        }
        // Provision a device via DPS, by sending the PnP model Id as DPS payload.
        private static async Task<DeviceRegistrationResult> ProvisionDeviceAsync(Parameters parameters, 
            CancellationToken cancellationToken)
        {
            SecurityProvider symmetricKeyProvider = new SecurityProviderSymmetricKey(
                parameters.DeviceId, parameters.DeviceSymmetricKey, null);
            ProvisioningTransportHandler mqttTransportHandler = new ProvisioningTransportHandlerMqtt();
            ProvisioningDeviceClient pdc = ProvisioningDeviceClient
                .Create(parameters.DpsEndpoint, parameters.DpsIdScope,
                symmetricKeyProvider, mqttTransportHandler);
            var pnpPayload = new ProvisioningRegistrationAdditionalData        {
                JsonData = $"{{ \"modelId\": \"{ModelId}\" }}",
            };
            return await pdc.RegisterAsync(pnpPayload, cancellationToken);
        }
        // Initialize the device client instance using connection string based authentication, 
        // over Mqtt protocol (TCP, with fallback over Websocket) and setting the ModelId into ClientOptions.
        // This method also sets a connection status change callback, that will get triggered any time the 
        //device's connection status changes.
        //Darling DavieB's note: seems like InitializeDeviceClient w/ 2 params is being used instead of this one:
        private static DeviceClient InitializeDeviceClient(string deviceConnectionString)   {
            var options = new ClientOptions    {
                ModelId = ModelId,
            };
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, 
                TransportType.Mqtt, options);
            deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
            {
                s_logger.LogDebug($"Connection status change registered - status={status}, reason={reason}.");
            });
            return deviceClient;
        }
        // Initialize the device client instance using symmetric key based authentication, over Mqtt protocol 
        //(TCP, with fallback over Websocket) and setting the ModelId into ClientOptions.
        // This method also sets a connection status change callback, that will get triggered any time the device's 
        //connection status changes.
        private static DeviceClient InitializeDeviceClient(string hostname, 
            IAuthenticationMethod authenticationMethod)   {
            var options = new ClientOptions   {
                ModelId = ModelId,
            };
            DeviceClient deviceClient = DeviceClient.Create(hostname, authenticationMethod, TransportType.Mqtt, options);
            deviceClient.SetConnectionStatusChangesHandler((status, reason) =>    {
                s_logger.LogDebug($"Connection status change registered - status={status}, reason={reason}.");
            });
            return deviceClient;
        }
    }
}
