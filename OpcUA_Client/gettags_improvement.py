import sys
import time

from datetime import datetime
from opcua import Client
sys.path.insert(0, "..")

def getParameters():
    
        now = datetime.now()
        current_time = now.strftime("_%Y_%m_%d_%H_%M_%S")
        url = "opc.tcp://KPC22014549.kongsberg.master.int:53530/OPCUA/SimulationServer"  # Prosys server       
        filename = r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\GetTags Python App\Taglist.txt"
        outfile =  r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\GetTags Python App\resultTagList-" + current_time +".csv"

        taglist_file_opened = open(filename, "r")
        result_file_opened = open(outfile, "w")

        d = dict();
        d['opcServerUrl'] = url
        d['taglist_file'] = taglist_file_opened
        d['result_file']  = result_file_opened 

        return d

class opcUaClient():
    
    def __init__(self, url):
        self.session = Client(url)
        self.session.connect()
        self.session.load_type_definitions()  # load definition of server specific structures/extension objects 
             

    def getArrayNodesFromOpcServer(self, tagList, resultFile):
        localArrayNodes = []
        for x in tagList:
            tag=x.strip()
            try:  
                var = self.session.get_node(tag)                
                
                localArrayNodes.append(var)

            except Exception as e: 
                errorMessage = tag + " ,\t Cannot read tag from source. Error message: "  + str(e) 
                writeMessageInFile(resultFile, errorMessage)       
        return localArrayNodes

    def disconnect(self):
        self.session.disconnect()

def readNodeValues (var):
    message = ""
    error = False
    dataValue = var.get_data_value() 
       
    #2nd Read the tagId of the tag
    try:               
        tagid = var.nodeid.Identifier
    except Exception as e:
        error = True
        tagid = ". Error message: "  + str(e)
        message = message + ",\t error trying to read the tagId from the tag. "                     
    
    #4th Read the whole variant Value of the tag
    try:               
        variantValue = str(dataValue.Value).replace(",", ";")
    except Exception as e:
        error = True
        variantValue = ". Error message: "  + str(e)
        message = message + ",\t error trying to read the variant value from the tag. "

    #5th Read the Value of the tag
    try:               
        value = str(dataValue.Value.Value).replace(",", ";")
    except Exception as e:
        error = True
        value = ". Error message: "  + str(e)
        message = message + ",\t error trying to read the value from the tag. "

    #6th Read the StatusCode of the tag
    try:               
        statusCode = str(dataValue.StatusCode).replace(",", ";")
    except Exception as e:
        error = True
        statusCode = ". Error message: "  + str(e)
        message = message + ",\t error trying to read the statusCode from the tag. "

    #7th Read the timeStamp of the tag
    try:               
        timestamp =dataValue.SourceTimestamp.isoformat('T')+"Z"
    except Exception as e:
        error = True
        timestamp = ". Error message: "  + str(e)
        message = message + ",\t error trying to read the SourceTimestamp from the tag. "  


    if error:
        message = tagid + " ,\t " + value + " ,\t " + statusCode + " ,\t " + timestamp + ",\t Whole variant value: " + variantValue + ",\t Error message: " + message + " ."
    else:
        message = tagid + " ,\t " + value + " ,\t " + statusCode + " ,\t " + timestamp + ",\t Whole variant value: " + variantValue + " ."
    
    print("ReadNodeValues \n" , message)
    return message + "\n"

def writeMessageInFile(fileOpened, messageToWrite):
    fileOpened.write(messageToWrite)
    fileOpened.write("\n")

def closeProgram(client, sourceFile, resultFile):
    sourceFile.close()
    resultFile.close()
    client.disconnect()

if __name__ == "__main__":

    try:
        parameters = getParameters()

        # Write header
        header = "Tagid  ,\t Value ,\t StatusCode ,\t Timestamp ,\t Variant value "                   
        writeMessageInFile(parameters['result_file'], header)

        # create client and connect to server
        opcua_client = opcUaClient(parameters['opcServerUrl'])

        # Read the nodes from server 
        arrayNodes = opcua_client.getArrayNodesFromOpcServer(parameters['taglist_file'],parameters['result_file'])  

        # Read values from the Nodes
        for node in arrayNodes:
             nodeValue = readNodeValues (node)
             writeMessageInFile(parameters['result_file'], nodeValue)  

    finally:
        closeProgram(opcua_client,parameters['taglist_file'],parameters['result_file'])