#!/usr/bin/env python3
"""
XGPU-B transplant patch for Prehistoricman's XBlastOS 0.65 RAMTESTER (RAM tester BIOS)
Configuration: 1.0 / 1.1 board (Conexant encoder), stock OEM Samsung RAM (64 MB or 128 MB)
Needs:         XBlastOS 0.65 RAMTESTER (Prehistoricman's stock image, or a build with the same
               start-up table such as RamTests v2)

Sets the RAM chips' mode register to 0x132 (the value for stock chips; the RAMTESTER's 0x232 is
for Prehistoricman's modules), 0x1210 = 0, and the donor 1.6 calibration (0x1214 = 68686868,
0x122C = AAAAAAAA). The same patch as 1.2 / 1.3. Tested with 8 OEM Samsung chips (128 MB) on
RamTests v2: boots.
Not yet tested with 64 MB.

Usage:  python patch_xblast_v1.0-1.1.py XBlastOS_0.65_RAMTESTER.bin [output.bin]
        (or run it with no arguments / drag the .bin onto it and it will ask)

Flash it to a spare bank. Press eject to start XBlast OS; its RAM tests are under Tools.
"""
import hashlib, os, sys

PATCHES = [
    (0x57E, "031012000f10000000", "031012000f00000000", "0x1210 = 00000000"),
    (0x5A2, "031412000f48084848", "031412000f68686868", "0x1214 = 68686868"),
    (0x5AB, "032c12000f88888888", "032c12000faaaaaaaa", "0x122C = AAAAAAAA"),
    (0x5C6, "031412000f09090909", "031412000f68686868", "0x1214 = 68686868"),
    (0x5E1, "031412000f09090909", "031412000f68686868", "0x1214 = 68686868"),
    (0x671, "03c002100f32020000", "03c002100f32010000", "RAM mode register 0x132 (stock chips)"),
    (0x67A, "03c802100f32020000", "03c802100f32010000", "RAM mode register 0x132 (stock chips)"),
    (0xD28, "03c002100f32020000", "03c002100f32010000", "RAM mode register 0x132 (stock chips)"),
    (0xD31, "03c802100f32020000", "03c802100f32010000", "RAM mode register 0x132 (stock chips)"),
]

def fail(msg):
    print("ERROR: " + msg)
    if len(sys.argv) < 2: input("Press Enter to exit...")
    sys.exit(1)

def main():
    src = sys.argv[1] if len(sys.argv) > 1 else input("Path to the XBlastOS 0.65 RAMTESTER .bin: ").strip().strip('"')
    if not os.path.isfile(src): fail("file not found: " + src)
    d = bytearray(open(src, "rb").read())
    if len(d) != 262144: fail("expected a 256 KB XBlast image, got %d bytes" % len(d))
    for off, old, new, why in PATCHES:
        cur = bytes(d[off:off + 9]).hex()
        if cur == new: fail("0x%04X is already patched - start from the original image" % off)
        if cur != old: fail("unexpected bytes at 0x%04X - not a stock XBlastOS 0.65 RAMTESTER image" % off)
    for off, old, new, why in PATCHES:
        d[off:off + 9] = bytes.fromhex(new)
        print("  0x%04X  %s" % (off, why))
    dst = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        os.path.dirname(os.path.abspath(src)), "XBlastOS_0.65_RAMTESTER_XGPUB_v1.0-1.1.bin")
    open(dst, "wb").write(d)
    print("\nSaved: " + dst)
    print("MD5:   " + hashlib.md5(d).hexdigest())
    print("Flash it to a spare bank. Press eject to start XBlast OS; its RAM tests are under Tools.")
    if len(sys.argv) < 2: input("Press Enter to exit...")

if __name__ == "__main__":
    main()
