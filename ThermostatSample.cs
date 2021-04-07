// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Microsoft.Azure.Devices.Client.Samples {
    internal enum StatusCode  {
        Completed = 200,
        InProgress = 202,
        NotFound = 404,
        BadRequest = 400
    }
    public class ThermostatSample    {
        private readonly Random _random = new Random();
        private double _temperature = 0d;
        private double _maxTemp = 0d;
        //_minTemp, _pressure & _randomPres made by Adorable DavieB:
        Random _randomPres = new Random();
        private double _minTemp = 100d; 
        private double _pressure = 0d;
        // Dictionary to hold the temperature updates sent over.
        // NOTE: Memory constrained devices should leverage storage capabilities of an external 
        //service to store this information and perform computation.
        // See https://docs.microsoft.com/en-us/azure/event-grid/compare-messaging-services for more details.
        private readonly Dictionary<DateTimeOffset, double> _temperatureReadingsDateTimeOffset = 
            new Dictionary<DateTimeOffset, double>();
        //Sassy DavieB added _pressureReadingsDateTimeOffset just to be a tease:
        private readonly Dictionary<DateTimeOffset, double> _pressureReadingsDateTimeOffset = 
            new Dictionary<DateTimeOffset, double>();
        private readonly DeviceClient _deviceClient;
        private readonly ILogger _logger;
        public ThermostatSample(DeviceClient deviceClient, ILogger logger)    {
            _deviceClient = deviceClient ?? throw new ArgumentNullException($"{nameof(deviceClient)} cannot be null.");
            _logger = logger ?? LoggerFactory.Create(builer => builer.AddConsole()).CreateLogger<ThermostatSample>();
        }
        public async Task PerformOperationsAsync(CancellationToken cancellationToken)    {
            // This sample follows the following workflow:
            // -> Set handler to receive "targetTemperature" updates, and send the received update over reported property.
            // -> Set handler to receive "getMaxMinReport" command, and send the generated report as command response.
            // -> Periodically send "temperature" over telemetry.
            // -> Send "maxTempSinceLastReboot" over property update, when a new max temperature is set.
            _logger.LogDebug($"Set handler to receive \"targetTemperature\" updates.");
            await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(TargetTemperatureUpdateCallbackAsync, 
                _deviceClient, cancellationToken);
            _logger.LogDebug($"Set handler for \"getMaxMinReport\" command.");
            await _deviceClient.SetMethodHandlerAsync("getMaxMinReport", HandleMaxMinReportCommand, 
                _deviceClient, cancellationToken);
            /*added by scrumptious DavieB.  Setting the 1st param to the latest pressure Command 
             * name in Thermostat.json works: */
            await _deviceClient.SetMethodHandlerAsync("pressureRptDavieB5", HandlePressureRptCmdDavieB, 
                _deviceClient, cancellationToken);
            bool temperatureReset = true;
            while (!cancellationToken.IsCancellationRequested)            {
                //if (temperatureReset)  {  If commented by wacky DaveyB to try to get a new temp every  5 seconds
                // Generate a random value between 5.0°C and 45.0°C for the current temperature reading.
                _temperature = Math.Round(_random.NextDouble() * 40.0 + 5.0, 1);
                //Cheruby Davie added _pressure:
                //_pressure = _temperature + 5;   
                _pressure = Math.Round(_randomPres.NextDouble() * 50.0 + 25, 1); //between 25 - 75
                temperatureReset = false;
                //}
                await SendTemperatureAsync();
                //wacked-out DaveyB changed from every 5 seconds (5 * 1000) to once a minute:
                await Task.Delay(60 * 1000);
            }
        }
        // The desired property update callback, which receives the target temperature as a desired property update,
        // and updates the current temperature value over telemetry and reported property update.
        //Sweet Davie doesn't think he needs to do add code for pressure since he doesn't want a target pressure:
        private async Task TargetTemperatureUpdateCallbackAsync(
            TwinCollection desiredProperties, object userContext)  {
            const string propertyName = "targetTemperature";
            (bool targetTempUpdateReceived, double targetTemperature) = 
                GetPropertyFromTwin<double>(desiredProperties, propertyName);
            if (targetTempUpdateReceived)  {
                _logger.LogDebug($"Property: Received - {{ \"{propertyName}\": {targetTemperature}°C }}.");
                string jsonPropertyPending = $"{{ \"{propertyName}\": {{ \"value\": {_temperature}, " +
                    $"\"ac\": {(int)StatusCode.InProgress}, " +
                    $"\"av\": {desiredProperties.Version} }} }}";
                var reportedPropertyPending = new TwinCollection(jsonPropertyPending);
                await _deviceClient.UpdateReportedPropertiesAsync(reportedPropertyPending);
                _logger.LogDebug($"Property: Update - {{\"{propertyName}\": {targetTemperature}°C }}" +
                    $"is {StatusCode.InProgress}.");
                // Update Temperature in 2 steps:
                double step = (targetTemperature - _temperature) / 2d;
                for (int i = 1; i <= 2; i++)   {
                    _temperature = Math.Round(_temperature + step, 1);
                    await Task.Delay(6 * 1000);
                }
                string jsonProperty = $"{{ \"{propertyName}\": {{ \"value\": {_temperature}, \"ac\": " +
                    $"{(int)StatusCode.Completed}, " +
                    $"\"av\": {desiredProperties.Version}, \"ad\": \"Successfully updated target temperature\" }} }}";
                var reportedProperty = new TwinCollection(jsonProperty);
                await _deviceClient.UpdateReportedPropertiesAsync(reportedProperty);
                _logger.LogDebug($"Property: Update - {{\"{propertyName}\": {_temperature}°C }} " +
                    $"is {StatusCode.Completed}.");
            }
            else   {
                _logger.LogDebug($"Property: Received an unrecognized property update from service:" +
                    $"\n{desiredProperties.ToJson()}");
            }
        }
        private static (bool, T) GetPropertyFromTwin<T>(TwinCollection collection, string propertyName)    {
            return collection.Contains(propertyName) ? (true, (T)collection[propertyName]) : (false, default);
        }
        /* The callback to handle "pressureRptDavieB" command.  It looks like the request param contains 
         since-date in Json format.*/ 
        private Task<MethodResponse> HandlePressureRptCmdDavieB (MethodRequest request, object userContext) {
            try {
                DateTime sinceInUtc = JsonConvert.DeserializeObject<DateTime>(request.DataAsJson);
                var sinceInDateTimeOffset = new DateTimeOffset(sinceInUtc);
                _logger.LogDebug($"Command: Received - generating pressureRptDavieB report since " +
                    $"{sinceInDateTimeOffset.LocalDateTime}.");
                Dictionary<DateTimeOffset, double> pressureReadings = _pressureReadingsDateTimeOffset
                    .Where(i => i.Key > sinceInDateTimeOffset).ToDictionary(i => i.Key, i => i.Value);
                Dictionary<DateTimeOffset, double> filteredReadings = _temperatureReadingsDateTimeOffset
                    .Where(i => i.Key > sinceInDateTimeOffset).ToDictionary(i => i.Key, i => i.Value);
                if (filteredReadings!=null && filteredReadings.Any()) {
                    var report = new {
                        avgPres = Math.Round(pressureReadings.Values.Average(), 1),
                        tempPresRatio = Math.Round(filteredReadings.Values.Average() / 
                            pressureReadings.Values.Average(), 1),
                        sumPres = Math.Round(pressureReadings.Values.Sum(), 1)
                    };
                    _logger.LogDebug($"Command: pressureRptDavieB since {sinceInDateTimeOffset.LocalDateTime}:"
                        + $"avgPres = {report.avgPres}");
                    byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report));
                    return Task.FromResult(new MethodResponse(responsePayload, (int)StatusCode.Completed));
                }
                _logger.LogDebug($"Command: No readings found since {sinceInDateTimeOffset.LocalDateTime} " +
                    "for report pressureRptDavieB.  So there, nah!");
                return Task.FromResult(new MethodResponse((int)StatusCode.NotFound));
            }
            catch (JsonReaderException ex) {
                _logger.LogError($"Command input is invalid: {ex.Message}.");
                return Task.FromResult(new MethodResponse((int)StatusCode.BadRequest));
            }
        }
        // The callback to handle "getMaxMinReport" command. This method will returns the max, 
        // min and average temperature from the specified time to the current time.
        private Task<MethodResponse> HandleMaxMinReportCommand(MethodRequest request, object userContext) {
            try   {
                DateTime sinceInUtc = JsonConvert.DeserializeObject<DateTime>(request.DataAsJson);
                var sinceInDateTimeOffset = new DateTimeOffset(sinceInUtc);
                _logger.LogDebug($"Command: Received - Generating max, min and avg temperature report since " +
                    $"{sinceInDateTimeOffset.LocalDateTime}.");
                Dictionary<DateTimeOffset, double> filteredReadings = _temperatureReadingsDateTimeOffset
                    .Where(i => i.Key > sinceInDateTimeOffset).ToDictionary(i => i.Key, i => i.Value);
                //pressureReadings added by Fresh DavieB-B-Boy-ee:
                Dictionary<DateTimeOffset, double> pressureReadings = _pressureReadingsDateTimeOffset
                    .Where(j => j.Key > sinceInDateTimeOffset).ToDictionary(j => j.Key, j => j.Value);
                if (filteredReadings != null && filteredReadings.Any())     {
                    var report = new     {
                        maxTemp = filteredReadings.Values.Max<double>(),  
                        minTemp = filteredReadings.Values.Min<double>(),
                        avgTemp = filteredReadings.Values.Average(),  startTime = filteredReadings.Keys.Min(),
                        endTime = filteredReadings.Keys.Max(), 
                        maxPres = pressureReadings.Values.Max<double>() //maxPres added by DavieB
                        , countChocula = pressureReadings.Count //countChocula added by you know who
                    };
                    _logger.LogDebug($"Command: MaxMinReport since {sinceInDateTimeOffset.LocalDateTime}:" +
                        $" maxTemp={report.maxTemp}, minTemp={report.minTemp}, avgTemp={report.avgTemp}, " +
                        $"startTime={report.startTime.LocalDateTime}, endTime={report.endTime.LocalDateTime}");
                    byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report));
                    return Task.FromResult(new MethodResponse(responsePayload, (int)StatusCode.Completed));
                }
                _logger.LogDebug($"Command: No relevant readings found since " +
                    $"{sinceInDateTimeOffset.LocalDateTime}, cannot generate any report.");
                return Task.FromResult(new MethodResponse((int)StatusCode.NotFound));
            }
            catch (JsonReaderException ex)       {
                _logger.LogDebug($"Command input is invalid: {ex.Message}.");
                return Task.FromResult(new MethodResponse((int)StatusCode.BadRequest));
            }
        }
        /* Send temperature updates over telemetry. The sample also sends the value of max temperature 
        since last reboot over reported property update. */
        private async Task SendTemperatureAsync()     {
            await SendTemperatureTelemetryAsync();
            double maxTemp = _temperatureReadingsDateTimeOffset.Values.Max<double>();
            if (maxTemp > _maxTemp)    {
                _maxTemp = maxTemp;
                /*added by Precious DavieB:
                await UpdateMaxMinTemperatureSinceLastRebootAsync("max"); */
                await UpdateMaxTemperatureSinceLastRebootAsync();
            }
            /* Sweetie-pie DavidB added the min temp lines below: */
            double miniTemp = _temperatureReadingsDateTimeOffset.Values.Min<double>();
            if (miniTemp < _minTemp) {
                _minTemp = miniTemp;
                /*added by Precious DavieB:
                await UpdateMaxMinTemperatureSinceLastRebootAsync("min"); */
                await UpdateMinTemperatureSinceLastRebootAsync();
            }
        }
        // Send temperature update over telemetry.
        private async Task SendTemperatureTelemetryAsync()        {
            const string telemetryName = "temperature";
            /*original: before Davie-boy got his scruffy hands on it:
            string telemetryPayload = $"{{ \"{telemetryName}\": {_temperature} }}"; */
            string telemetryPayload = $"{{ \"{telemetryName}\": {_temperature}, \"Pressure\": {_pressure} }}";
            /* Sweet-boy DavieB added _pressure to telemetryPayload, and telemetryPayload to the logger: */
            _logger.LogInformation("telemetryPayload: " + telemetryPayload);
            using var message = new Message(Encoding.UTF8.GetBytes(telemetryPayload))     {
                ContentEncoding = "utf-8",
                ContentType = "application/json",
            };
            await _deviceClient.SendEventAsync(message);
            /*Precious DavieB added the funky Switch.  Originally it was just the boring _logger.LogDebug msg:*/
            switch (_temperature) {
                case double d when (d > 35):
                    _logger.LogWarning("Telemetry: Holy Moly, this crazy device needs to simmer down now! "
                        + $"- {telemetryName}: {_temperature}°C .");
                    break;
                case double d when (d < 20):
                    _logger.LogCritical("Telemetry: Oh snap! This device needs some warm lovin' " +
                        $"since it's getting COLD - {telemetryName}: {_temperature}°C");
                    break;
                default:
                    _logger.LogDebug($"Telemetry: Sent - {{ \"{telemetryName}\": {_temperature}°C }}.");
                    break;
            }
            _temperatureReadingsDateTimeOffset.Add(DateTimeOffset.Now, _temperature);
            _pressureReadingsDateTimeOffset.Add(DateTimeOffset.Now, _pressure); //added by that sweet guy!
        }
        // Send temperature over reported property update.
        private async Task UpdateMaxTemperatureSinceLastRebootAsync()  {
            const string propertyName = "maxTempSinceLastReboot";
            var reportedProperties = new TwinCollection();
            reportedProperties[propertyName] = _maxTemp;
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            _logger.LogDebug($"Property: Update - {{ \"{propertyName}\": {_maxTemp}°C }} is {StatusCode.Completed}.");
        }
        /*UpdateMinTemperatureSinceLastRebootAsync made by Precious DavieB: */
        private async Task UpdateMinTemperatureSinceLastRebootAsync() {
            const string propertyName = "minTempSinceLastReboot";
            var reportedProperties = new TwinCollection();
            reportedProperties[propertyName] = _minTemp;
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            _logger.LogDebug($"Property: Update - {{ \"{propertyName}\": {_minTemp}°C }} is {StatusCode.Completed}.");
        }
        /*made by snuggly DavieB so we wouldn't have 2 separate methods, but when there's a new min,
         *  it writes the max too in https://gipc6xfwcimt5.azureiotcentral.com/devices/details/sample-device-01/rawdata
        private async Task UpdateMaxMinTemperatureSinceLastRebootAsync(string maxOrMin) {
            var reportedProperties = new TwinCollection();
            string propertyName = "maxTempSinceLastReboot";
            reportedProperties[propertyName] = _maxTemp;
            if (maxOrMin == "min") {
                propertyName = "minTempSinceLastReboot";
                reportedProperties[propertyName] = _minTemp;
            }
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            _logger.LogDebug($"Property: Update - {{ \"{propertyName}\": " + 
                $"{reportedProperties[propertyName]}°C }} is {StatusCode.Completed}.");
        } */
    }
}
