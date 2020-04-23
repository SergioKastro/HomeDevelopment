import asyncio
import sys
sys.path.insert(0, "..")
from asyncua import Client, Node, ua

async def main():
     #local run
    # filename = r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\Taglist.txt"
    # outfile =  r"C:\Users\sergioc\OneDrive - KONGSBERG MARITIME AS\Projects\Edge Gateway for Shell\Trond app to read tags unit measures\resultTagList.txt"
    # url = r"opc.tcp://KPC22014549:21381/MatrikonOpcUaWrapper"

    #Client run with arguments
    if len(sys.argv) != 4:
        print("Syntax: " , sys.argv[0], "[OpcUaUrl]", "[Inputfile]", "[Outputfile]")
        sys.exit(-1)
        
    url = sys.argv[1]
    filename = sys.argv[2]
    outfile =  sys.argv[3]

    async with Client(url=url) as client:
        try:
            f = open(filename, "r")
            w = open(outfile, "w", encoding="utf-8")
            for x in f:
                tag=x.strip()
                try:
                    var = client.get_node(tag)
                    try:
                        name = (await var.get_browse_name()).Name
                    except:
                        name = "Error reading name"
                    try:
                        val =  str(await var.get_value())
                    except:
                        val = "Error reading value"
                    try:
                        timestamp = (await var.get_data_value()).SourceTimestamp.isoformat('T')+"Z"
                    except:
                        timestamp = "Error reading timestamp"
                    line = tag + "," + name + "," + val + "," + timestamp
                except:
                    line = tag + ",Cannot read tag from source"
                w.write(line)
                w.write("\n")
                print(line)
        finally:
            f.close()
            w.close()
if __name__ == '__main__':
    loop = asyncio.get_event_loop()
    loop.set_debug(True)
    loop.run_until_complete(main())
    loop.close()