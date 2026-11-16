// XGPU-B Transplant Cerbios Patcher
// Patches a stock Cerbios 3.1.0 Hybrid Beta image (standard build) so a
// transplanted 1.6 GPU (XGPU-B) works on a 1.0-1.4 board.
//
// Build on any Windows 10/11 PC (no install needed, uses the built-in .NET Framework compiler):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:XgpuBPatcher.exe XgpuBPatcher.cs
//
// Written for C# 5 so the built-in compiler accepts it.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("XGPU-B Transplant Cerbios Patcher")]
[assembly: AssemblyProduct("XgpuBPatcher")]
[assembly: AssemblyDescription("Patches stock Cerbios 3.1.0 for Xbox boards with a transplanted XGPU-B")]
[assembly: AssemblyCopyright("Copyright (c) 2026 JohnnyLuvBuckets, MIT License")]
[assembly: AssemblyVersion("1.7.3.0")]
[assembly: AssemblyFileVersion("1.7.3.0")]

namespace XgpuBPatcher
{
    class Patch
    {
        public int Offset;
        public byte[] Old;
        public byte[] New;
        public string Why;
        public Patch(int offset, string oldHex, string newHex, string why)
        {
            Offset = offset; Old = Hex(oldHex); New = Hex(newHex); Why = why;
        }
        public Patch(int offset, byte[] oldBytes, byte[] newBytes, string why)
        {
            Offset = offset; Old = oldBytes; New = newBytes; Why = why;
        }
        public Patch(int offset, string oldHex, byte[] newBytes, string why)
        {
            Offset = offset; Old = Hex(oldHex); New = newBytes; Why = why;
        }
        public static byte[] Hex(string s)
        {
            byte[] b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }
    }

    class RamConfig
    {
        public string Name;
        public bool Needs256Build;
        public uint R1214, R122C, R10B8;
        public bool Set6C23;
        public string Status;
        public RamConfig(string name, bool needs256, uint r1214, uint r122c, uint r10b8, bool set6c23, string status)
        {
            Name = name; Needs256Build = needs256; R1214 = r1214; R122C = r122c; R10B8 = r10b8; Set6C23 = set6c23; Status = status;
        }
        public override string ToString() { return Name; }
    }

    static class Patcher
    {
        public const int ImageSize = 262144;

        // x-code mem_write entry: opcode 0x03, register (LE), value (LE)
        static byte[] MemWrite(uint reg, uint val)
        {
            byte[] b = new byte[9];
            b[0] = 0x03;
            BitConverter.GetBytes(reg).CopyTo(b, 1);
            BitConverter.GetBytes(val).CopyTo(b, 5);
            return b;
        }

        // x-code and/or entry: opcode 0x06, AND mask 0, OR value (LE) -> result = value
        static byte[] AndOr(uint val)
        {
            byte[] b = new byte[9];
            b[0] = 0x06;
            BitConverter.GetBytes(val).CopyTo(b, 5);
            return b;
        }

        // 0 = not a recognised Cerbios 3.1.0 image, 1 = standard build, 2 = 256MB build
        public static int DetectBuild(byte[] d)
        {
            if (d == null || d.Length != ImageSize) return 0;
            if (Same(d, 0xDC1, Patch.Hex("03c002100f32010000"))) return 1;
            if (Same(d, 0xDC1, Patch.Hex("03c002100f32020000"))) return 2;
            return 0;
        }

        public static bool Same(byte[] d, int off, byte[] b)
        {
            if (off + b.Length > d.Length) return false;
            for (int i = 0; i < b.Length; i++) if (d[off + i] != b[i]) return false;
            return true;
        }

        // board: 0 = 1.0/1.1, 1 = 1.2/1.3, 2 = 1.4.  ramIndex: 0 = 64 MB stock, 1 = 128 MB OEM
        public static List<Patch> Build(RamConfig ram, int board, int ramIndex)
        {
            List<Patch> p = new List<Patch>();
            if (board == 1 && ramIndex != 2)
            {
                // 1.2/1.3 (Conexant) with OEM RAM: verified build "X3". GPU check (1.6 path only for an
                // XGPU-B), calibration and C3 = 0x42 as on 1.4, stock pads, and Cerbios's own Conexant
                // encoder setup after the 1.6 path (this board frags without it: register C4).
                // The same calibration for 64 MB and 128 MB: the 128 MB donor values gave no picture on 1.2/1.3.
                p.Add(new Patch(0x158, "081000000009000000", "060800000000000000", "GPU revision check (replaces SMBus board check)"));
                p.Add(new Patch(0x161, "090000000024000000", "0800000000e5ffffff", "GPU revision check"));
                p.Add(new Patch(0x16A, "060800000000000000", "1100c00000ff000000", "GPU revision check"));
                p.Add(new Patch(0x173, "0800000000d3ffffff", "020000000f00000000", "Read GPU revision (PMC_BOOT_0)"));
                p.Add(new Patch(0x17C, "1100c00000ff000000", "06ff00000000000000", "Mask GPU revision byte"));
                p.Add(new Patch(0x185, "0900000000c9060000", "08d3000000c9060000", "Take the 1.6 path only for an XGPU-B (rev D3)"));
                p.Add(new Patch(0x3BC, "07030000004812000f", MemWrite(0x0F001248, 0x42), "Memory-type byte (EEPROM C3) = 0x42"));
                p.Add(new Patch(0x57E, "060000ffff00000000", AndOr(0xDDDD0000), "0x10B8 = DDDD0000"));
                p.Add(new Patch(0x5BD, "024812000f00000000", AndOr(0x64646464), "0x122C = 64646464"));
                p.Add(new Patch(0x5E1, "024812000f00000000", AndOr(0x88888888), "0x1214 = 88888888"));
                p.Add(new Patch(0x84E, "09000000006a050000", "0900000000b0010000", "End of 1.6 path runs the Conexant encoder setup"));
                p.Add(new Patch(0xB8A, "1104c0000020000000", "09000000002e020000", "After Conexant setup: continue to common code"));
                return p;
            }
            if (board == 0 && ramIndex != 2)
            {
                // 1.0/1.1 (Conexant) with OEM RAM: based on the verified 1.0/1.1 "gpurev_fixedcal2" build.
                // Takes the 1.6 path only when the GPU reports revision D3 (XGPU-B), keeps the EEPROM-read
                // structure with fixed calibration, uses 1.0/1.1 pad values and leaves the encoder setup
                // alone. 128 MB uses the 1.4 128 MB calibration.
                p.Add(new Patch(0x158, "081000000009000000", "060800000000000000", "GPU revision check (replaces SMBus board check)"));
                p.Add(new Patch(0x161, "090000000024000000", "0800000000e5ffffff", "GPU revision check"));
                p.Add(new Patch(0x16A, "060800000000000000", "1100c00000ff000000", "GPU revision check"));
                p.Add(new Patch(0x173, "0800000000d3ffffff", "020000000f00000000", "Read GPU revision (PMC_BOOT_0)"));
                p.Add(new Patch(0x17C, "1100c00000ff000000", "06ff00000000000000", "Mask GPU revision byte"));
                p.Add(new Patch(0x185, "0900000000c9060000", "08d3000000c9060000", "Take the 1.6 path only for an XGPU-B (rev D3)"));
                p.Add(new Patch(0x57E, "060000ffff00000000", AndOr(ram.R10B8), "0x10B8 = " + ram.R10B8.ToString("X8")));
                p.Add(new Patch(0x5BD, "024812000f00000000", AndOr(ram.R122C), "0x122C = " + ram.R122C.ToString("X8")));
                p.Add(new Patch(0x5E1, "024812000f00000000", AndOr(ram.R1214), "0x1214 = " + ram.R1214.ToString("X8")));
                p.Add(new Patch(0x72E, "033012000fffffffff", "033012000ff8f8f8f8", "Pad 0x1230 = F8F8F8F8"));
                p.Add(new Patch(0x737, "033412000faaaaaaaa", "033412000f87878787", "Pad 0x1234 = 87878787"));
                p.Add(new Patch(0x740, "033812000faaaaaaaa", "033812000fbbbbbbbb", "Pad 0x1238 = BBBBBBBB"));
                p.Add(new Patch(0x749, "033c12000f8b8b8b8b", "033c12000f54545454", "Pad 0x123C = 54545454"));
                p.Add(new Patch(0x752, "034012000fffffffff", "034012000ff8f8f8f8", "Pad 0x1240 = F8F8F8F8"));
                p.Add(new Patch(0x75B, "034412000f8b8b8b8b", "034412000f65656565", "Pad 0x1244 = 65656565"));
                p.Add(new Patch(0x764, "034812000f8b8b8b8b", "034812000f96969696", "Pad 0x1248 = 96969696"));
                return p;
            }
            p.Add(new Patch(0x185, "0900000000c9060000", "090000000000000000", "Always take the 1.6 boot path (force16path)"));
            p.Add(new Patch(0x84E, "09000000006a050000", "090000000005010000", "End of 1.6 path runs the Focus encoder init"));
            p.Add(new Patch(0x9B6, "090000000048000000", "090000000002040000", "No Focus encoder found: continue to common code"));
            p.Add(new Patch(0x9FE, "090000000083010000", "0900000000ba030000", "After Focus init: continue to common code"));
            p.Add(new Patch(0x3BC, "07030000004812000f", MemWrite(0x0F001248, 0x42), "Memory-type byte (EEPROM C3) = 0x42"));
            p.Add(new Patch(0x587, "0703000000b810000f", MemWrite(0x0F0010B8, ram.R10B8), "0x10B8 = " + ram.R10B8.ToString("X8") + " (EEPROM C2)"));
            p.Add(new Patch(0x5C6, "07030000002c12000f", MemWrite(0x0F00122C, ram.R122C), "0x122C = " + ram.R122C.ToString("X8") + " (EEPROM C1)"));
            p.Add(new Patch(0x5EA, "07030000001412000f", MemWrite(0x0F001214, ram.R1214), "0x1214 = " + ram.R1214.ToString("X8") + " (EEPROM C0)"));
            if (ram.Set6C23)
            {
                p.Add(new Patch(0xDD3, "046c03008001083300", "046c03008001082300", "PCI 0:0.3 reg 0x6C = 0x00230801"));
                p.Add(new Patch(0xDDC, "046c03008001083301", "046c03008001082301", "PCI 0:0.3 reg 0x6C = 0x01230801"));
            }
            return p;
        }

        // Returns null on success, or an error message. Log receives one line per patch.
        public static string Apply(byte[] d, List<Patch> patches, StringBuilder log)
        {
            foreach (Patch p in patches)
            {
                if (Same(d, p.Offset, p.New))
                    return string.Format("0x{0:X4} is already patched. Start from the original, unmodified Cerbios image.", p.Offset);
                if (!Same(d, p.Offset, p.Old))
                    return string.Format("0x{0:X4} has unexpected bytes. This is not a stock Cerbios 3.1.0 Hybrid Beta image of the selected build.", p.Offset);
            }
            foreach (Patch p in patches)
            {
                p.New.CopyTo(d, p.Offset);
                log.AppendLine(string.Format("0x{0:X4}  {1}", p.Offset, p.Why));
            }
            return null;
        }

        public static string Md5(byte[] d)
        {
            using (MD5 m = MD5.Create())
            {
                byte[] h = m.ComputeHash(d);
                StringBuilder sb = new StringBuilder();
                foreach (byte x in h) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    // Prehistoricman's XBlastOS 0.65 RAMTESTER (his stock image or the RamTests v2 build: same startup table).
    static class XBlastPatcher
    {
        // null when no verified XBlast build exists yet for this board and RAM.
        static readonly string StockTable64 =
            "BIQIAIABgAAABBAIAIABgAAABAQIAIADAAAAEUmAAAAIAAAAEdmAAAAAAAAAESaAAAABAAAABEzwAIABAAAABBjwAIAAAQEABIQA" +
            "AID///8HBCDwAIAAD/APBCTwAIAA8PD3BBAAAYAAAAAPBBQAAYAAAADwBAQAAYAHAAAABATwAIAHAAAABAQJAIABAAAABBQJAIAB" +
            "wAAABBgJAIABwgAACQAAAAAAAAAAA7AQAA9hNGMHA8wQAA8AAGZmAgAQEA8AAAAABgAADAAAAAAACAAAAAAtAAAAAgAQEA8AAAAA" +
            "Bv//8+EAAACABwMAAAAAEBAPA7gQAA8AAO7uCQAAAABaAAAACAAADAAtAAAAAgAQEA8AAAAABv//8+EAAAyGBwMAAAAAEBAPA7gQ" +
            "AA8AAP//CQAAAAAkAAAAAgAQEA8AAAAABv//8+EAAAyCBwMAAAAAEBAPA7gQAA8AABERA9QQAA8JAAAAA7QQAA8AAAAAA7wQAA9m" +
            "WAAAA8QQAA9YyFEDA8gQAA9nfQAwA9gQAA8AAAAAA9wQAA81NkKgA+gQAA/GWGUMAwACEA8DAQcDAxAEEA8WAAARAzADEA+IiISE" +
            "AywDEA//z///AygDEA8BAAAAAzgDEA/fAAAAEQDCAABwAAAAEQTAAADUAAAAEQjAAAAMAAAAEQbAAAAAAAAAEQLAAAAKAAAAEgDA" +
            "AAAAAAAACBAAAAAJAAAACQAAAAAkAAAABggAAAAAAAAACAAAAADT////EQDAAAD/AAAACQAAAABIAAAAEQDAAAAQAAAAEQjAAAAN" +
            "AAAAEQbAAAAgAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAACQAAAADCAQAAEQDAAAAQAAAAEQTAAACKAAAA" +
            "EQjAAAC6AAAAEQbAAAA/AAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAAAJAAAACQAAAAAtAAAABggAAAAAAAAACAAAAADT////EQDA" +
            "AAD/AAAAEQDAAAAQAAAACQAAAACoAwAAEQDAAAAQAAAAEQjAAABsAAAAEQbAAABGAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu" +
            "////EQDAAAAQAAAAEQjAAAC4AAAAEQbAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAADOAAAA" +
            "EQbAAAAZAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAADGAAAAEQbAAACcAAAAEQLAAAAKAAAAEgDA" +
            "AAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAAAyAAAAEQbAAAAIAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQ" +
            "AAAAEQjAAADEAAAAEQbAAAABAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQTAAAAgAAAAEQjAAAABAAAA" +
            "EQbAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQTAAAAhAAAAEQjAAAABAAAAEQLAAAAKAAAAEgDA" +
            "AAAAAAAACBAAAADu////EQDAAAAQAAAAEgbAAAAAAAAAAwAFaA8BHAEAAwwFaA8ABAoAAyASAA8AAAAAAygSAA8AAAAAA2QSAA8A" +
            "AAAAAxASAA8QAAAAAgAQEA8AAAAABgAAAAYAAAAACAAAAAAbAAAAAxQSAA9ICEhIAywSAA+IiIiICQAAAAA2AAAACAAAAAYbAAAA" +
            "AxQSAA8JCQkJAywSAA+qqqqqCQAAAAASAAAAAxQSAA8JCQkJAywSAA+qqqqqAzASAA//////AzQSAA+qqqqqAzgSAA+qqqqqAzwS" +
            "AA+Li4uLA0ASAA//////A0QSAA+Li4uLA0gSAA+Li4uLA9QCEA8BAAAAA8QCEA9CABAAA8wCEA9CABAAA8ACEA8RAAAAA8gCEA8R" +
            "AAAAA8ACEA8yAAAAA8gCEA8yAAAAA8ACEA8yAgAAA8gCEA8yAgAAA9ACEA8BAAAAA9ACEA8BAAAAAxACEA8AAACAA0wSAA+Lqouq" +
            "A1ASAA+LqgAAAygCEA//BRIIAxgSAA8AAAEABWAIAIAAAAAABv////8ABAAABwQAAABgCACABEwIAIDe/QAABJwIAIAHxxyHBbQI" +
            "AIAAAAAABv/w//8ADwAABwQAAAC0CACABEADAIDAwPDwBEQDAIAAAMAABFwDAIAAAAcECQAAAACuBgAAEQDCAABwAAAAA7AQAA8Q" +
            "AAABA8wQAA8AAGZmCQAAAABWAQAAA9QQAA8PAAAAA7QQAA8AAAAAA7wQAA9gWAAAA8QQAA8AAKqqA8gQAA9nfQAAA9gQAA8AAAAA" +
            "A9wQAA8AAAAAA+gQAA8AAAAEAwACEA8DAQcDAxAEEA8WAAARAzADEA+IiISEAywDEA//z///AygDEA8BAAAAAzgDEA/fAAAAEQTA" +
            "AAAgAAAAEQjAAAABAAAAEQbAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQTAAAAhAAAAEQjAAAAB" +
            "AAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEgbAAAAAAAAACFAAAAAJAAAACQAAAAAJAAAABGwDAIAAAAAB" +
            "AwAFaA8BHAEAAwwFaA8ABAoAAyASAA8AAAAAAygSAA8AAAAAA2QSAA8AAAAAAxASAA8AAAAACQAAAAAJAAAABDwYAIDDAAAAEQTA" +
            "AACpAAAABTwYAIAAAAAABxEAAAAIwAAAEQDAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAABwQAAAA8EACABggAAAAAAAAACAgAAAAJ" +
            "AAAACQAAAADT////BTwQAIAAAAAABv8AAAAAAAAACBAAAACL////EgbAAAAAAAAABv8AAAAAAAAABwMAAABIEgAPBTwYAIAAAAAA" +
            "Bv8AAAAAAAAACMMAAACiAAAAAkgSAA8AAAAACEIAAAAbAAAAAgAQEA8AAAAABv//8+EAAAyACQAAAABaAAAACEAAAAAbAAAAAgAQ" +
            "EA8AAAAABv//8+EAAAiACQAAAAA2AAAACAIAAAAbAAAAAgAQEA8AAAAABv//8+EAAASACQAAAAASAAAAAgAQEA8AAAAABv//8+EA" +
            "AACABwMAAAAAEBAPBDwYAIDCAAAACQAAAACz/v//AkgSAA8AAAAABgEAAAAAAAAACAAAAABoAQAAAkgSAA8AAAAABgIAAAAAAAAA" +
            "CAAAAABxAQAAAkgSAA8AAAAABgQAAAAAAAAACAAAAAB6AQAAAkgSAA8AAAAABggAAAAAAAAACAAAAACDAQAAAkgSAA8AAAAABhAA" +
            "AAAAAAAACAAAAACMAQAAAkgSAA8AAAAABiAAAAAAAAAACAAAAACVAQAAAkgSAA8AAAAABkAAAAAAAAAACAAAAACeAQAAAkgSAA8A" +
            "AAAABoAAAAAAAAAACAAAAACnAQAABTwYAIAAAAAABv8AAAAAAAAACMIAAAAtAAAAAkgSAA8AAAAABgAA//8AAAAABwMAAAC4EAAP" +
            "BDwYAIDBAAAACQAAAAA0/P//BTwYAIAAAAAABv8AAAAAAAAACMEAAAAkAAAAAkgSAA8AAAAABwMAAAAsEgAPBDwYAIDAAAAACQAA" +
            "AABU/f//AkgSAA8AAAAABwMAAAAUEgAPBDwYAIDDAAAACQAAAAAgAQAAAkgSAA8AAAAABv////8AAQEBBwMAAABIEgAPCQAAAAB9" +
            "/v//AkgSAA8AAAAABv////8AAgICBwMAAABIEgAPCQAAAAB0/v//AkgSAA8AAAAABv////8ABAQEBwMAAABIEgAPCQAAAABr/v//" +
            "AkgSAA8AAAAABv////8ACAgIBwMAAABIEgAPCQAAAABi/v//AkgSAA8AAAAABv////8AEBAQBwMAAABIEgAPCQAAAABZ/v//AkgS" +
            "AA8AAAAABv////8AICAgBwMAAABIEgAPCQAAAABQ/v//AkgSAA8AAAAABv////8AQEBABwMAAABIEgAPCQAAAABH/v//AkgSAA8A" +
            "AAAABv////8AgICABwMAAABIEgAPCQAAAAA1/v//A0gSAA8AAAAAAzASAA//////AzQSAA+qqqqqAzgSAA+qqqqqAzwSAA+Li4uL" +
            "A0ASAA//////A0QSAA+Li4uLA0gSAA+Li4uLA9QCEA8BAAAAA8QCEA8AABAAA8wCEA8AABAAA8ACEA8RAAAAA8gCEA8RAAAAA8AC" +
            "EA8yAAAAA8gCEA8yAAAAA8ACEA8yAgAAA8gCEA8yAgAAA9ACEA8BAAAAA9ACEA8BAAAAAxACEA8AAACAA0wSAA+LqouqA1ASAA90" +
            "VAAAAygCEA//BRIIAxgSAA8AAAAABWAIAIAAAAAABv////8ABAAABwQAAABgCACABEwIAIDe/QAABJwIAIAHxxyHBbQIAIAAAAAA" +
            "Bv/w//8ADwAABwQAAAC0CACABEADAIDAwPDwBEQDAIAAAAAABFwDAIAAAAAABGwDAIABCCMABGwDAIABCCMBCQAAAAAAAAAACQAA" +
            "AAAAAAAAAwQCEA8QkEQRA0gQAAC+uv7KAhASAA8AAAAABv////8AAQAABwMAAAAQEgAPAkgAAAAAAAAACL66/sotAAAAAwQCEA8A" +
            "gEQRAhASAA8AAAAABv/+//8AAAAABwMAAAAQEgAPCQAAAABIAAAABIQAAID///8PAwACEA8DAQcDAzQSAAjvvq3eAjQSAAgAAAAA" +
            "CO++rd4JAAAACQAAAAASAAAABIQAAID///8HAwACEA8DAAcDBDwQAIAAAAAABDwYAIAAAAAAEQDAAAAQAAAAEQTAAAAgAAAAEQjA" +
            "AAATAAAAEQbAAAAPAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAAASAAAAEQbAAADwAAAAEQLAAAAK" +
            "AAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAABCDwAIAA/fD9BBAAAYAAAAD9AwAAAAC48A8AAwQAAAD//+AA7gYIAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";
        static readonly string Board14Table64 =
            "BIQIAIABgAAABBAIAIABgAAABAQIAIADAAAAEUmAAAAIAAAAEdmAAAAAAAAAESaAAAABAAAABEzwAIABAAAABBjwAIAAAQEABIQA" +
            "AID///8HBCDwAIAAD/APBCTwAIAA8PD3BBAAAYAAAAAPBBQAAYAAAADwBAQAAYAHAAAABATwAIAHAAAABAQJAIABAAAABBQJAIAB" +
            "wAAABBgJAIABwgAAEQDCAABwAAAAEQTAAADhAAAAEQjAAAAAAAAAEQbAAAAAAAAAEQLAAAANAAAAEgDAAAAAAAAACBAAAAAJAAAA" +
            "CQAAAAAkAAAABggAAAAAAAAACAAAAADT////EQDAAAD/AAAACQAAAAAAAAAAEgnAAAAAAAAAEgnAAAAAAAAAEgnAAAAAAAAAEgnA" +
            "AAAAAAAAEQDAAAD/AAAAA7AQAA8QAAABA8wQAA8AAGZmCQAAAABWAQAAA9QQAA8PAAAAA7QQAA8AAAAAA7wQAA9gWAAAA8QQAA8A" +
            "AKqqA8gQAA9nfQAAA9gQAA8AAAAAA9wQAA8AAAAAA+gQAA8AAAAEAwACEA8DAQcDAxAEEA8WAAARAzADEA+IiISEAywDEA//z///" +
            "AygDEA8BAAAAAzgDEA/fAAAAEQTAAAAgAAAAEQjAAAABAAAAEQbAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDA" +
            "AAAQAAAAEQTAAAAhAAAAEQjAAAABAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEgbAAAAAAAAACFAAAAAJ" +
            "AAAACQAAAAAJAAAABGwDAIAAAAABAwAFaA8BHAEAAwwFaA8ABAoAAyASAA8AAAAAAygSAA8AAAAAA2QSAA8AAAAAAxASAA8AAAAA" +
            "CQAAAAAJAAAABDwYAIDDAAAAEQTAAACpAAAABTwYAIAAAAAABxEAAAAIwAAAEQDAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAABwQA" +
            "AAA8EACABggAAAAAAAAACAgAAAAJAAAACQAAAADT////BTwQAIAAAAAABv8AAAAAAAAACBAAAACL////EgbAAAAAAAAABv8AAAAA" +
            "AAAAA0gSAA9CAAAABTwYAIAAAAAABv8AAAAAAAAACMMAAACiAAAAAkgSAA8AAAAACEIAAAAbAAAAAgAQEA8AAAAABv//8+EAAAyA" +
            "CQAAAABaAAAACEAAAAAbAAAAAgAQEA8AAAAABv//8+EAAAiACQAAAAA2AAAACAIAAAAbAAAAAgAQEA8AAAAABv//8+EAAASACQAA" +
            "AAASAAAAAgAQEA8AAAAABv//8+EAAACABwMAAAAAEBAPBDwYAIDCAAAACQAAAACz/v//AkgSAA8AAAAABgEAAAAAAAAACAAAAABo" +
            "AQAAAkgSAA8AAAAABgIAAAAAAAAACAAAAABxAQAAAkgSAA8AAAAABgQAAAAAAAAACAAAAAB6AQAAAkgSAA8AAAAABggAAAAAAAAA" +
            "CAAAAACDAQAAAkgSAA8AAAAABhAAAAAAAAAACAAAAACMAQAAAkgSAA8AAAAABiAAAAAAAAAACAAAAACVAQAAAkgSAA8AAAAABkAA" +
            "AAAAAAAACAAAAACeAQAAAkgSAA8AAAAABoAAAAAAAAAACAAAAACnAQAABTwYAIAAAAAABv8AAAAAAAAACMIAAAAtAAAAAkgSAA8A" +
            "AAAABgAA//8AAAAAA7gQAA8AAN3dBDwYAIDBAAAACQAAAAA0/P//BTwYAIAAAAAABv8AAAAAAAAACMEAAAAkAAAAAkgSAA8AAAAA" +
            "AywSAA+qqqqqBDwYAIDAAAAACQAAAABU/f//AkgSAA8AAAAAAxQSAA+IiIiIBDwYAIDDAAAACQAAAAAgAQAAAkgSAA8AAAAABv//" +
            "//8AAQEBBwMAAABIEgAPCQAAAAB9/v//AkgSAA8AAAAABv////8AAgICBwMAAABIEgAPCQAAAAB0/v//AkgSAA8AAAAABv////8A" +
            "BAQEBwMAAABIEgAPCQAAAABr/v//AkgSAA8AAAAABv////8ACAgIBwMAAABIEgAPCQAAAABi/v//AkgSAA8AAAAABv////8AEBAQ" +
            "BwMAAABIEgAPCQAAAABZ/v//AkgSAA8AAAAABv////8AICAgBwMAAABIEgAPCQAAAABQ/v//AkgSAA8AAAAABv////8AQEBABwMA" +
            "AABIEgAPCQAAAABH/v//AkgSAA8AAAAABv////8AgICABwMAAABIEgAPCQAAAAA1/v//A0gSAA8AAAAAAzASAA//////AzQSAA+q" +
            "qqqqAzgSAA+qqqqqAzwSAA+Li4uLA0ASAA//////A0QSAA+Li4uLA0gSAA+Li4uLA9QCEA8BAAAAA8QCEA8AABAAA8wCEA8AABAA" +
            "A8ACEA8RAAAAA8gCEA8RAAAAA8ACEA8yAAAAA8gCEA8yAAAAA9ACEA8BAAAAA9ACEA8BAAAAAxACEA8AAACAA0wSAA+LqouqA1AS" +
            "AA90VAAAAygCEA//BRIIAxgSAA8AAAAABWAIAIAAAAAABv////8ABAAABwQAAABgCACABEwIAIDe/QAABJwIAIAHxxyHBbQIAIAA" +
            "AAAABv/w//8ADwAABwQAAAC0CACABEADAIDAwPDwBEQDAIAAAAAABFwDAIAAAAAACQAAAAAFAQAAA7AQAA9hNGMHA8wQAA8AAGZm" +
            "AgAQEA8AAAAABgAADAAAAAAACAAAAAAtAAAAAgAQEA8AAAAABv//8+EAAACABwMAAAAAEBAPA7gQAA8AAO7uCQAAAAAtAAAACAAA" +
            "DAD0+f//AgAQEA8AAAAABv//8+EAAAyGBwMAAAAAEBAPA7gQAA8AAP//A9QQAA8JAAAAA7QQAA8AAAAAA7wQAA9mWAAAA8QQAA9Y" +
            "yFEDA8gQAA9nfQAwA9gQAA8AAAAAA9wQAA81NkKgA+gQAA/GWGUMAwACEA8DAQcDAxAEEA8WAAARAzADEA+IiISEAywDEA//z///" +
            "AygDEA8BAAAAAzgDEA/fAAAAEQTAAADUAAAAEQjAAAAMAAAAEQbAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAAAJAAAACQAA" +
            "AAAkAAAABggAAAAAAAAACAAAAADT////EQDAAAD/AAAACQAAAAACBAAAEQDAAAAQAAAAEQjAAAANAAAAEQbAAAAgAAAAEQLAAAAK" +
            "AAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAACQAAAAC6AwAAEQTAAACKAAAAEQjAAAC6AAAAEQbAAAA/AAAAEQLAAAAKAAAA" +
            "EgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAABsAAAAEQbAAABGAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDA" +
            "AAAQAAAAEQjAAAC4AAAAEQbAAAAAAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAADOAAAAEQbAAAAZ" +
            "AAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAADGAAAAEQbAAACcAAAAEQLAAAAKAAAAEgDAAAAAAAAA" +
            "CBAAAADu////EQDAAAAQAAAAEQjAAAAyAAAAEQbAAAAIAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjA" +
            "AADEAAAAEQbAAAABAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQTAAAAgAAAAEQjAAAABAAAAEQbAAAAA" +
            "AAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQTAAAAhAAAAEQjAAAABAAAAEQLAAAAKAAAAEgDAAAAAAAAA" +
            "CBAAAADu////EQDAAAAQAAAAEgbAAAAAAAAACFAAAAAJAAAACQAAAAAJAAAABGwDAIAAAAABAwAFaA8BHAEAAwwFaA8ABAoAAyAS" +
            "AA8AAAAAAygSAA8AAAAAA2QSAA8AAAAAAxASAA8QAAAAAgAQEA8AAAAABgAAAAYAAAAACAAAAAAbAAAAAxQSAA9ICEhIAywSAA+I" +
            "iIiICQAAAAASAAAAAxQSAA8JCQkJAywSAA+qqqqqAzASAA//////AzQSAA+qqqqqAzgSAA+qqqqqAzwSAA+Li4uLA0ASAA//////" +
            "A0QSAA+Li4uLA0gSAA+Li4uLA9QCEA8BAAAAA8QCEA9CABAAA8wCEA9CABAAA8ACEA8RAAAAA8gCEA8RAAAAA8ACEA8yAAAAA8gC" +
            "EA8yAAAAA9ACEA8BAAAAA9ACEA8BAAAAAxACEA8AAACAA0wSAA+LqouqA1ASAA+LqgAAAygCEA//BRIIAxgSAA8AAAEABWAIAIAA" +
            "AAAABv////8ABAAABwQAAABgCACABEwIAIDe/QAABJwIAIAHxxyHBbQIAIAAAAAABv/w//8ADwAABwQAAAC0CACABEADAIDAwPDw" +
            "BEQDAIAAAMAABFwDAIAAAAcEA8ACEA8yAQAAA8gCEA8yAQAABGwDAIABCCMABGwDAIABCCMBCQAAAAAAAAAACQAAAAAAAAAAAwQC" +
            "EA8QkEQRA0gQAAC+uv7KAhASAA8AAAAABv////8AAQAABwMAAAAQEgAPAkgAAAAAAAAACL66/sotAAAAAwQCEA8AgEQRAhASAA8A" +
            "AAAABv/+//8AAAAABwMAAAAQEgAPCQAAAAAFAQAABIQAAID///8PAwACEA8DAQcDAzQSAAjvvq3eAjQSAAgAAAAACO++rd4JAAAA" +
            "CQAAAAASAAAABIQAAID///8HAwACEA8DAAcDBDwQAIAAAAAABDwYAIAAAAAAEQDAAAAQAAAAEQTAAAAgAAAAEQjAAAATAAAAEQbA" +
            "AAAPAAAAEQLAAAAKAAAAEgDAAAAAAAAACBAAAADu////EQDAAAAQAAAAEQjAAAASAAAAEQbAAADwAAAAEQLAAAAKAAAAEgDAAAAA" +
            "AAAACBAAAADu////EQDAAAAQAAAABCDwAIAA/fD9BBAAAYAAAAD9AwAAAAC48A8AAwQAAAD//+AA7gYIAAAAAAAAA8ACEA8yAQAA" +
            "A8gCEA8yAQAABGwDAIABCCMABGwDAIABCCMBCQAAAAAW////AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        // 1.4: the whole start-up table (0x80-0xFF0) is replaced with the one from the XBlast build that
        // passed on a 1.4 transplant board with 8 OEM Samsung chips (force 1.6 path, Focus init, OEM calibration).
        // RamTests v2 (the build with the extended RAM test) has extra RAM setup in its 2BL at 0x12E4.
        // On a 1.4 board its write of 0x1210 = 0x110 (after the table runs) frags, so it is turned off.
        public static bool IsRamTestsV2(byte[] d)
        {
            return d != null && d.Length == Patcher.ImageSize && Patcher.Same(d, 0x12E4, Patch.Hex("b8040210fdc70010904411b8101200fdc70010010000"));
        }

        public static List<Patch> Build(int board, int ramIndex) { return Build(board, ramIndex, null); }

        public static List<Patch> Build(int board, int ramIndex, byte[] image)
        {
            if (board == 2)
            {
                List<Patch> t = new List<Patch>();
                t.Add(new Patch(0x80, Convert.FromBase64String(StockTable64), Convert.FromBase64String(Board14Table64),
                    "Start-up table 0x0080-0x0FEF replaced (1.4 transplant: force 1.6 path, Focus init, OEM chip RAM setup, mode register 0x132)"));
                if (IsRamTestsV2(image))
                    t.Add(new Patch(0x12EF, "b8101200fdc70010010000", "9090909090909090909090", "RamTests v2: 2BL write of 0x1210 = 0x110 turned off"));
                return t;
            }
            if (board != 1) return null;
            List<Patch> p = new List<Patch>();
            p.Add(new Patch(0x57E, "031012000f10000000", "031012000f00000000", "0x1210 = 00000000"));
            p.Add(new Patch(0x5A2, "031412000f48084848", "031412000f68686868", "0x1214 = 68686868"));
            p.Add(new Patch(0x5AB, "032c12000f88888888", "032c12000faaaaaaaa", "0x122C = AAAAAAAA"));
            p.Add(new Patch(0x5C6, "031412000f09090909", "031412000f68686868", "0x1214 = 68686868"));
            p.Add(new Patch(0x5E1, "031412000f09090909", "031412000f68686868", "0x1214 = 68686868"));
            p.Add(new Patch(0x671, "03c002100f32020000", "03c002100f32010000", "RAM mode register 0x132 (stock chips)"));
            p.Add(new Patch(0x67A, "03c802100f32020000", "03c802100f32010000", "RAM mode register 0x132 (stock chips)"));
            p.Add(new Patch(0xD28, "03c002100f32020000", "03c002100f32010000", "RAM mode register 0x132 (stock chips)"));
            p.Add(new Patch(0xD31, "03c802100f32020000", "03c802100f32010000", "RAM mode register 0x132 (stock chips)"));
            return p;
        }

        // True when the file is a stock XBlastOS 0.65 RAMTESTER image (every byte the 1.2/1.3 patch changes is as expected).
        public static bool IsStockRamTester(byte[] d)
        {
            if (d == null || d.Length != Patcher.ImageSize) return false;
            foreach (Patch p in Build(1, 0)) if (!Patcher.Same(d, p.Offset, p.Old)) return false;
            return true;
        }

        public static string Status(int board, int ramIndex)
        {
            if (board == 2)
            {
                if (ramIndex == 1) return "Tested: 1.4 board, 8 OEM Samsung chips (128 MB): boots from the stock RAMTESTER and from RamTests v2 (use RamTests v2 for the extended RAM test).";
                return "Same table as the tested 128 MB build; not yet tested with 64 MB.";
            }
            if (board != 1) return "No verified XBlast build for this board yet.";
            if (ramIndex == 1) return "Tested: 1.2/1.3 board, 8 OEM Samsung chips (128 MB): boots, and the extended RAM test stays clean.";
            return "Same settings as the tested 128 MB build; not yet tested with 64 MB.";
        }
    }

    class MainForm : Form
    {
        TextBox txtFile = new TextBox();
        Button btnBrowse = new Button();
        Label lblDetected = new Label();
        ComboBox cboBuild = new ComboBox();
        ComboBox cboBoard = new ComboBox();
        ComboBox cboRam = new ComboBox();
        TextBox txtInfo = new TextBox();
        Button btnPatch = new Button();
        byte[] image;
        int detected;
        TextBox txtXbFile = new TextBox();
        Button btnXbBrowse = new Button();
        Label lblXb = new Label();
        Button btnXbPatch = new Button();
        byte[] xbImage;

        static readonly RamConfig[] Rams = new RamConfig[] {
            new RamConfig("64 MB stock (OEM chips, 4 positions)", false, 0x88888888, 0x64646464, 0xDDDD0000, false,
                "Tested: v1.4 Focus board, 0 FRAGs in 10 cold boots, 0 errors in memory, GPU and hard drive stress tests."),
            new RamConfig("128 MB OEM Samsung chips (all 8 positions)", false, 0x68686868, 0xAAAAAAAA, 0xDDDD0000, false,
                "Uses the donor 1.6 calibration values verified on the transplant boards (1214 = 68686868, 122C = AAAAAAAA, 10B8 = DDDD0000).")
        };

        public MainForm()
        {
            Text = "XGPU-B Transplant Patcher (Cerbios + XBlast)";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 640);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            int y = 14;
            AddLabel("1. Stock Cerbios 3.1.0 Hybrid Beta image (.bin)", 14, y); y += 22;
            txtFile.SetBounds(14, y, 440, 24); txtFile.ReadOnly = true; Controls.Add(txtFile);
            btnBrowse.SetBounds(462, y - 1, 84, 26); btnBrowse.Text = "Browse..."; btnBrowse.Click += OnBrowse; Controls.Add(btnBrowse);
            y += 30;
            lblDetected.SetBounds(14, y, 530, 20); lblDetected.Text = "No image loaded."; Controls.Add(lblDetected);
            y += 30;

            AddLabel("2. Cerbios build", 14, y); y += 22;
            cboBuild.SetBounds(14, y, 532, 24); cboBuild.DropDownStyle = ComboBoxStyle.DropDownList;
            cboBuild.Items.Add("Standard (Cerbios_Hybrid_V3.1.0_Beta.bin)");
            cboBuild.SelectedIndex = 0; cboBuild.SelectedIndexChanged += OnChanged; Controls.Add(cboBuild);
            y += 34;

            AddLabel("3. Board revision", 14, y); y += 22;
            cboBoard.SetBounds(14, y, 532, 24); cboBoard.DropDownStyle = ComboBoxStyle.DropDownList;
            cboBoard.Items.Add("1.0 / 1.1 (Conexant encoder)");
            cboBoard.Items.Add("1.2 / 1.3");
            cboBoard.Items.Add("1.4 (Focus encoder)");
            cboBoard.SelectedIndex = 2; cboBoard.SelectedIndexChanged += OnChanged; Controls.Add(cboBoard);
            y += 34;

            AddLabel("4. RAM configuration", 14, y); y += 22;
            cboRam.SetBounds(14, y, 532, 24); cboRam.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (RamConfig r in Rams) cboRam.Items.Add(r);
            cboRam.SelectedIndex = 0; cboRam.SelectedIndexChanged += OnChanged; Controls.Add(cboRam);
            y += 36;

            txtInfo.SetBounds(14, y, 532, 150); txtInfo.Multiline = true; txtInfo.ReadOnly = true;
            txtInfo.ScrollBars = ScrollBars.Vertical; txtInfo.BackColor = SystemColors.Window; Controls.Add(txtInfo);
            y += 160;

            btnPatch.SetBounds(386, y, 160, 32); btnPatch.Text = "Patch Cerbios and save..."; btnPatch.Click += OnPatch; Controls.Add(btnPatch);
            y += 46;

            AddLabel("5. Optional: XBlast RAM tester for the same board and RAM", 14, y); y += 22;
            Label xbHint = new Label(); xbHint.SetBounds(14, y, 532, 20);
            xbHint.Text = "Choose Prehistoricman's XBlastOS 0.65 RAMTESTER image (.bin).";
            Controls.Add(xbHint); y += 22;
            txtXbFile.SetBounds(14, y, 440, 24); txtXbFile.ReadOnly = true; Controls.Add(txtXbFile);
            btnXbBrowse.SetBounds(462, y - 1, 84, 26); btnXbBrowse.Text = "Browse..."; btnXbBrowse.Click += OnXbBrowse; Controls.Add(btnXbBrowse);
            y += 30;
            lblXb.SetBounds(14, y, 532, 36); Controls.Add(lblXb);
            y += 40;
            btnXbPatch.SetBounds(386, y, 160, 32); btnXbPatch.Text = "Patch XBlast and save..."; btnXbPatch.Click += OnXbPatch; Controls.Add(btnXbPatch);

            UpdateState();
        }

        void AddLabel(string text, int x, int y)
        {
            Label l = new Label(); l.Text = text; l.SetBounds(x, y, 532, 20);
            l.Font = new Font(Font, FontStyle.Bold); Controls.Add(l);
        }

        void OnBrowse(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "BIOS image (*.bin)|*.bin|All files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    image = File.ReadAllBytes(dlg.FileName);
                    txtFile.Text = dlg.FileName;
                    detected = Patcher.DetectBuild(image);
                    if (detected == 1) { lblDetected.Text = "Detected: standard Cerbios 3.1.0 build."; cboBuild.SelectedIndex = 0; }
                    else if (detected == 2) { lblDetected.Text = "This is the 256MB Cerbios build. Use the standard build (Cerbios_Hybrid_V3.1.0_Beta.bin)."; cboBuild.SelectedIndex = 0; }
                    else lblDetected.Text = "Not recognised as a stock 256 KB Cerbios 3.1.0 Hybrid Beta image.";
                }
                catch (Exception ex)
                {
                    image = null; detected = 0;
                    MessageBox.Show(this, "Could not read the file:\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            UpdateState();
        }

        void OnChanged(object sender, EventArgs e) { UpdateState(); }

        string Problem()
        {
            if (image == null) return "Choose a stock Cerbios image first.";
            if (detected == 0) return "The selected file is not a stock Cerbios 3.1.0 Hybrid Beta image.";
            if (detected == 2) return "This is the 256MB Cerbios build. This tool needs the standard build (Cerbios_Hybrid_V3.1.0_Beta.bin).";
            return null;
        }

        void UpdateState()
        {
            RamConfig ram = (RamConfig)cboRam.SelectedItem;
            StringBuilder sb = new StringBuilder();
            string problem = Problem();
            if (problem != null) sb.AppendLine("Can't patch yet: " + problem).AppendLine();
            sb.AppendLine("Will apply:");
            foreach (Patch p in Patcher.Build(ram, cboBoard.SelectedIndex, cboRam.SelectedIndex)) sb.AppendLine(string.Format("  0x{0:X4}  {1}", p.Offset, p.Why));
            sb.AppendLine().AppendLine(ram.Status);
            if (cboBoard.SelectedIndex == 0)
                sb.AppendLine().AppendLine("1.0/1.1 build: GPU revision detection, fixed calibration and 1.0/1.1 pad values, with no encoder changes. Verified with 64 MB; 128 MB is not yet tested.");
            else if (cboBoard.SelectedIndex == 1)
                sb.AppendLine().AppendLine("1.2/1.3 build: GPU revision detection, Cerbios's Conexant encoder setup after the 1.6 path, 1.4 calibration with C3 = 0x42 and stock pads. The same image for 64 MB and 128 MB; tested with both.");
            sb.AppendLine().AppendLine("After flashing, enable 480p and 720p in the video settings: 480i can drop the picture for a few seconds at a time on transplant boards.");
            txtInfo.Text = sb.ToString();
            btnPatch.Enabled = problem == null;
            UpdateXbState();
        }

        string XbProblem()
        {
            if (XBlastPatcher.Build(cboBoard.SelectedIndex, cboRam.SelectedIndex) == null) return XBlastPatcher.Status(cboBoard.SelectedIndex, cboRam.SelectedIndex);
            if (xbImage == null) return "Choose an XBlastOS 0.65 RAMTESTER image.";
            if (!XBlastPatcher.IsStockRamTester(xbImage)) return "Not recognised as a stock XBlastOS 0.65 RAMTESTER image (or it is already patched).";
            return null;
        }

        void UpdateXbState()
        {
            string problem = XbProblem();
            lblXb.Text = problem != null ? problem : XBlastPatcher.Status(cboBoard.SelectedIndex, cboRam.SelectedIndex);
            btnXbPatch.Enabled = problem == null;
        }

        void OnXbBrowse(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "BIOS image (*.bin)|*.bin|All files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { xbImage = File.ReadAllBytes(dlg.FileName); txtXbFile.Text = dlg.FileName; }
                catch (Exception ex)
                {
                    xbImage = null;
                    MessageBox.Show(this, "Could not read the file:\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            UpdateXbState();
        }

        void OnXbPatch(object sender, EventArgs e)
        {
            string problem = XbProblem();
            if (problem != null) { MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            byte[] d = (byte[])xbImage.Clone();
            StringBuilder log = new StringBuilder();
            string err = Patcher.Apply(d, XBlastPatcher.Build(cboBoard.SelectedIndex, cboRam.SelectedIndex, xbImage), log);
            if (err != null) { MessageBox.Show(this, err, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            string[] boardTag = new string[] { "v1.0-1.1", "v1.2-1.3", "v1.4" };
            string[] ramTag = new string[] { "64MB", "128MB_OEM" };
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "BIOS image (*.bin)|*.bin";
                dlg.FileName = "XBlastOS_0.65_RAMTESTER_XGPUB_" + boardTag[cboBoard.SelectedIndex] + "_" + ramTag[cboRam.SelectedIndex] + ".bin";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllBytes(dlg.FileName, d); }
                catch (Exception ex) { MessageBox.Show(this, "Could not save:\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                txtInfo.Text = "Saved " + dlg.FileName + "\r\n\r\nApplied:\r\n" + log.ToString() + "\r\nMD5: " + Patcher.Md5(d) +
                               "\r\n\r\nFlash it to a spare bank. Press eject to start XBlast OS; its RAM tests are under Tools.";
            }
        }

        void OnPatch(object sender, EventArgs e)
        {
            string problem = Problem();
            if (problem != null) { MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            RamConfig ram = (RamConfig)cboRam.SelectedItem;
            byte[] d = (byte[])image.Clone();
            StringBuilder log = new StringBuilder();
            string err = Patcher.Apply(d, Patcher.Build(ram, cboBoard.SelectedIndex, cboRam.SelectedIndex), log);
            if (err != null) { MessageBox.Show(this, err, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            string[] boardTag = new string[] { "v1.0-1.1", "v1.2-1.3", "v1.4" };
            string[] ramTag = new string[] { "64MB", "128MB_OEM" };
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "BIOS image (*.bin)|*.bin";
                dlg.FileName = "Cerbios_3.1.0_XGPUB_" + boardTag[cboBoard.SelectedIndex] + "_" + ramTag[cboRam.SelectedIndex] + ".bin";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllBytes(dlg.FileName, d); }
                catch (Exception ex) { MessageBox.Show(this, "Could not save:\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                txtInfo.Text = "Saved " + dlg.FileName + "\r\n\r\nApplied:\r\n" + log.ToString() + "\r\nMD5: " + Patcher.Md5(d) +
                               "\r\n\r\nFlash it, then enable 480p and 720p in the video settings.";
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
