Hello.  This is a Core Console App that interacts with Azure IoT (Internet Of Things) Central via commands, properties, and telemetries.
It's based on [__Tutorial - Connect a generic client app to Azure IoT Central | Microsoft Docs__](https://docs.microsoft.com/en-us/azure/iot-central/core/tutorial-connect-device?pivots=programming-language-csharp).  In addition to C# and JSON programming, it involved a lot of configuration in Azure IoT Central.<br/>
I added some neat features :thumbsup:, ordered from basic to complex:<br/> 
-	Changing the device's reporting interval from every 5 seconds to every minute<br/>
-	Changing the device's temperature telemetry from a constant value each debugging session to varying each reporting interval<br/> 
-	Displaying warning and critical messages in the console when temperature goes above and below thresholds<br/>
-	Modifying JSON files and using Cloud Shell to create new devices for new versions of the device template<br/>
-	Making a telemetry for pressure<br/>
-	Making a property for the minimum temperature since the last reboot<br/>
-	Adding the maximum pressure and the number of telemetries in the last debug session to the max-min report command<br/>
-	Making the pressureRptDavieB command to display average pressure, temperature/pressure ratio, and the sum of the pressure telemetries<br/><br/> 
P.S. Please excuse my silly comments.  They're just a way to keep programming fun :smiley:.
