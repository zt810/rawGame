import os
import re
import secrets
import sys
import pathlib

key_hex = os.environ.get("MICAY_KEY", "").strip()
if not key_hex:
    print("MICAY_KEY chua duoc dat; dung khoa mac dinh trong code.")
    sys.exit(0)

try:
    key = bytes.fromhex(key_hex)
except ValueError:
    print("MICAY_KEY khong phai chuoi hex hop le.")
    sys.exit(1)

if len(key) < 16:
    print("MICAY_KEY qua ngan (can it nhat 32 ky tu hex).")
    sys.exit(1)

p1 = bytes(secrets.randbelow(256) for _ in range(len(key)))
p2 = bytes(a ^ b for a, b in zip(p1, key))


def arr(data):
    return ", ".join(str(x) for x in data)


path = pathlib.Path("Assets/Scripts/MiCay/Core/Guard.cs")
src = path.read_text(encoding="utf-8")
src, n1 = re.subn(r"byte\[\] P1 = \{[^}]*\}", "byte[] P1 = { " + arr(p1) + " }", src, count=1)
src, n2 = re.subn(r"byte\[\] P2 = \{[^}]*\}", "byte[] P2 = { " + arr(p2) + " }", src, count=1)
if n1 != 1 or n2 != 1:
    print("Khong tim thay P1/P2 trong Guard.cs de thay the.")
    sys.exit(1)

path.write_text(src, encoding="utf-8")
print(f"Da nhung MICAY_KEY vao Guard.cs ({len(key)} byte).")
