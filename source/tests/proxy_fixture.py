"""Loopback-only authenticated SOCKS4/SOCKS5/HTTP CONNECT fixture."""
import base64, pathlib, select, socket, struct, sys, threading
method, portfile = int(sys.argv[1]), pathlib.Path(sys.argv[2])
listener=socket.socket(); listener.bind(('127.0.0.1',0)); listener.listen()
portfile.write_text(str(listener.getsockname()[1]),encoding='ascii')
def handle(client):
    remote=None
    try:
        client.settimeout(10)
        def receive(count):
            data=b''
            while len(data)<count:
                block=client.recv(count-len(data))
                if not block: raise EOFError()
                data+=block
            return data
        def zero():
            data=b''
            while True:
                b=receive(1)
                if b==b'\0': return data
                if len(data)>4096: raise ValueError()
                data+=b
        if method==1:
            request=receive(8)
            if request[:2]!=b'\x04\x01' or zero()!=b'proxy-user': raise ValueError()
            host=socket.inet_ntoa(request[4:]); port=struct.unpack('!H',request[2:4])[0]
        elif method==2:
            version,count=receive(2)
            if version!=5 or 2 not in receive(count): raise ValueError()
            client.sendall(b'\x05\x02'); version,userlen=receive(2)
            user=receive(userlen); password=receive(receive(1)[0])
            if version!=1 or user!=b'proxy-user' or password!=b'proxy-password': raise ValueError()
            client.sendall(b'\x01\x00'); version,cmd,reserved,kind=receive(4)
            if version!=5 or cmd!=1: raise ValueError()
            host=socket.inet_ntoa(receive(4)) if kind==1 else receive(receive(1)[0]).decode('ascii') if kind==3 else None
            port=struct.unpack('!H',receive(2))[0]
        else:
            request=b''
            while not request.endswith(b'\r\n\r\n'):
                if len(request)>32768: raise ValueError()
                request+=receive(1)
            lines=request.decode('ascii').split('\r\n'); verb,target,_=lines[0].split(' ')
            headers=dict(line.split(':',1) for line in lines[1:] if ':' in line)
            if verb!='CONNECT' or headers.get('Proxy-Authorization','').strip()!='Basic '+base64.b64encode(b'proxy-user:proxy-password').decode(): raise ValueError()
            host,port=target.rsplit(':',1); port=int(port)
        if host!='127.0.0.1': raise ValueError('Only loopback destinations permitted')
        remote=socket.create_connection((host,port),timeout=10)
        client.sendall(b'\x00\x5a'+b'\0'*6 if method==1 else b'\x05\x00\x00\x01'+b'\0'*6 if method==2 else b'HTTP/1.1 200 Connection established\r\n\r\n')
        client.settimeout(None); remote.settimeout(None)
        while True:
            ready,_,_=select.select([client,remote],[],[],10)
            if not ready: continue
            for source in ready:
                data=source.recv(65536)
                if not data: return
                (remote if source is client else client).sendall(data)
    except (OSError,EOFError,ValueError): pass
    finally:
        client.close()
        if remote: remote.close()
while True:
    client,_=listener.accept(); threading.Thread(target=handle,args=(client,),daemon=True).start()
