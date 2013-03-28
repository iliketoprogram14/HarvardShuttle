import csv, re, cStringIO, codecs
import time
import HTMLParser

from pattern.web import abs, URL, DOM, Node, plaintext, strip_between
from pattern.web import NODE, TEXT, COMMENT, ELEMENT, DOCUMENT


class Writer:
    def __init__(self, path):
        self.f = open(path, "wb")

    def write(self, string):
        self.f.write('"' + clean(string) + '",')

    def writeLine(self, string):
        self.f.write(str(string) + "\n")

    def writeBlankLine(self):
        self.f.write("\n")

    def writeList(self, lst):
        self.f.write(clean(','.join(lst)) + "\n")

    def close(self):
        self.f.close()


h = HTMLParser.HTMLParser()


def get_all_elems(e):
    """Concatenates a list of strings. 
       May remove the last element of the list if unneccessary."""
    if e[-1].content.find("more credit") != -1: # may be extra credits for actors, writers, etc
        e.pop()
    return reduce(lambda x, y: x + '; ' + y.content, e, '').lstrip('; ')

def clean(s):
    """Gets rid of escaped html sequences"""
    return h.unescape(s.replace("<br />", " ").replace("&nbsp;", ""))

urls = ["http://www.campusservices.harvard.edu/transit-fleet/evening-nights-monday-friday", 
        "http://www.campusservices.harvard.edu/transit-fleet/morning-afternoon-monday-friday",
        "http://www.campusservices.harvard.edu/transit-fleet/weekends",
        "http://www.campusservices.harvard.edu/transit-fleet/overnight-service"]

count = 1
for urlStr in urls:
    url = URL(urlStr)
    dom = DOM(url.download(cached=False))
    print "Parsing " + urlStr
    
    body = dom.by_class("field-items")[0]
    tables = body.by_tag("table")
    headers = body.by_tag("h2")
    for i, table in enumerate(tables):
        writer = Writer(str(count) + ".csv")
        header = headers[i].content
        print ""
        print "Parsing route " + str(header)
        writer.writeLine(urlStr.split("/")[-1])
        writer.writeLine(header)
        
        header = True
        for row in table.by_tag("tr"):
            #lst = []
            for cell in row.by_tag("td"):
                if (header and len(cell.by_tag("strong")) != 0):
                    writer.write(cell.by_tag("strong")[0].content)
                else:
                    writer.write(cell.content)
            header = False
            writer.writeBlankLine()
            #writer.writeList(lst)
        
        writer.close()
        count += 1
