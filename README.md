Hello.  This is a Core Console App that interacts with Azure IoT (Internet Of Things) Central via commands, properties, and telemetry.
It's based on [__Tutorial - Connect a generic client app to Azure IoT Central | Microsoft Docs__](https://docs.microsoft.com/en-us/azure/iot-central/core/tutorial-connect-device?pivots=programming-language-csharp).<br/>
I added some neat features :thumbsup:, like:<br/> 
-	<br/>
-	<br/>
-	Mading the pressureRptDavieB command to display average pressure, temperature/pressure ratio, and the sum of the pressure telemetries.<br/> 
-	Mading a telemetry for pressure.<br/>
-	Mading a property for the min temperature since last reboot.<br/>
-	Changing the device's temperature telemetry from a constant value each debugging session to varying each reporting interval<br/> 
-	Changing the device's reporting interval from every 5 seconds to every minute. (Alright, that's pretty basic, but it's one of the 
	  first changes I made, so it was pretty neat!)<br/>

P.S. Please excuse my silly comments.  They're just a way to keep programming fun :smiley:.
