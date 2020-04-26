We have created a new desktop application which will get the values of the tags which are in a text file.
The purpose of this program is to get the unit measurement from some tags which are needed for the Digital Twin application .
	ns=2;s=0:GE-32-5680:Z.X.Parameters.Unit
	ns=2;s=0:FIC-31-0114:Z.X.Parameters.Unit
	ns=2;s=0:FIC-31-0114:Z.Y.Parameters.Unit
	ns=2;s=0:TV-32-5635-BYPASS:Y.Parameters.Unit
	ns=2;s=0:KE-32-5802:Z.YR.Parameters.Unit
	ns=2;s=0:KE-32-5802:Z.XG.Parameters.Unit
	ns=2;s=0:KE-32-5802:XR.Parameters.Unit
	

The program has been written in python and it uses the official library OPCUA:
	OPCUA library: 
		from opcua import Client
		from opcua import ua
	https://readthedocs.org/projects/python-opcua/downloads/pdf/stable/
	
	
The program has two versions
	1.- Using Polling mode to get the values
	2.- Using the Publishing mode to get the values
We need to use the second version because publishing mode is the only way allowed to read the data from the tags (sensors) when running from Shell OPCUA Server.

Command:
	 gettags_subscription.exe [OPCUA Server Url] [file which taglist] [file to write resulys] [susbcriptionTimeInMs], [delayTimeToReadTagsInSc]
	 gettags_subscription.exe "opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper/" taglist.txt resultTagList 1000, 30
	
	Shell
	 gettags_subscription.exe "opc.tcp://3p-int-mat03:21381/MatrikonOpcUaWrapper/" taglist_Static_OnlyUnits.txt resultTagListOnlyUnits 5000, 30
	
	 gettags_subscription.exe "opc.tcp://3p-int-mat03:21381/MatrikonOpcUaWrapper/" taglist_Dynamic.txt resultTagListDynamic 5000, 30
	
	
Pseudo-algorithm for version number 2: Publishing mode:
	1.- Read the file which contains the tags (sensors) list.
	2.- Connect to the server and get the client (the session)
	3.- Read the nodes from server using the tag names from the file. We will get real nodes from the OPCUA Server 
	4.- Add all nodes into a subscription
	5.- Start the subscription
		The program will start to write the values into the result file
	6.- Delay the execution of the program to be able to read the values (default 30 sec)
	7.- Disconnect.


Create python executable in one file
https://datatofish.com/executable-pyinstaller/

Visual Studio Code 
pyinstaller --onefile [py file]