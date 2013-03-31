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

def cleanTime(string):
    string = string.replace("To Allston Campus", "").replace('"','').replace("<p>","").replace("</p>",'')
    string = string.replace("To Allston", "")
    string = string.replace("To Garage via Harvard Square", "")
    #string = string.replace("<strong>FRIDAY AND SATURDAY NIGHT ONLY</strong>", "")
    return string

def adjustTime(time, isMorning):
    if (time == ""): return (time, isMorning)
    if "am" in time or "AM" in time:
        isMorning = True
        time = time.replace("am", "").replace("AM", "").strip()
    elif "pm" in time or "PM" in time:
        isMorning = False
        time = time.replace("pm", "").replace("PM", "").strip()
        fields = time.split(":")
        time = str(int(fields[0]) + 12) + ":" + fields[1]
    elif not isMorning:
        fields = time.split(":")
        time = str(int(fields[0]) + 12) + ":" + fields[1]
    return (time, isMorning)

def writeExpressTimes(writer, first_trip, last_trip, step, stops_dict):
    curr_trip = first_trip
    firstTime = True

    while (curr_trip <= last_trip):
        if (firstTime): firstTime = False
        else:           writer.beginObj()

        for i, time in enumerate(curr_trip):
            # Write out the time
            hr, minute = time
            time_str = "%d:%.2d" % (hr, minute)
            writer.writeKeyVal(stops_dict[i], time_str, (i != len(curr_trip)-1))

            # Update the time for the next trip
            if (minute + step >= 60):
                hr += 1
            minute = (minute + step) % 60
            curr_trip[i] = (hr, minute)

        # Only close the object or write commas if this isn't the last object
        if (curr_trip <= last_trip):
            writer.endObj()
            writer.write(", ")

def writeQuadExpressTimes(writer, stops_dict):
    """ Quad express comes every 10 minutes from 7:40am to 3:50pm """
    first_trip = [(8, 00), (8, 03), (8, 10)]
    last_trip = [(15, 50), (15, 53), (16, 00)]
    step = 10
    writeExpressTimes(writer, first_trip, last_trip, step, stops_dict)

def writeMatherExpressTimes(writer, stops_dict):
    """ Mather Express comes every 10 minutes from 8:20am to 3:00pm """
    first_trip = [(8, 20), (8, 23), (8, 30), (8, 35)]
    last_trip = [(15, 00), (15, 03), (15, 10), (15, 15)]
    step = 10
    writeExpressTimes(writer, first_trip, last_trip, step, stops_dict)


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
    # Get the stop id, name, title, and the stop's routes
    stop_id = stop.attributes["s_id"]
    name = stop.by_tag("title")[0].content
    title = "i-Lab" if name == "HiLab-HBS" else name
    routes = stop.by_tag("stop_routes")[0].content.split(",")

    # Map names to stop ids
    namesToIDs[name] = stop_id

    # Write out the stop to the db
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

# Hard coded cases to fix up names
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

# Route metadata
first = 1 # idx of first route
last = 12 # idx after last route
routes = dom.by_tag("routes")

# Iterate through each of the routes
for i in range(first, last):
    fileStr = open(str(i) + ".csv", "r").read()
    lines = fileStr.split("\n")

    # Get the title, id, and the stops of the route
    title = lines[1].replace(",","").title()
    route_id = "blah"
    stops = lines[2].split(',')

    print ""
    print i, title

    # Write first part of object
    writer.beginObj()
    writer.writeKeyVal("id", route_id, True)
    writer.writeKeyVal("name", title, True)

    # Write the "special" field
    special_val = "1" if title == "Extended Overnight" else "0"
    writer.writeKeyVal("special", special_val, True)

    # Write stops
    writer.writeKey("stops")
    id_lst = []
    for stop in stops:
        stop = stop.replace('"', ""). replace("'", "")
        if stop == "": continue
        if stop == "Garden St": continue
        fixed_name = fixed_names[stop]
        id_lst.append(namesToIDs[fixed_name])
        print stop, fixed_name, namesToIDs[fixed_name]
    writer.writeArray(id_lst)
    writer.write(",")
    
    # Write trips
    writer.writeKey("trips")
    writer.beginArray()
    num_lines = len(lines)
    isMorning = True
    special_activated = False
    for j, line in enumerate(lines[3:]): # trip j has k times
        if line == "": continue
        times = line.split(",")

        # Handle the special case: Extended Overnight
        if (special_val == "1"):
            if (title == "Extended Overnight"):
                if (times[0] == "<strong>FRIDAY AND SATURDAY NIGHT ONLY</strong>"):
                    special_activated = True
                    continue
                writer.beginObj()
                val = "Fri,Sat" if special_activated else "Sun,Mon,Tue,Wed,Thur,Fri,Sat"
                writer.writeKeyVal("special", val, True)
            else:
                print "FALSE SPECIAL CASE " + title
                sys.exit()
        else:
            writer.beginObj()


        # Write out the times for this particular trip (trip j)
        for k, time in enumerate(times):
            time, isMorning = adjustTime(cleanTime(time), isMorning)
            # If we hit "|", we need to generate times :(
            if (time == "|"):
                if (title == "Quad Express"):
                    writeQuadExpressTimes(writer, id_lst)
                elif (title == "Mather Express"):
                    writeMatherExpressTimes(writer, id_lst)
                else:
                    print "FAIL WILL ROBINSON " + title
                    sys.exit()
                break
            # Otherwise, just write out the time as is
            else:
                writer.writeKeyVal(id_lst[k], time, (k != len(times)-1))
        writer.endObj()
        if (j+3 < num_lines - 2):
            writer.write(", ")

    writer.endArray()
    writer.endObj()

    if (i < last-1):
        writer.write(",")

writer.endArray()
writer.endObj()







sys.exit()
