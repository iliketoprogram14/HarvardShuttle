import csv, re, cStringIO, codecs
import time
import HTMLParser
import sys, os

from pattern.web import abs, URL, DOM, Node, plaintext, strip_between
from pattern.web import NODE, TEXT, COMMENT, ELEMENT, DOCUMENT


class Writer:
    def __init__(self, path):
        self.f = open(path, "wb")

    def write(self, string):
        self.f.write(string)

    def writeKey(self, string):
        self.f.write('"' + string + '": ')

    def writeArray(self, lst):
        self.beginArray()
        lst = map(lambda x: '"'+x+'"', lst)
        self.f.write(','.join(lst))        
        self.endArray()

    def writeKeyVal(self, k, v, notLast):
        self.writeKey(k)
        self.writeVal(v, notLast)

    def writeVal(self, v, notLast):
        self.write('"' + v + '"' + (", " if notLast else ""))

    def beginObj(self):
        self.f.write("{")
        
    def endObj(self):
        self.f.write("}")

    def beginArray(self):
        self.f.write("[")

    def endArray(self):
        self.f.write("]")

    def close(self):
        self.f.close()


h = HTMLParser.HTMLParser()

def clean(s):
    """Gets rid of escaped html sequences"""
    return h.unescape(s)


stop_str = open('api_data_store.xml', 'r').read()
dom = DOM(stop_str)

# Write the beginning
writer = Writer("db.json")
writer.beginObj()

# Write the stops
print "Writing stops..."
writer.writeKey("stops")
writer.beginArray()

namesToIDs = dict()

stops = dom.by_tag("stop")
for stop in stops:
    stop_id = stop.attributes["s_id"]
    name = stop.by_tag("title")[0].content
    title = "i-Lab" if name == "HiLab-HBS" else name
    routes = stop.by_tag("stop_routes")[0].content.split(",")

    # Map names to ids
    namesToIDs[name] = stop_id

    writer.beginObj()
    writer.writeKeyVal("id", stop_id, True)
    writer.writeKeyVal("name", name, True)
    writer.writeKeyVal("title", title, True)
    writer.writeKey("routes")
    writer.writeArray(routes)
    writer.endObj()
    
    if (stop != stops[-1]):
        writer.write(",")
    
writer.endArray()
writer.write(",")
print "Done writing stops."

# Write the routes
print "Writing routes..."
writer.writeKey("routes")
writer.beginArray()

fixed_names = dict()
for name in namesToIDs.iterkeys():
    fixed_names[name] = name
fixed_names["WCC"] = "Law School"
fixed_names["WCC "] = "Law School"
fixed_names["Law"] = "Law School"
fixed_names["Mather"] = "Mather House"
fixed_names["Winthrop"] = "Winthrop House"
fixed_names["Hi Lab HBS"] = "HiLab-HBS"
fixed_names["Hi Lab HBS "] = "HiLab-HBS"
fixed_names["HKS"] = "Kennedy School"
fixed_names["Quad Mass Ave"] = "Quad"
fixed_names["Mass Ave"] = "Mass Ave Garden St"
fixed_names["Soldiers"] = "Soldiers Field Park"
fixed_names["Harvard"] = "Harvard Square"
fixed_names["Lamont"] = "Lamont Library"
fixed_names["Memorial"] = "Memorial Hall"
fixed_names["Peabody"] = "Peabody Terrace"
fixed_names["Boylston"] = "Boylston Gate"

def cleanTime(string):
    string = string.replace("To Allston Campus", "").replace('"','').replace("<p>","").replace("</p>",'')
    #string = string.replace("<strong>FRIDAY AND SATURDAY NIGHT ONLY</strong>", "")
    return string


first = 1
last = 12
routes = dom.by_tag("routes")
for i in range(first, last):
    fileStr = open(str(i) + ".csv", "r").read()
    lines = fileStr.split("\n")

    title = lines[1].replace(",","")
    stops = lines[2].split(',')
    route_id = "blah"

    print ""
    print i, title

    # Write first part of object
    writer.beginObj()
    writer.writeKeyVal("id", route_id, True)
    writer.writeKeyVal("name", title, True)

    # Write stops
    writer.writeKey("stops")
    lst = []
    id_lst = []
    for stop in stops:
        if stop.replace('"',"").replace("'","") == "": continue
        if stop.replace('"',"").replace("'","") == "Garden St": continue
        fixed_name = fixed_names[stop.replace('"',"")]
        print stop, fixed_name, namesToIDs[fixed_name]
        lst.append(fixed_name)
        id_lst.append(namesToIDs[fixed_name])
    writer.writeArray(id_lst)
    writer.write(",")
    
    # Write trips
    writer.writeKey("trips")
    writer.beginArray()
    num_lines = len(lines)
    for j, line in enumerate(lines[3:]): # trip j has k times
        if line == "": continue
        times = line.split(",")
        count = 0
        writer.beginObj()
        for k, time in enumerate(times):
            count += 1
            time = cleanTime(time)
            writer.writeKeyVal(id_lst[k], time, (k != len(times)-1))
            #print i, time, len(id_lst)
        writer.endObj()
        if (j+3 < num_lines - 2):
            writer.write(", ")
        print j, num_lines

    writer.endArray()
    writer.endObj()

    if (i < last-1):
        writer.write(",")

writer.endArray()
writer.endObj()







sys.exit()
