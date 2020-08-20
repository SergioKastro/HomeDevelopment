# Introduction 
This project is an implementation for the DigitalTwin project on the Nyhamna facility. 

At the Nyhamna facility we have a Virtual Machine (3p-kognf-app01) where we have installed an Edge Device with the OPCUA Connector.
The OPCUA Connector gets the data for a list of sensors from the OPC Server. The Data is being sent to the Azure Service Event Hub.

The DigitalTwin to know the type of data (metadata for the unit of measurement) that each of the sensor is using, it needs to extract that information from the OPC Server.
This data (unit measurement), obviously, does not change often. So, to don't overload the OPCUA Connector and to split the work the DigitalTwin request to have a new software that will get this data.
Data:

	+ Dynamic tags: Those tags from the sensors which change often
			ns=4;s=0:FIC-50-0201:Z.X.Value
			ns=4;s=0:ST-95-0001:X.Value

	+ Static tags: Those tags from the sensors which does not change or does not change often
			ns=4;s=0:YT-32-5677Y:Name:Description
			ns=4;s=0:YT-32-5677Y:Z.X.Parameters.Max
			ns=4;s=0:YT-32-5677Y:Z.X.Parameters.Min
			ns=4;s=0:YT-32-5677Y:Z.X.Parameters.Unit
			
This program, which is a Windows Service, will get the data from the Static Tags.

# Getting Started
1.	Installation process
	How to install the windows service. Please follow the steps describe in the following document:
	+ \\GetStaticTagsValuesWindowsService\Documentation\Install GetStaticTags Windows Service.docx
	
2.	Software dependencies
	This software has been build using:

		- .Net Framework 4.6.2
		- OPCUA .Net Standard Library: https://github.com/OPCFoundation/UA-.NETStandard
		- TopShelf library to build windows services: https://github.com/Topshelf/Topshelf

3.	Software architecture
	Document that describes the architecture and design of this program:
	+ \\GetStaticTagsValuesWindowsService\Documentation\	GetTagsWindowsService.pptx


# Build and Test


