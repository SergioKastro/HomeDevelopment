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

def getParameters():
    
        # Client run with arguments
        # if len(sys.argv) != 8 and len(sys.argv) != 4:
        #     print("Syntax: " , sys.argv[0], "[OpcUaUrl]", "[Inputfile]", "[OutputfileName]", "[SusbcriptionTimeInSec]", "[DelayTimeToReadTagsInSec]")
        #     sys.exit(-1)
            
        # url = sys.argv[1]
        # filename = sys.argv[2]
        # outfile =  sys.argv[3] + current_time + ".csv"

        # susbcriptionTimeInSec = num(sys.argv[4], 1000) #This is the sampling rate of the subscription for each tag in MSEC
        # delayTimeToReadTagsInSec= num(sys.argv[5], 30) # This is the time that we will delay to read the values of the tags in SEC
        # batchLoadSizeItems = num(sys.argv[6], 200) # This is the size of the batch. How many tags should we load at a time (1000 tags)
        # delayLoadBatchTimeInSec = num (sys.argv[7], 60) # This is the time in milleseconds that we will wait to load each of the batches

        url = "opc.tcp://KPC22014549.kongsberg.master.int:53530/OPCUA/SimulationServer"  # Prosys server
        # url ="opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper" # Matrikon server
        filename = r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\GetTags Python App\Taglist.txt"
        outfile =  r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\GetTags Python App\resultTagList-" + current_time +".csv"
        susbcriptionTimeInSec = num(15, 10)
        delayTimeToReadTagsInSec = num(60, 30)
        batchLoadSizeItems = num(100, 200)
        delayLoadBatchTimeInSec = num (0, 60)        
        
        taglist_file_opened = open(filename, "r")
        result_file_opened = open(outfile, "w")

        d = dict();
        d['opcServerUrl'] = url
        d['taglist_file'] = taglist_file_opened
        d['result_file']  = result_file_opened
        d['susbcriptionTimeInSec'] = susbcriptionTimeInSec
        d['delayTimeToReadTagsInSec'] = delayTimeToReadTagsInSec
        d['batchLoadSizeItems'] = batchLoadSizeItems
        d['delayLoadBatchTimeInSec'] = delayLoadBatchTimeInSec

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

def batch(iterable, n=1):
    l = len(iterable)
    for ndx in range(0, l, n):
        yield iterable[ndx:min(ndx + n, l)]

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
        writeMessageInFile(self.obj.result_file, line) 

class OpcUaSubscription(object):

    def __init__(self, opcua_client_session, result_file, susbcriptionTimeInSec):
        self.result_file = result_file

        handler = subHandler(self)
        susbcriptionTimeInMSec = susbcriptionTimeInSec * 1000   # Converted from Sec into Msec
        self.sub = opcua_client_session.create_subscription(susbcriptionTimeInMSec, handler)
        self.arraynodesHandlers = []        
    
    def start_Subscription_LazyLoad(self, nodes, batchLoadSizeItems, delayLoadBatchTimeInSec ):
        delayToSubscribeBetweenNodes = delayLoadBatchTimeInSec/batchLoadSizeItems

        for batchNodes in batch(arrayNodes, batchLoadSizeItems):
            # Add all nodes into a subscription and start listening
            self.start_Subscription(batchNodes) 

            # Add a small delay between subscription
            time.sleep(delayLoadBatchTimeInSec )    



    def start_Subscription(self, nodes):
        for node in nodes:
            try:
                nodehandle = self.sub.subscribe_data_change(node)
                self.arraynodesHandlers.append(nodehandle)    
            except Exception as e:
                error = True
                errorMessage = ". Error adding node " + str(node.nodeid) + ": " + str(e)
                writeMessageInFile(self.result_file, errorMessage)        

    def close_Subscription(self):
        for handle in self.arraynodesHandlers:
            self.sub.unsubscribe(handle)
        self.sub.delete()   

if __name__ == "__main__":  

    logging.basicConfig(level=logging.ERROR)      

    parameters = getParameters()

    try:               
        firstLineMessage = "Tagid  ,\t Value ,\t StatusCode ,\t Timestamp ,\t Variant value ,\t Error messages"                   
        writeMessageInFile(parameters['result_file'], firstLineMessage)
        
        # Init Opcua Client Session
        opcua_client = opcUaClient(parameters['opcServerUrl'])

        # Read the nodes from server 
        arrayNodes = opcua_client.getArrayNodesFromOpcServer(parameters['taglist_file'],parameters['result_file'])

        # Create OpcUa Susbcription
        subscription = OpcUaSubscription(opcua_client.session, parameters['result_file'], parameters['susbcriptionTimeInSec'])

        # Add items into susbcription with a delay between addings
        subscription.start_Subscription_LazyLoad(arrayNodes, parameters['batchLoadSizeItems'], parameters['delayLoadBatchTimeInSec'])               

        # Delay the program to read the values 
        time.sleep(parameters['delayTimeToReadTagsInSec'] ) #Default 30 sec to read the value of all tags

        # Close the subscription
        subscription.close_Subscription()
        
    finally:
        opcua_client.session.disconnect()