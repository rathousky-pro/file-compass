"""Loopback-only FTP fixture. Requires pyftpdlib in the given dependency folder."""
import pathlib
import sys

sys.path.insert(0, sys.argv[1])
from pyftpdlib.authorizers import DummyAuthorizer
from pyftpdlib.handlers import FTPHandler, ThrottledDTPHandler
from pyftpdlib.servers import FTPServer

root = pathlib.Path(sys.argv[2]).resolve()
root.mkdir(parents=True, exist_ok=True)
(root / "sub").mkdir(exist_ok=True)
(root / "český soubor.txt").write_text("Příliš žluťoučký kůň", encoding="utf-8")
(root / "slow.bin").write_bytes(bytes(range(256)) * 32768)
authorizer = DummyAuthorizer()
authorizer.add_user("tester", "test-password", str(root), perm="elradfmwMT")
class RecoverableFTP(FTPHandler):
    def ftp_APPE(self, path):
        if (root / "__deny_relay_append__").exists():
            self.respond("502 Test server denies APPE for relay")
            return
        return super().ftp_APPE(path)

    def ftp_RETR(self, path):
        marker = root / "__fail_relay_download_once__"
        if pathlib.Path(path).name == "slow.bin" and marker.exists():
            marker.unlink()
            self.respond("550 Test relay download refused once")
            return
        return super().ftp_RETR(path)

    def ftp_STOR(self, path, mode="w"):
        marker = root / "__fail_relay_upload_once__"
        if pathlib.Path(path).parent.name == "failure-retry" and marker.exists():
            marker.unlink()
            self.respond("550 Test relay upload refused once")
            return
        return super().ftp_STOR(path, mode)

    def ftp_MKD(self, path):
        if pathlib.Path(path).name == "__deny_cwd_for_test__":
            self.deny_cwd_for_test = True
            self.respond('257 "test fault armed"')
            return
        return super().ftp_MKD(path)

    def ftp_CWD(self, path):
        if getattr(self, "deny_cwd_for_test", False):
            self.respond("550 Test session no longer allows CWD")
            return
        return super().ftp_CWD(path)

handler = RecoverableFTP
mode = sys.argv[4] if len(sys.argv) > 4 else "default"
if mode.startswith("ftps"):
    import datetime, ipaddress
    from cryptography import x509
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography.hazmat.primitives.serialization import pkcs12
    from pyftpdlib.handlers import TLS_FTPHandler
    from OpenSSL import SSL
    private = rsa.generate_private_key(65537, 2048)
    name = x509.Name([x509.NameAttribute(x509.NameOID.COMMON_NAME, "Commander loopback test")])
    now = datetime.datetime.now(datetime.timezone.utc)
    san = x509.SubjectAlternativeName([x509.DNSName("wrong.example")] if mode == "ftps-wrong-host" else [x509.IPAddress(ipaddress.ip_address("127.0.0.1"))])
    cert = x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(private.public_key()).serial_number(x509.random_serial_number()).not_valid_before(now-datetime.timedelta(minutes=1)).not_valid_after(now+datetime.timedelta(days=1)).add_extension(san,False).add_extension(x509.BasicConstraints(ca=True,path_length=None),True).sign(private,hashes.SHA256())
    folder = pathlib.Path(sys.argv[3]).parent
    folder.joinpath("ca.pem").write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    pem = folder / "server.pem"
    pem.write_bytes(private.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.TraditionalOpenSSL,serialization.NoEncryption()) + cert.public_bytes(serialization.Encoding.PEM))
    handler = TLS_FTPHandler
    if mode == "ftps-implicit":
        class Implicit(TLS_FTPHandler):
            def handle(self): self.secure_connection(self.ssl_context)
            def handle_ssl_established(self): FTPHandler.handle(self)
        handler = Implicit
    handler.certfile = str(pem)
    handler.tls_control_required = handler.tls_data_required = True
    if mode == "ftps-client":
        client_key = rsa.generate_private_key(65537,2048)
        client_name = x509.Name([x509.NameAttribute(x509.NameOID.COMMON_NAME,"Commander client")])
        client_cert = x509.CertificateBuilder().subject_name(client_name).issuer_name(name).public_key(client_key.public_key()).serial_number(x509.random_serial_number()).not_valid_before(now-datetime.timedelta(minutes=1)).not_valid_after(now+datetime.timedelta(days=1)).sign(private,hashes.SHA256())
        folder.joinpath("client.pfx").write_bytes(pkcs12.serialize_key_and_certificates(b"test",client_key,client_cert,None,serialization.NoEncryption()))
        ctx = handler.get_ssl_context()
        ctx.load_verify_locations(str(folder / "ca.pem"))
        ctx.set_verify(SSL.VERIFY_PEER | SSL.VERIFY_FAIL_IF_NO_PEER_CERT,lambda c,cert,error,depth,valid: valid)
elif mode == "list":
    class Legacy(FTPHandler):
        def ftp_MLSD(self,path): self.respond("502 MLSD not supported")
    handler=Legacy
elif mode == "no-resume":
    class NoResume(FTPHandler):
        def ftp_REST(self, line): self.respond("502 REST disabled by test server")
        def ftp_APPE(self, path): self.respond("502 APPE disabled by test server")
    handler = NoResume
    pathlib.Path(sys.argv[3]).parent.joinpath("no-resume.txt").write_text("test fixture", encoding="ascii")
elif mode.startswith("ansi"):
    handler.encoding = "cp1250"
if mode == "utf8-no-feat":
    def no_utf8_feat(self, line):
        self.respond("211 No advertised extensions")
    handler.ftp_FEAT = no_utf8_feat
handler.authorizer = authorizer
class SlowTransfer(ThrottledDTPHandler):
    write_limit = 1048576
    read_limit = 1048576
    def send(self, data):
        sent = super().send(data)
        marker = root / "__drop_relay_once__"
        if self.file_obj and pathlib.Path(self.file_obj.name).name == "slow.bin" and self.tot_bytes_sent >= 2097152 and marker.exists():
            marker.unlink()
            self.handle_close()
        return sent
if not mode.startswith("ftps"): handler.dtp_handler = SlowTransfer
server = FTPServer(("127.0.0.1", 0), handler)
pathlib.Path(sys.argv[3]).write_text(str(server.socket.getsockname()[1]), encoding="ascii")
server.serve_forever()
