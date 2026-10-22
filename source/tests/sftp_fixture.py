"""Loopback-only SFTP fixture backed by a dedicated test directory."""
import errno
import os
import pathlib
import socket
import sys
import threading
import time
import base64
import hashlib
import uuid

sys.path.insert(0, sys.argv[1])
import paramiko

auth_mode = sys.argv[4] if len(sys.argv) > 4 else "password"
user_key = None
if auth_mode.startswith("key-"):
    keyfile = pathlib.Path(sys.argv[3]).parent / "id_test"
    if auth_mode == "key-ed25519":
        from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
        from cryptography.hazmat.primitives import serialization
        keyfile.write_bytes(Ed25519PrivateKey.generate().private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.OpenSSH, serialization.NoEncryption()))
        user_key = paramiko.Ed25519Key(filename=str(keyfile))
    else:
        user_key = paramiko.RSAKey.generate(2048)
        user_key.write_private_key_file(str(keyfile))

root = pathlib.Path(sys.argv[2]).resolve()
root.mkdir(parents=True, exist_ok=True)
(root / "sub").mkdir(exist_ok=True)
(root / "český soubor.txt").write_text("Příliš žluťoučký kůň", encoding="utf-8")
(root / "slow.bin").write_bytes(bytes(range(256)) * 32768)
(root / "sub" / "nested").mkdir(exist_ok=True)
(root / "sub" / "nested" / "one.bin").write_bytes(b"A" * 1048576)
(root / "sub" / "two.bin").write_bytes(b"B" * 1048576)

class TestHandle(paramiko.SFTPHandle):
    def close(self):
        super().close()
        trash = getattr(self, "unlink_when_closed", None)
        if trash is not None:
            try:
                trash.unlink()
            except OSError:
                pass  # Another live Unix-style handle can still own this inode.

    def stat(self):
        try:
            stream = getattr(self, "readfile", None) or getattr(self, "writefile", None)
            return paramiko.SFTPAttributes.from_stat(os.fstat(stream.fileno()))
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def chattr(self, attr):
        try:
            stream = getattr(self, "writefile", None) or getattr(self, "readfile", None)
            if attr.st_size is not None:
                stream.truncate(attr.st_size)
            return paramiko.SFTP_OK
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)


class SlowHandle(TestHandle):
    def read(self, offset, length):
        time.sleep(0.012)
        return super().read(offset, length)


class Auth(paramiko.ServerInterface):
    def check_global_request(self, kind, message):
        if kind == "keepalive@libssh2.org":
            counter = pathlib.Path(sys.argv[3]).parent / "keepalive.txt"
            value = int(counter.read_text()) if counter.exists() else 0
            counter.write_text(str(value + 1))
        return True

    def check_auth_password(self, username, password):
        return paramiko.AUTH_SUCCESSFUL if auth_mode == "password" and (username, password) == ("tester", "test-password") else paramiko.AUTH_FAILED

    def check_auth_publickey(self, username, key):
        return paramiko.AUTH_SUCCESSFUL if username == "tester" and user_key is not None and key == user_key else paramiko.AUTH_FAILED

    def check_auth_interactive(self, username, submethods):
        if auth_mode == "interactive" and username == "tester":
            return paramiko.InteractiveQuery("Login", "", ("Password: ", False))
        return paramiko.AUTH_FAILED

    def check_auth_interactive_response(self, responses):
        return paramiko.AUTH_SUCCESSFUL if responses == ["test-password"] else paramiko.AUTH_FAILED

    def get_allowed_auths(self, username):
        return "publickey" if user_key is not None else "keyboard-interactive" if auth_mode == "interactive" else "password"

    def check_channel_request(self, kind, chanid):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED


class Files(paramiko.SFTPServerInterface):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.handles = []

    def local(self, path):
        result = (root / path.lstrip("/")).resolve()
        if not result.is_relative_to(root):
            raise PermissionError(errno.EACCES, "Outside fixture root")
        return result

    def canonicalize(self, path):
        return "/" + self.local(path).relative_to(root).as_posix().rstrip(".")

    def list_folder(self, path):
        try:
            entries = []
            for item in self.local(path).iterdir():
                attrs = paramiko.SFTPAttributes.from_stat(item.stat())
                attrs.filename = item.name
                entries.append(attrs)
            return entries
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def stat(self, path):
        try:
            return paramiko.SFTPAttributes.from_stat(self.local(path).stat())
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    lstat = stat

    def open(self, path, flags, attr):
        try:
            fd = os.open(self.local(path), flags | getattr(os, "O_BINARY", 0), 0o666)
            mode = "r+b" if flags & os.O_RDWR else "wb" if flags & os.O_WRONLY else "rb"
            stream = os.fdopen(fd, mode)
            handle = SlowHandle(flags) if path.endswith("slow.bin") else TestHandle(flags)
            if flags & (os.O_WRONLY | os.O_RDWR):
                handle.writefile = stream
            if not flags & os.O_WRONLY:
                handle.readfile = stream
            handle.local_path = self.local(path)
            handle.flags = flags
            self.handles.append(handle)
            return handle
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def mkdir(self, path, attr):
        try:
            self.local(path).mkdir()
            return paramiko.SFTP_OK
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def remove(self, path):
        try:
            # Unix permits unlinking an open file. Windows-backed fixtures need
            # a private trash inode so outstanding handles retain their contents.
            source = self.local(path)
            reopen = []
            for handle in self.handles:
                stream = getattr(handle, "readfile", None) or getattr(handle, "writefile", None)
                if handle.local_path == source and stream is not None and not stream.closed:
                    stream.flush()
                    reopen.append((handle, stream.tell()))
                    stream.close()
            if reopen:
                trash = source.with_name(".deleted-" + uuid.uuid4().hex)
                source.rename(trash)
                for handle, position in reopen:
                    fd = os.open(trash, (handle.flags & ~(os.O_TRUNC | os.O_EXCL | os.O_CREAT)) | getattr(os, "O_BINARY", 0))
                    mode = "r+b" if handle.flags & os.O_RDWR else "wb" if handle.flags & os.O_WRONLY else "rb"
                    stream = os.fdopen(fd, mode)
                    stream.seek(position)
                    if hasattr(handle, "readfile"):
                        handle.readfile = stream
                    if hasattr(handle, "writefile"):
                        handle.writefile = stream
                    handle.local_path = trash
                    handle.unlink_when_closed = trash
            else:
                source.unlink()
            return paramiko.SFTP_OK
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def rmdir(self, path):
        try:
            self.local(path).rmdir()
            return paramiko.SFTP_OK
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def rename(self, oldpath, newpath):
        try:
            # Windows denies renaming Python-open files; real Unix SFTP servers
            # allow it. Preserve live fixture handles across the rename.
            reopen = []
            for handle in self.handles:
                stream = getattr(handle, "readfile", None) or getattr(handle, "writefile", None)
                if handle.local_path == self.local(oldpath) and stream is not None and not stream.closed:
                    stream.flush()
                    reopen.append((handle, stream.tell()))
                    stream.close()
            self.local(oldpath).rename(self.local(newpath))
            for handle, position in reopen:
                fd = os.open(self.local(newpath), (handle.flags & ~(os.O_TRUNC | os.O_EXCL | os.O_CREAT)) | getattr(os, "O_BINARY", 0))
                mode = "r+b" if handle.flags & os.O_RDWR else "wb" if handle.flags & os.O_WRONLY else "rb"
                stream = os.fdopen(fd, mode)
                stream.seek(position)
                if hasattr(handle, "readfile"):
                    handle.readfile = stream
                if hasattr(handle, "writefile"):
                    handle.writefile = stream
                handle.local_path = self.local(newpath)
            return paramiko.SFTP_OK
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)

    def chattr(self, path, attr):
        try:
            if attr.st_atime is not None and attr.st_mtime is not None:
                os.utime(self.local(path), (attr.st_atime, attr.st_mtime))
            return paramiko.SFTP_OK
        except OSError as exc:
            return paramiko.SFTPServer.convert_errno(exc.errno)


key = paramiko.RSAKey.generate(2048)
second_key = paramiko.ECDSAKey.generate(bits=256)
pathlib.Path(sys.argv[3]).parent.joinpath("host-fingerprint.txt").write_text("ssh-rsa " + base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip("="), encoding="ascii")
listener = socket.socket()
listener.bind(("127.0.0.1", 0))
listener.listen()
pathlib.Path(sys.argv[3]).write_text(str(listener.getsockname()[1]), encoding="ascii")


def serve(client):
    transport = paramiko.Transport(client)
    transport.add_server_key(key)
    if auth_mode != "password":
        transport.add_server_key(second_key)
        transport.use_compression(True)
    transport.set_subsystem_handler("sftp", paramiko.SFTPServer, Files)
    try:
        transport.start_server(server=Auth())
        while transport.is_active():
            time.sleep(0.1)
    except Exception:
        pass
    finally:
        transport.close()


while True:
    client, _ = listener.accept()
    threading.Thread(target=serve, args=(client,), daemon=True).start()
