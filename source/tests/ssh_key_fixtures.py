"""Generate disposable RSA fixtures for the SCP/SFTP authentication regression."""
import base64
from hashlib import sha1
import hmac
from pathlib import Path
import struct
import sys
from textwrap import wrap

sys.path.insert(0, sys.argv[1])
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.kdf.argon2 import Argon2id
from cryptography.hazmat.primitives import serialization

root = Path(sys.argv[2])
root.mkdir(parents=True, exist_ok=True)
password = b"fixture key phrase"
key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
(root / "authorized.pub").write_bytes(key.public_key().public_bytes(
    serialization.Encoding.OpenSSH, serialization.PublicFormat.OpenSSH))
(root / "id_encrypted").write_bytes(key.private_bytes(
    serialization.Encoding.PEM, serialization.PrivateFormat.OpenSSH,
    serialization.BestAvailableEncryption(password)))


def field(value):
    return struct.pack(">I", len(value)) + value


def mpint(value):
    return field(value.to_bytes((value.bit_length() + 8) // 8, "big"))


def base64_lines(label, data):
    lines = wrap(base64.b64encode(data).decode("ascii"), 64)
    return [f"{label}-Lines: {len(lines)}", *lines]


public_key = base64.b64decode((root / "authorized.pub").read_bytes().split()[1])
numbers = key.private_numbers()
key_blob = b"".join(map(mpint, (numbers.d, numbers.p, numbers.q, numbers.iqmp)))
for version in (2, 3):
    headers = [f"PuTTY-User-Key-File-{version}: ssh-rsa", "Encryption: aes256-cbc", "Comment: fixture"]
    headers.extend(base64_lines("Public", public_key))
    if version == 2:
        aes_key = (sha1(b"\x00\x00\x00\x00" + password).digest() + sha1(b"\x00\x00\x00\x01" + password).digest())[:32]
        iv = bytes(16)
        mac_key = sha1(b"putty-private-key-file-mac-key" + password).digest()
        digest = "sha1"
    else:
        salt = bytes.fromhex("aabbccddeeff00112233445566778899")
        material = Argon2id(salt=salt, length=80, iterations=3, lanes=1, memory_cost=8192).derive(password)
        aes_key, iv, mac_key = material[:32], material[32:48], material[48:]
        headers.extend(["Key-Derivation: Argon2id", "Argon2-Memory: 8192", "Argon2-Passes: 3",
                        "Argon2-Parallelism: 1", f"Argon2-Salt: {salt.hex()}"])
        digest = "sha256"
    padded = key_blob + bytes(-len(key_blob) % 16)
    mac = hmac.new(mac_key, b"".join(map(field, (b"ssh-rsa", b"aes256-cbc", b"fixture", public_key, padded))), digest)
    cipher = Cipher(algorithms.AES(aes_key), modes.CBC(iv)).encryptor()
    headers.extend(base64_lines("Private", cipher.update(padded) + cipher.finalize()))
    headers.append("Private-MAC: " + mac.hexdigest())
    (root / f"id_v{version}.ppk").write_text("\n".join(headers) + "\n", encoding="ascii")
