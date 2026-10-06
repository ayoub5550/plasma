import struct, zlib, sys, xml.etree.ElementTree as ET
f=open(sys.argv[1],'rb'); h=f.read(28)
magic,hs,ver,tc,tu,ck=struct.unpack('>4sHHQQI',h); assert magic==b'xar!'
f.seek(hs); toc=zlib.decompress(f.read(tc)); heap=hs+tc
root=ET.fromstring(toc)
def walk(e,path=''):
    for fe in e.findall('file'):
        name=fe.findtext('name'); p=path+'/'+name
        d=fe.find('data')
        if d is not None:
            off=int(d.findtext('offset')); ln=int(d.findtext('length')); enc=d.find('encoding').get('style')
            print(p,off,ln,enc)
            if name in ('Payload','Scripts','PackageInfo'):
                f.seek(heap+off); out=open(p.strip('/').replace('/','_'),'wb')
                rem=ln
                while rem>0:
                    b=f.read(min(rem,1<<24)); out.write(b); rem-=len(b)
        walk(fe,p)
walk(root.find('toc'))
