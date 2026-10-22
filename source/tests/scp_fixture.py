"""Loopback SSH/SCP fixture. No SFTP subsystem; no real shell commands executed."""
import datetime, os, pathlib, shlex, shutil, socket, sys, threading, time
sys.path.insert(0, sys.argv[1])
import paramiko
root = pathlib.Path(sys.argv[2]).resolve()
root.mkdir(parents=True, exist_ok=True)
(root / "sub").mkdir(exist_ok=True)
(root / "no-write").mkdir(exist_ok=True)
(root / "český soubor.txt").write_text("Příliš žluťoučký kůň", encoding="utf-8")
(root / "slow.bin").write_bytes(bytes(range(256)) * 32768)
(root / "sub" / "nested.txt").write_text("nested", encoding="utf-8")

def local(path):
    result = (root / path.lstrip("/")).resolve()
    if not result.is_relative_to(root): raise PermissionError("Outside fixture root")
    return result
def line(channel):
    data = bytearray()
    while True:
        b = channel.recv(1)
        if not b: raise EOFError()
        if b == b"\n": return bytes(data)
        data.extend(b)
def ack(channel):
    value = channel.recv(1)
    if value != b"\0": raise IOError("Invalid SCP acknowledgement: " + repr(value))
def execute(channel, raw):
    try:
        # Let Paramiko send SSH_MSG_CHANNEL_SUCCESS before a fast command exits.
        time.sleep(.03)
        print(raw.decode("utf-8"), flush=True)
        args = shlex.split(raw.decode("utf-8"))
        while args and "=" in args[0]: args.pop(0)
        if args[0] == "scp":
            # libssh2 combines flags (-pf/-pt); OpenSSH accepts both forms.
            flags = "".join(arg.lstrip("-") for arg in args[1:-1] if arg.startswith("-"))
            target = local(args[-1])
            if "t" in flags:
                if target == root / "no-write" or target.is_relative_to(root / "no-write"):
                    channel.sendall(b"\x01Permission denied\n"); return
                channel.sendall(b"\0")
                header = line(channel)
                while header.startswith(b"T"):
                    channel.sendall(b"\0"); header = line(channel)
                if not header.startswith(b"C"): raise IOError("Missing SCP file header")
                _, size, name = header.split(b" ", 2); remaining = int(size)
                if target.is_dir():
                    target = (target / name.decode("utf-8")).resolve()
                    if not target.is_relative_to(root): raise PermissionError("Unsafe SCP filename")
                channel.sendall(b"\0")
                with target.open("wb") as stream:
                    while remaining:
                        block = channel.recv(min(65536, remaining))
                        if not block: raise EOFError()
                        stream.write(block); remaining -= len(block)
                ack(channel); channel.sendall(b"\0")
            elif "f" in flags:
                ack(channel); size = target.stat().st_size
                if "p" in flags:
                    stat = target.stat()
                    channel.sendall(f"T{int(stat.st_mtime)} 0 {int(stat.st_atime)} 0\n".encode())
                    ack(channel)
                channel.sendall(b"C0644 " + str(size).encode() + b" " + target.name.encode("utf-8") + b"\n")
                ack(channel)
                with target.open("rb") as stream:
                    while block := stream.read(65536):
                        channel.sendall(block)
                        if target.name == "slow.bin": time.sleep(.008)
                channel.sendall(b"\0"); ack(channel)
            else: raise IOError("Unsupported SCP operation")
        elif args[0] == "find":
            folder = local(args[1]); values = []
            for item in folder.iterdir():
                stat = item.lstat()
                values.extend(["d" if item.is_dir() else "l" if item.is_symlink() else "f",
                    str(stat.st_size), str(stat.st_mtime), item.name])
            channel.sendall(("\0".join(values) + ("\0" if values else "")).encode("utf-8"))
        elif args[0] == "mkdir": local(args[-1]).mkdir()
        elif args[0] == "mv": local(args[-2]).rename(local(args[-1]))
        elif args[0] == "rm":
            item = local(args[-1])
            if item == root: raise PermissionError("Root removal")
            if item.is_dir(): shutil.rmtree(item)
            else: item.unlink()
        elif args[0] == "touch":
            stamp = datetime.datetime.strptime(args[2], "%Y%m%d%H%M.%S").replace(tzinfo=datetime.timezone.utc).timestamp()
            os.utime(local(args[-1]), (stamp, stamp))
        else: raise IOError("Unsupported command: " + args[0])
        channel.send_exit_status(0)
    except Exception as exc:
        print(repr(exc), file=sys.stderr, flush=True)
        try: channel.send_stderr(str(exc).encode("utf-8")); channel.send_exit_status(1)
        except Exception: pass
    finally:
        channel.close()
class Auth(paramiko.ServerInterface):
    def check_auth_password(self, username, password):
        return paramiko.AUTH_SUCCESSFUL if (username, password) == ("tester", "test-password") else paramiko.AUTH_FAILED
    def get_allowed_auths(self, username): return "password"
    def check_channel_request(self, kind, chanid):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED
    def check_channel_exec_request(self, channel, command):
        threading.Thread(target=execute, args=(channel, command), daemon=True).start()
        return True
    def check_channel_subsystem_request(self, channel, name):
        (root / "SFTP-WAS-REQUESTED").write_text(name)
        return False
key = paramiko.RSAKey.generate(2048)
listener = socket.socket()
listener.bind(("127.0.0.1", 0)); listener.listen()
pathlib.Path(sys.argv[3]).write_text(str(listener.getsockname()[1]))
def client(connection):
    transport = paramiko.Transport(connection)
    transport.add_server_key(key)
    channels = []
    try:
        transport.start_server(server=Auth())
        while transport.is_active():
            channel = transport.accept(1)
            if channel is not None: channels.append(channel)
    except Exception: pass
    finally: transport.close()
while True:
    connection, address = listener.accept()
    threading.Thread(target=client, args=(connection,), daemon=True).start()

