#!/usr/bin/env python3
"""
XGPU-B transplant patch for Cerbios 3.1.0 Hybrid Beta
Configuration: 64 MB stock RAM (OEM chips, 4 positions)
Needs:         the STANDARD Cerbios build (Cerbios_Hybrid_V3.1.0_Beta.bin)
Boards:        1.4 (Focus encoder). For 1.0/1.1 use patch_64MB_v1.0-1.1.py, for 1.2/1.3 patch_64MB_v1.2-1.3.py

Usage:  python patch_64MB_stock.py Cerbios_Hybrid_V3.1.0_Beta.bin [output.bin]
        (or run it with no arguments / drag the .bin onto it and it will ask)

After flashing, enable 480p and 720p in the video settings.
"""
import hashlib, os, struct, sys

CONFIG   = "64MB_stock"
NEED_256 = False
R1214, R122C, R10B8 = 0x88888888, 0x64646464, 0xDDDD0000
SET_6C23 = False

def mem_write(reg, val):
    return (bytes([0x03]) + struct.pack("<II", reg, val)).hex()

PATCHES = [
    (0x185, "0900000000c9060000", "090000000000000000", "Always take the 1.6 boot path (force16path)"),
    (0x84E, "09000000006a050000", "090000000005010000", "End of 1.6 path runs the Focus encoder init"),
    (0x9B6, "090000000048000000", "090000000002040000", "No Focus encoder found: continue to common code"),
    (0x9FE, "090000000083010000", "0900000000ba030000", "After Focus init: continue to common code"),
    (0x3BC, "07030000004812000f", mem_write(0x0F001248, 0x42),  "Memory-type byte (EEPROM C3) = 0x42"),
    (0x587, "0703000000b810000f", mem_write(0x0F0010B8, R10B8), "0x10B8 = %08X" % R10B8),
    (0x5C6, "07030000002c12000f", mem_write(0x0F00122C, R122C), "0x122C = %08X" % R122C),
    (0x5EA, "07030000001412000f", mem_write(0x0F001214, R1214), "0x1214 = %08X" % R1214),
]
if SET_6C23:
    PATCHES += [
        (0xDD3, "046c03008001083300", "046c03008001082300", "PCI 0:0.3 reg 0x6C = 0x00230801"),
        (0xDDC, "046c03008001083301", "046c03008001082301", "PCI 0:0.3 reg 0x6C = 0x01230801"),
    ]

def fail(msg):
    print("ERROR: " + msg)
    if len(sys.argv) < 2: input("Press Enter to exit...")
    sys.exit(1)

def main():
    src = sys.argv[1] if len(sys.argv) > 1 else input("Path to the stock Cerbios .bin: ").strip().strip('"')
    if not os.path.isfile(src): fail("file not found: " + src)
    d = bytearray(open(src, "rb").read())
    if len(d) != 262144: fail("expected a 256 KB Cerbios image, got %d bytes" % len(d))

    tag = bytes(d[0xDC1:0xDC1 + 9]).hex()
    build = {"03c002100f32010000": "standard", "03c002100f32020000": "256MB"}.get(tag)
    if build is None: fail("not a stock Cerbios 3.1.0 Hybrid Beta image")
    print("Detected: %s Cerbios 3.1.0 build" % build)
    if (build == "256MB") != NEED_256:
        fail("this configuration needs the %s build" % ("256MB" if NEED_256 else "standard"))

    for off, old, new, why in PATCHES:
        cur = bytes(d[off:off + 9]).hex()
        if cur == new: fail("0x%04X is already patched - start from the original image" % off)
        if cur != old: fail("unexpected bytes at 0x%04X - not a stock image" % off)
    for off, old, new, why in PATCHES:
        d[off:off + 9] = bytes.fromhex(new)
        print("  0x%04X  %s" % (off, why))

    dst = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        os.path.dirname(os.path.abspath(src)), "Cerbios_3.1.0_XGPUB_%s.bin" % CONFIG)
    open(dst, "wb").write(d)
    print("\nSaved: " + dst)
    print("MD5:   " + hashlib.md5(d).hexdigest())
    print("Flash it, then enable 480p and 720p in the video settings.")
    if len(sys.argv) < 2: input("Press Enter to exit...")

if __name__ == "__main__":
    main()
