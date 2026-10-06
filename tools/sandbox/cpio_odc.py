import gzip, os, sys, stat
src, dest = sys.argv[1], sys.argv[2]
listonly = len(sys.argv)>3
f = gzip.open(src,'rb'); n=0
while True:
    h=f.read(76)
    if len(h)<76: break
    assert h[:6]==b'070707', h[:6]
    mode=int(h[18:24],8); nsz=int(h[59:65],8); fsz=int(h[65:76],8)
    name=f.read(nsz)[:-1].decode()
    if name=='TRAILER!!!': break
    data=f.read(fsz)
    n+=1
    if listonly:
        if n<15: print(oct(mode),fsz,name)
        continue
    p=os.path.normpath(os.path.join(dest,name))
    assert p.startswith(os.path.normpath(dest))
    t=stat.S_IFMT(mode)
    if t==stat.S_IFDIR: os.makedirs(p,exist_ok=True)
    elif t==stat.S_IFLNK:
        os.makedirs(os.path.dirname(p),exist_ok=True)
        if os.path.lexists(p): os.remove(p)
        os.symlink(data.decode(),p)
    elif t==stat.S_IFREG:
        os.makedirs(os.path.dirname(p),exist_ok=True)
        with open(p,'wb') as o: o.write(data)
        os.chmod(p, mode & 0o777)
print('entries',n)
