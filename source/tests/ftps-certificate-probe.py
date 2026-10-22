"""Diagnose certificate substitution on our own loopback fixture only.
This deliberately unverified diagnostic does not change application TLS checks.
"""
import ftplib
import pathlib
import ssl
import subprocess
import sys
import time

sys.path.insert(0, sys.argv[1])
from cryptography import x509

root = pathlib.Path(sys.argv[2]).resolve()
root.mkdir(parents=True, exist_ok=True)
port = root / "port.txt"
with (root / "server-errors.log").open("wb") as errors:
    server = subprocess.Popen([sys.executable, str(pathlib.Path(__file__).with_name("ftp_fixture.py")), sys.argv[1], str(root / "server"), str(port), "ftps"], stdout=subprocess.DEVNULL, stderr=errors)
    try:
        deadline = time.monotonic() + 15
        while not port.exists() and time.monotonic() < deadline:
            time.sleep(0.05)
        if not port.exists():
            raise RuntimeError("fixture did not start")
        expected = x509.load_pem_x509_certificate((root / "ca.pem").read_bytes())
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE  # Inspection of an intercepted cert, never a production option.
        ftp = ftplib.FTP_TLS(context=context, timeout=10)
        ftp.connect("127.0.0.1", int(port.read_text()))
        ftp.login("tester", "test-password")
        control = x509.load_der_x509_certificate(ftp.sock.getpeercert(binary_form=True))
        ftp.prot_p()
        data, _ = ftp.ntransfercmd("RETR slow.bin")
        received = x509.load_der_x509_certificate(data.getpeercert(binary_form=True))
        print("Expected issuer:", expected.issuer.rfc4514_string())
        print("Control issuer:", control.issuer.rfc4514_string())
        print("Data issuer:", received.issuer.rfc4514_string())
        print("LOCAL_TLS_INTERCEPTION" if received.fingerprint(__import__("cryptography.hazmat.primitives.hashes", fromlist=["SHA256"]).SHA256()) != expected.fingerprint(__import__("cryptography.hazmat.primitives.hashes", fromlist=["SHA256"]).SHA256()) else "FIXTURE_CERTIFICATE_UNCHANGED")
        data.close()
        ftp.close()
    finally:
        server.terminate()
        server.wait(timeout=10)
