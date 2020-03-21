import sys
import logging
import time
import os


from datetime import datetime
from opcua import Client
from opcua import ua


now = datetime.now()
current_time = now.strftime("_%Y_%m_%d_%H_%M_%S")

def clientAndFiles():

    #Client run with arguments
    if len(sys.argv) != 4:
        print("Syntax: " , sys.argv[0], "[OpcUaUrl]", "[Inputfile]", "[OutputfileName]")
        sys.exit(-1)
        
    url = sys.argv[1]
    filename = sys.argv[2]
    outfile =  sys.argv[3] + current_time + ".csv"
    client = Client(url)

    # client = Client("opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper")    
    # filename = r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\Taglist.txt"
    # outfile =  r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\resultTagList-" + current_time +".txt"


    d = dict();
    d['opcua_client'] = Client("opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper")
    d['taglist_file'] = filename
    d['result_file']  = outfile
    return d   

def readNodeValues (var, dataValue):
    message = ""
    error = False
       
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

    #7th Read the timeStampt of the tag
    try:               
        timestamp =dataValue.SourceTimestamp.isoformat('T')+"Z"
    except Exception as e:
        error = True
        timestamp = ". Error message: "  + str(e)
        message = message + ",\t error trying to read the SourceTimestamp from the tag. "  


    if error:
        message = tag + " ,\t " + tagid + " ,\t " + value + " ,\t " + statusCode + " ,\t " + timestamp + ",\t Whole variant value: " + variantValue + ",\t Error message: " + message + " ."
    else:
        message = tag + " ,\t " + tagid + " ,\t " + value + " ,\t " + statusCode + " ,\t " + timestamp + ",\t Whole variant value: " + variantValue + " ."
    
    print("ReadNodeValues \n" , message)
    return message + "\n"

def writeMessageInFile(fileOpened, message):

    line = message  
    fileOpened.write(line)
    fileOpened.write("\n")

class subHandler(object):

    """
    Subscription Handler. To receive events from server for a subscription
    data_change and event methods are called directly from receiving thread.
    Do not do expensive, slow or network operation there. Create another 
    thread if you need to do such a thing
    """
    
    def __init__(self, obj):
        self.obj = obj        
        
    def datachange_notification(self, node, val, data):  
        
        line = readNodeValues(node, data.monitored_item.Value)        
        self.obj.message = self.obj.message + line    

class start_Subscription(object):

    def __init__(self, opcua_server, result_file, ua_node):
        self.ua_node = ua_node
        self.result_file = result_file
        self.message = ""

        handler = subHandler(self)
        sub = opcua_server.create_subscription(500, handler)
        handle = sub.subscribe_data_change(self.ua_node)
        
        time.sleep(10) #10 sec to read the value of a tags
        
        sub.unsubscribe(handle)
        sub.delete()
        writeMessageInFile(self.result_file, self.message)        

if __name__ == "__main__":
    logging.basicConfig(level=logging.WARN)
    
    clientAndFiles = clientAndFiles()

    try:
        clientAndFiles['opcua_client'].connect()
        clientAndFiles['opcua_client'].load_type_definitions()  # load definition of server specific structures/extension objects
        taglist_file_opened = open(clientAndFiles['taglist_file'], "r")
        result_file_opened = open(clientAndFiles['result_file'], "w")        
        
        message = "Tag from file  ,\t Tagid  ,\t Value ,\t StatusCode ,\t Timestamp ,\t Variant value ,\t Error messages"                   
        writeMessageInFile(result_file_opened, message)
        
        for x in taglist_file_opened:
            tag=x.strip()
            try:  
                var = clientAndFiles['opcua_client'].get_node(tag) 

                start_Subscription(clientAndFiles['opcua_client'], result_file_opened, var)

            except Exception as e: 
                message = tag + " ,\t Cannot read tag from source. Error message: "  + str(e) 
                writeMessageInFile(result_file_opened, message)       
    finally:
        clientAndFiles['opcua_client'].disconnect()