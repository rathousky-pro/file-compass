"""Isolated Windows named-pipe SSH agent; serves only a generated fixture key."""
import ctypes, pathlib, struct, sys
from ctypes import wintypes as w
sys.path.insert(0, sys.argv[1])
import paramiko
key = paramiko.RSAKey(filename=sys.argv[2])
k = ctypes.WinDLL('kernel32', use_last_error=True)
k.CreateNamedPipeW.argtypes = [w.LPCWSTR,w.DWORD,w.DWORD,w.DWORD,w.DWORD,w.DWORD,w.DWORD,w.LPVOID]
k.CreateNamedPipeW.restype = w.HANDLE
k.ConnectNamedPipe.argtypes = [w.HANDLE,w.LPVOID]
k.ReadFile.argtypes = [w.HANDLE,w.LPVOID,w.DWORD,ctypes.POINTER(w.DWORD),w.LPVOID]
k.WriteFile.argtypes = [w.HANDLE,w.LPCVOID,w.DWORD,ctypes.POINTER(w.DWORD),w.LPVOID]
k.CloseHandle.argtypes = [w.HANDLE]
pipe = k.CreateNamedPipeW(sys.argv[3], 3, 0, 1, 65536, 65536, 0, None)
if pipe == w.HANDLE(-1).value: raise ctypes.WinError(ctypes.get_last_error())
pathlib.Path(sys.argv[4]).write_text('ready')
def read(n):
    parts=[]
    while n:
        b=ctypes.create_string_buffer(n); got=w.DWORD()
        if not k.ReadFile(pipe,b,n,ctypes.byref(got),None) or got.value==0: raise EOFError()
        parts.append(b.raw[:got.value]);n-=got.value
    return b''.join(parts)
try:
    if not k.ConnectNamedPipe(pipe,None) and ctypes.get_last_error()!=535: raise ctypes.WinError(ctypes.get_last_error())
    while True:
        length=struct.unpack('>I',read(4))[0]
        if length>65536: raise ValueError('oversized fixture request')
        request=read(length);m=paramiko.Message(request[1:]);response=paramiko.Message()
        if request[0]==11:
            response.add_byte(bytes([12]));response.add_int(1);response.add_string(key.asbytes());response.add_string('CommanderCZ fixture only')
        elif request[0]==13:
            m.get_string();data=m.get_string();flags=m.get_int()
            algorithm='rsa-sha2-512' if flags&4 else 'rsa-sha2-256' if flags&2 else 'ssh-rsa'
            response.add_byte(bytes([14]));response.add_string(key.sign_ssh_data(data,algorithm).asbytes())
        else: response.add_byte(bytes([5]))
        payload=response.asbytes();out=struct.pack('>I',len(payload))+payload;written=w.DWORD()
        if not k.WriteFile(pipe,out,len(out),ctypes.byref(written),None):raise ctypes.WinError(ctypes.get_last_error())
except EOFError: pass
finally:k.CloseHandle(pipe)
