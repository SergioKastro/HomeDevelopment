import sys
import logging
import time

from datetime import datetime
from opcua import Client
from opcua import ua

now = datetime.now()
current_time = now.strftime("_%Y_%m_%d_%H_%M_%S")

def num(s, defaultValue):
    try:
        if not s is None:
            return int(s)
        else :
            return defaultValue
    except :
        return defaultValue #if any error the programm will set subscriptions for defaultValue


def clientAndFiles():

    #Client run with arguments
    if len(sys.argv) != 6 and len(sys.argv) != 4:
        print("Syntax: " , sys.argv[0], "[OpcUaUrl]", "[Inputfile]", "[OutputfileName]", "[SusbcriptionTimeInMSec]", "[DelayTimeToReadTagsInSec]")
        sys.exit(-1)
        
    url = sys.argv[1]
    filename = sys.argv[2]
    outfile =  sys.argv[3] + current_time + ".csv"

    susbcriptionTimeInSec = num(sys.argv[4], 500) #This is the sampling rate of the subscription for each tag
    delayTimeToReadTagsInSec= num(sys.argv[5], 10) # This is the time that we will delay to read the values of the tags 

    client = Client(url)

    # client = Client("opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper")    
    # filename = r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\Taglist.txt"
    # outfile =  r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\resultTagList-" + current_time +".csv"
    # susbcriptionTimeInSec = num(500, 500)
    # delayTimeToReadTagsInSec= num(10, 10)
    
    client.connect()
    client.load_type_definitions()  # load definition of server specific structures/extension objects
    taglist_file_opened = open(filename, "r")
    result_file_opened = open(outfile, "w")

    d = dict();
    d['opcua_client'] = client
    d['taglist_file'] = taglist_file_opened
    d['result_file']  = result_file_opened
    d['susbcriptionTimeInSec'] = susbcriptionTimeInSec
    d['delayTimeToReadTagsInSec'] = delayTimeToReadTagsInSec
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

    def __init__(self, opcua_server, result_file, susbcriptionTimeInSec, delayTimeToReadTagsInSec, ua_node):
        self.ua_node = ua_node
        self.result_file = result_file
        self.message = ""

        handler = subHandler(self)
        sub = opcua_server.create_subscription(susbcriptionTimeInSec, handler)
        handle = sub.subscribe_data_change(self.ua_node)
        
        time.sleep(delayTimeToReadTagsInSec) #Default 10 sec to read the value of a tags
        
        sub.unsubscribe(handle)
        sub.delete()
        writeMessageInFile(self.result_file, self.message)        

if __name__ == "__main__":  

    logging.basicConfig(level=logging.ERROR)      

    clientAndFiles = clientAndFiles()

    try:               
        message = "Tag from file  ,\t Tagid  ,\t Value ,\t StatusCode ,\t Timestamp ,\t Variant value ,\t Error messages"                   
        writeMessageInFile(clientAndFiles['result_file'], message)
        
        for x in clientAndFiles['taglist_file']:
            tag=x.strip()
            try:  
                var = clientAndFiles['opcua_client'].get_node(tag) 

                start_Subscription(clientAndFiles['opcua_client'], clientAndFiles['result_file'], clientAndFiles['susbcriptionTimeInSec'], clientAndFiles['delayTimeToReadTagsInSec'], var)

            except Exception as e: 
                message = tag + " ,\t Cannot read tag from source. Error message: "  + str(e) 
                writeMessageInFile(clientAndFiles['result_file'], message)       
    finally:
        clientAndFiles['opcua_client'].disconnect()