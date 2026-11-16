// XGPU-B Transplant Cerbios Patcher
// Patches a stock Cerbios 3.1.0 Hybrid Beta image (standard or 256MB build) so a
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
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

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

        // board: 0 = 1.0/1.1, 1 = 1.2/1.3, 2 = 1.4.  ramIndex: 0 = 64 MB stock, 1 = 128 MB OEM, 2 = 128 MB modules
        public static List<Patch> Build(RamConfig ram, int board, int ramIndex)
        {
            List<Patch> p = new List<Patch>();
            if (board == 1 && ramIndex != 2)
            {
                // 1.2/1.3 (Conexant) with OEM RAM: verified build "X3". GPU check (1.6 path only for an
                // XGPU-B), calibration and C3 = 0x42 as on 1.4, stock pads, and Cerbios's own Conexant
                // encoder setup after the 1.6 path (this board frags without it: register C4).
                p.Add(new Patch(0x158, "081000000009000000", "060800000000000000", "GPU revision check (replaces SMBus board check)"));
                p.Add(new Patch(0x161, "090000000024000000", "0800000000e5ffffff", "GPU revision check"));
                p.Add(new Patch(0x16A, "060800000000000000", "1100c00000ff000000", "GPU revision check"));
                p.Add(new Patch(0x173, "0800000000d3ffffff", "020000000f00000000", "Read GPU revision (PMC_BOOT_0)"));
                p.Add(new Patch(0x17C, "1100c00000ff000000", "06ff00000000000000", "Mask GPU revision byte"));
                p.Add(new Patch(0x185, "0900000000c9060000", "08d3000000c9060000", "Take the 1.6 path only for an XGPU-B (rev D3)"));
                p.Add(new Patch(0x3BC, "07030000004812000f", MemWrite(0x0F001248, 0x42), "Memory-type byte (EEPROM C3) = 0x42"));
                p.Add(new Patch(0x57E, "060000ffff00000000", AndOr(ram.R10B8), "0x10B8 = " + ram.R10B8.ToString("X8")));
                p.Add(new Patch(0x5BD, "024812000f00000000", AndOr(ram.R122C), "0x122C = " + ram.R122C.ToString("X8")));
                p.Add(new Patch(0x5E1, "024812000f00000000", AndOr(ram.R1214), "0x1214 = " + ram.R1214.ToString("X8")));
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

        static readonly RamConfig[] Rams = new RamConfig[] {
            new RamConfig("64 MB stock (OEM chips, 4 positions)", false, 0x88888888, 0x64646464, 0xDDDD0000, false,
                "Tested: v1.4 Focus board, 0 FRAGs in 10 cold boots, 0 errors in memory, GPU and hard drive stress tests."),
            new RamConfig("128 MB OEM Samsung chips (all 8 positions)", false, 0x68686868, 0xAAAAAAAA, 0xDDDD0000, false,
                "Uses the donor 1.6 calibration values verified on the transplant boards (1214 = 68686868, 122C = AAAAAAAA, 10B8 = DDDD0000)."),
            new RamConfig("128 MB Prehistoricman modules (4 positions)", true, 0x68686868, 0xAAAAAAAA, 0xDDDD0000, true,
                "Tested: boots to the dashboard and runs stable with the donor calibration values and 0x6C = 0x23.")
        };

        public MainForm()
        {
            Text = "XGPU-B Transplant Cerbios Patcher";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 470);
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
            cboBuild.Items.Add("256MB (Cerbios_Hybrid_V3.1.0_Beta_FOR_256MB_CONSOLES_ONLY.bin)");
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

            btnPatch.SetBounds(386, y, 160, 32); btnPatch.Text = "Patch and save..."; btnPatch.Click += OnPatch; Controls.Add(btnPatch);

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
                    else if (detected == 2) { lblDetected.Text = "Detected: 256MB Cerbios 3.1.0 build."; cboBuild.SelectedIndex = 1; }
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
            RamConfig ram = (RamConfig)cboRam.SelectedItem;
            bool want256 = cboBuild.SelectedIndex == 1;
            if (image == null) return "Choose a stock Cerbios image first.";
            if (detected == 0) return "The selected file is not a stock Cerbios 3.1.0 Hybrid Beta image.";
            if ((detected == 2) != want256) return "The selected build doesn't match the file you loaded.";
            if (ram.Needs256Build && !want256) return "This RAM configuration needs the 256MB Cerbios build.";
            if (!ram.Needs256Build && want256) return "This RAM configuration needs the standard Cerbios build.";
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
            if (cboBoard.SelectedIndex == 0 && cboRam.SelectedIndex != 2)
                sb.AppendLine().AppendLine("1.0/1.1 build: GPU revision detection, fixed calibration and 1.0/1.1 pad values, with no encoder changes. Verified with 64 MB; 128 MB is not yet tested.");
            else if (cboBoard.SelectedIndex == 1 && cboRam.SelectedIndex != 2)
                sb.AppendLine().AppendLine("1.2/1.3 build: GPU revision detection, Cerbios's Conexant encoder setup after the 1.6 path, 1.4 calibration with C3 = 0x42 and stock pads. Verified with 64 MB; 128 MB is not yet tested.");
            else if (cboBoard.SelectedIndex != 2)
                sb.AppendLine().AppendLine("Note: for Prehistoricman modules on this board the patch is the same as for 1.4 (the Focus init only runs if a Focus encoder is found). Full testing so far was on v1.4 boards.");
            sb.AppendLine().AppendLine("After flashing, enable 480p and 720p in the video settings: 480i can drop the picture for a few seconds at a time on transplant boards.");
            txtInfo.Text = sb.ToString();
            btnPatch.Enabled = problem == null;
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
            string[] ramTag = new string[] { "64MB", "128MB_OEM", "128MB_modules" };
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
