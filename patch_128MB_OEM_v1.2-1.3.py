#!/usr/bin/env python3
"""
XGPU-B transplant patch for Cerbios 3.1.0 Hybrid Beta
Configuration: 1.2 / 1.3 board (Conexant encoder), 128 MB OEM Samsung chips (all 8 positions)
Needs:         the STANDARD Cerbios build (Cerbios_Hybrid_V3.1.0_Beta.bin)

Takes the 1.6 boot path only when the GPU reports revision D3 (XGPU-B), runs Cerbios's own
Conexant encoder setup after the 1.6 path (this board frags without it), forces the calibration
values and the memory-type byte (C3 = 0x42) as on 1.4, and keeps the stock pad values.
Same build with the 1.4 128 MB calibration values. Not yet tested on hardware.

Usage:  python patch_128MB_OEM_v1.2-1.3.py Cerbios_Hybrid_V3.1.0_Beta.bin [output.bin]
        (or run it with no arguments / drag the .bin onto it and it will ask)

After flashing, enable 480p and 720p in the video settings.
"""
import hashlib, os, struct, sys

CONFIG   = "v1.2-1.3_128MB_OEM"
NEED_256 = False

PATCHES = [
    (0x158, "081000000009000000", "060800000000000000", "GPU revision check (replaces SMBus board check)"),
    (0x161, "090000000024000000", "0800000000e5ffffff", "GPU revision check"),
    (0x16A, "060800000000000000", "1100c00000ff000000", "GPU revision check"),
    (0x173, "0800000000d3ffffff", "020000000f00000000", "Read GPU revision (PMC_BOOT_0)"),
    (0x17C, "1100c00000ff000000", "06ff00000000000000", "Mask GPU revision byte"),
    (0x185, "0900000000c9060000", "08d3000000c9060000", "Take the 1.6 path only for an XGPU-B (rev D3)"),
    (0x3BC, "07030000004812000f", "034812000f42000000", "Memory-type byte (EEPROM C3) = 0x42"),
    (0x57E, "060000ffff00000000", "06000000000000dddd", "0x10B8 = DDDD0000"),
    (0x5BD, "024812000f00000000", "0600000000aaaaaaaa", "0x122C = AAAAAAAA"),
    (0x5E1, "024812000f00000000", "060000000068686868", "0x1214 = 68686868"),
    (0x84E, "09000000006a050000", "0900000000b0010000", "End of 1.6 path runs the Conexant encoder setup"),
    (0xB8A, "1104c0000020000000", "09000000002e020000", "After Conexant setup: continue to common code"),
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
