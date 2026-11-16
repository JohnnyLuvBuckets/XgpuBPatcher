# XGPU-B Transplant Cerbios Patcher

Patches a **stock Cerbios 3.1.0 Hybrid Beta** image so an original Xbox with a **transplanted 1.6 GPU (XGPU-B)** boots and runs stably on a 1.0 – 1.4 motherboard.

Stock Cerbios picks its memory and video setup from the board revision. A 1.0–1.4 board with an XGPU-B gets the wrong setup, which shows up as memory errors, FRAGs, wrong colours and freezes. This tool rewrites a handful of entries in Cerbios's startup table so the transplanted GPU gets the setup it needs.

**This tool does not include Cerbios.** You supply your own copy of the stock image and the patch is applied on your PC. Cerbios belongs to the Cerbios team; please don't redistribute patched images.

---

## Supported configurations

| RAM configuration | Cerbios build you need | Status |
|---|---|---|
| 64 MB stock (OEM chips, 4 positions) | Standard: `Cerbios_Hybrid_V3.1.0_Beta.bin` | Tested on a v1.4 Focus board: 0 FRAGs in 10 cold boots, 0 errors in memory, GPU and hard-drive stress tests |
| 128 MB OEM Samsung chips (all 8 positions) | Standard: `Cerbios_Hybrid_V3.1.0_Beta.bin` | Uses donor 1.6 calibration values verified on transplant boards |
| 128 MB Prehistoricman modules (4 positions) | 256MB: `Cerbios_Hybrid_V3.1.0_Beta_FOR_256MB_CONSOLES_ONLY.bin` | Tested: boots to the dashboard and runs stable |

**Board revisions:**
- **1.0 / 1.1 and 1.2 / 1.3 (Conexant encoder) with OEM RAM** use the 1.0/1.1 build. It takes the 1.6 boot path only when the GPU reports revision D3 (XGPU-B), forces the calibration values, uses 1.0/1.1 pad values and leaves the encoder setup alone. 128 MB uses the same calibration values as 1.4 with 128 MB. Verified on 1.0/1.1 with 64 MB; the 1.2/1.3 and 128 MB versions are not yet tested on hardware.
- **1.4 (Focus encoder)**, and **Prehistoricman modules on any board**, use the force16path + Focus init patch. The Focus encoder init only runs if a Focus encoder is detected.

**Not supported yet:** 256 MB (all 8 Prehistoricman modules) on a transplant board.

---

## How to use

### Option A: Windows app
1. Download `XgpuBPatcher.exe` from the [Releases](../../releases) page, or build it yourself (see below).
2. Click **Browse...** and pick your stock Cerbios image. The app detects whether it's the standard or 256MB build.
3. Choose your board revision and RAM configuration.
4. Click **Patch and save...**. Your original file is never modified.
5. Compare the MD5 shown with the table below.

Windows may show "Windows protected your PC" because the app isn't code-signed. Click **More info → Run anyway**, or build it from source.

### Option B: Python scripts (no .exe)
Requires Python 3, nothing else.

```
python patch_64MB_stock.py          Cerbios_Hybrid_V3.1.0_Beta.bin      (1.4, 64 MB)
python patch_128MB_OEM.py           Cerbios_Hybrid_V3.1.0_Beta.bin      (1.4, 128 MB OEM)
python patch_64MB_v1.0-1.3.py       Cerbios_Hybrid_V3.1.0_Beta.bin      (1.0 - 1.3, 64 MB)
python patch_128MB_OEM_v1.0-1.3.py  Cerbios_Hybrid_V3.1.0_Beta.bin      (1.0 - 1.3, 128 MB OEM)
python patch_128MB_modules.py       Cerbios_Hybrid_V3.1.0_Beta_FOR_256MB_CONSOLES_ONLY.bin   (any board)
```

You can also drag the .bin onto the script, or double-click the script and paste the path. The patched file is saved next to the original.

### Build the .exe yourself
No install needed. Windows already includes the compiler.
1. Put `XgpuBPatcher.cs` and `build.bat` in the same folder.
2. Double-click `build.bat`.

Or run this from a command prompt in that folder:
```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:XgpuBPatcher.exe XgpuBPatcher.cs
```

---

## Expected MD5s

If you start from the official stock images, your patched file should match exactly:

| Configuration | MD5 of patched image |
|---|---|
| 1.4, 64 MB stock | `fa8679098dc6bee64d0ec8593e751849` |
| 1.4, 128 MB OEM (8 chips) | `52dd22d43678a38287bc829849f5cb4a` |
| 1.0 – 1.3, 64 MB stock | `ea4ea429315539a46e93055252996484` |
| 1.0 – 1.3, 128 MB OEM (8 chips) | `80ad84d0063dd0164cea474fc2615986` |
| Any board, 128 MB Prehistoricman modules | `e3cb3481c58eb0ab77e8d4bf8e115cd4` |

## Safety checks

The app and scripts refuse to patch when:
- the file isn't a 256 KB stock Cerbios 3.1.0 Hybrid Beta image
- the Cerbios build doesn't match the selected RAM configuration
- the image has already been patched
- any byte to be changed isn't what the stock image contains

---

## After flashing

- **Enable 480p and 720p** in your dashboard's video settings. On transplant boards, 480i can drop the picture for a few seconds at a time. It's a known open issue that has been reported to the Cerbios team.
- Always flash with a way to recover, such as a spare modchip bank or a programmer.

---

## What it changes

All patches are in Cerbios's startup (x-code) table. Both builds have the same table in this range, so the offsets apply to both.

| Offset | Change |
|---|---|
| 0x185 | Always take the 1.6 boot path (force16path) |
| 0x84E | End of the 1.6 path runs the Focus encoder init |
| 0x9B6 | No Focus encoder found → continue to common code |
| 0x9FE | After Focus init → continue to common code |
| 0x3BC | Memory-type byte (EEPROM C3) fixed at 0x42 |
| 0x587 | GPU reg 0x10B8 fixed (DDDD0000) |
| 0x5C6 | GPU reg 0x122C fixed (64646464 for 64 MB, AAAAAAAA for 128 MB) |
| 0x5EA | GPU reg 0x1214 fixed (88888888 for 64 MB, 68686868 for 128 MB) |
| 0xDD3 / 0xDDC | *128 MB modules only:* PCI 0:0.3 reg 0x6C = 0x00230801 / 0x01230801 |

The 1214 / 122C / 10B8 values replace the calibration Cerbios would normally read from EEPROM bytes C0–C2. That calibration is tuned for the original board's GPU, not the transplanted one.

---

## Hardware guides

Board modifications needed after the GPU swap:
- [XGPU-B Swap Guide: 1.0 / 1.1 Xbox](guides/XGPU-B_Swap_Guide_1.0-1.1.pdf)
- [XGPU-B Swap Guide: 1.2 / 1.3 Xbox](guides/XGPU-B_Swap_Guide_1.2-1.3.pdf)
- [XGPU-B Swap Guide: 1.4 Xbox](guides/XGPU-B_Swap_Guide_1.4.pdf)

Board photos in the guides are based on the motherboard scans at retro.jnltech.net.

---

## Code signing policy

Windows releases of `XgpuBPatcher.exe` are built automatically by [GitHub Actions](.github/workflows/build.yml) from this repository's source.

**Status:** an application for free code signing through the SignPath Foundation open-source program is pending. Until it's approved, release builds are unsigned. Once approved, releases will carry this notice: *Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).*

**Team roles**
- Committers and reviewers: [JohnnyLuvBuckets](https://github.com/JohnnyLuvBuckets)
- Approvers: [JohnnyLuvBuckets](https://github.com/JohnnyLuvBuckets)

**Privacy policy:** this program does not transfer any information to other networked systems. It only reads the Cerbios image you select and writes the patched copy you save.

---

## Credits

- **The Cerbios team**, for Cerbios
- **Prehistoricman**, for the 128/256 MB RAM interposer modules and RAM-tester XBlast
- **Shawn Shakir**, for the original force16path Cerbios for XGPU-B boards
- **Claude (Anthropic)**, for co-developing the patch, the Windows app, the Python scripts and the documentation
- **redoverlord**, for being the first person to attempt the 1.6 GPU transplant

## Disclaimer

Provided as-is, with no warranty. Flashing a BIOS can leave a console unbootable if something goes wrong. Use at your own risk.
