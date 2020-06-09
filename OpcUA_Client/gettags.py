import sys
sys.path.insert(0, "..")


from opcua import Client


if __name__ == "__main__":
    #local run
    client = Client("opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper")
    filename = r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\Taglist.txt"
    outfile =  r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\resultTagList.txt"
    
    #Client run with arguments
    # if len(sys.argv) != 4:
    #     print("Syntax: " , sys.argv[0], "[OpcUaUrl]", "[Inputfile]", "[Outputfile]")
    #     sys.exit(-1)
        
    # url = sys.argv[1]
    # filename = sys.argv[2]
    # outfile =  sys.argv[3]
    # client = Client(url)
    
    try:
        client.connect()
        f = open(filename, "r")
        w = open(outfile, "w")
        error = False
        message = "Tag from file  ,\t Tagid  ,\t BrowseName ,\t Value ,\t StatusCode ,\t Timestamp ,\t Variant value ,\t Error messages"
                   
        w.write(message)
        w.write("\n")
        
        for x in f:
            tag=x.strip()
            try:               
                var = client.get_node(tag)                
                
                message = ""

                #1st Read the whole variant Value of the tag
                try:               
                    browseName = var.get_browse_name().Name
                except:
                    error = True
                    browseName = ""
                    message = message + ",\t error trying to read the name from the tag. "
                
                #2nd Read the tagId of the tag
                try:               
                    tagid = var.nodeid.Identifier
                except:
                    error = True
                    tagid = ""
                    message = message + ",\t error trying to read the tagId from the tag. "

                #3rd Read the DataValue of the tag
                try:               
                    dataValue = var.get_data_value()                                
                except:
                    error = True
                    dataValue = ""
                    message = message + ",\t error trying to get the DataValue from the tag. "                               
                
                #4th Read the whole variant Value of the tag
                try:               
                    variantValue = str(dataValue.Value)
                except Exception as e:
                    error = True
                    variantValue = ". Error message: "  + str(e)
                    message = message + ",\t error trying to read the variant value from the tag. "

                #5th Read the Value of the tag
                try:               
                    value = str(dataValue.Value.Value)
                except Exception as e:
                    error = True
                    value = ". Error message: "  + str(e)
                    message = message + ",\t error trying to read the value from the tag. "

                #6th Read the StatusCode of the tag
                try:               
                    statusCode = str(dataValue.StatusCode)
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
                    message = tag + " ,\t " + tagid + " ,\t " + browseName + " ,\t " + value + " ,\t " + statusCode + " ,\t " + timestamp + ",\t Whole variant value: " + variantValue + ",\t Error message: " + message + " ."
                else:
                    message = tag + " ,\t " + tagid + " ,\t " + browseName + " ,\t " + value + " ,\t " + statusCode + " ,\t " + timestamp + ",\t Whole variant value: " + variantValue + " ."

            except Exception as e: 
                message = tag + " ,\t Cannot read tag from source. Error message: "  + str(e)

            line = message
            w.write(line)
            w.write("\n")
            print(line)
    finally:
        client.disconnect()