# Unpack.ps1 - Clause.bat が自動生成した解凍スクリプト。Open.bat から実行される。
# 同じフォルダの data.mpk.NNN を読み、このフォルダの中身をバイト単位で元通りに復元する。
# 各ファイルは書き込む前に SHA-256 を照合する。
# Windows 標準の PowerShell 5.1 + .NET Framework (Add-Type) だけで動作する。
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolRx = '^(Clause\.bat|Open\.bat|Unpack\.ps1|data\.mpk\.\d{3})$'

$parts = @(Get-ChildItem -LiteralPath $here -File | Where-Object { $_.Name -match '^data\.mpk\.\d{3}$' } | Sort-Object Name | ForEach-Object { $_.FullName })
if ($parts.Count -eq 0) { throw "圧縮データ (data.mpk.000 ...) が見つかりません: $here" }

$existing = @(Get-ChildItem -LiteralPath $here -Force | Where-Object { $_.Name -notmatch $toolRx })
if ($existing.Count -gt 0 -and -not $Force) {
    $ans = Read-Host "$here には既にファイルがあります。同名ファイルを上書きして復元しますか? (y/N)"
    if ($ans -ne 'y' -and $ans -ne 'Y') { Write-Host '中止しました。'; exit 1 }
}

$cs = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mpk
{
    // LZ77 + adaptive binary range coding (LZMA-style model, lc=3 lp=0 pb=2).
    public sealed class Model
    {
        public readonly ushort[] IsMatch = new ushort[12 << 4];
        public readonly ushort[] IsRep = new ushort[12];
        public readonly ushort[] IsRepG0 = new ushort[12];
        public readonly ushort[] IsRepG1 = new ushort[12];
        public readonly ushort[] IsRepG2 = new ushort[12];
        public readonly ushort[] IsRep0Long = new ushort[12 << 4];
        public readonly ushort[] PosSlot = new ushort[4 << 6];
        public readonly ushort[] PosSpec = new ushort[114];
        public readonly ushort[] Align = new ushort[16];
        public readonly ushort[] Lit = new ushort[0x300 << 3];
        // length model: [0]=choice [1]=choice2 [2+ps*8]=low [34+ps*8]=mid [66]=high
        public readonly ushort[] Len = new ushort[322];
        public readonly ushort[] RepLen = new ushort[322];

        public Model()
        {
            ushort[][] all = { IsMatch, IsRep, IsRepG0, IsRepG1, IsRepG2, IsRep0Long, PosSlot, PosSpec, Align, Lit, Len, RepLen };
            foreach (ushort[] a in all) for (int i = 0; i < a.Length; i++) a[i] = 1024;
        }
    }

    public sealed class RangeDecoder
    {
        readonly byte[] buf; int pos; uint range, code;
        public RangeDecoder(byte[] b, int start)
        {
            buf = b; pos = start; range = 0xFFFFFFFFu; code = 0;
            for (int i = 0; i < 5; i++) code = (code << 8) | Next();
        }
        byte Next() { if (pos < buf.Length) return buf[pos++]; pos++; return 0; }
        public bool Overrun { get { return pos > buf.Length + 5; } }
        public int Bit(ushort[] p, int i)
        {
            uint v = p[i]; uint bound = (range >> 11) * v; int bit;
            if (code < bound) { range = bound; p[i] = (ushort)(v + ((2048 - v) >> 5)); bit = 0; }
            else { code -= bound; range -= bound; p[i] = (ushort)(v - (v >> 5)); bit = 1; }
            if (range < (1u << 24)) { range <<= 8; code = (code << 8) | Next(); }
            return bit;
        }
        public uint Direct(int n)
        {
            uint res = 0;
            for (int i = 0; i < n; i++)
            {
                range >>= 1; uint t = (code - range) >> 31; code -= range & (t - 1); res = (res << 1) | (1 - t);
                if (range < (1u << 24)) { range <<= 8; code = (code << 8) | Next(); }
            }
            return res;
        }
        public int Tree(ushort[] p, int off, int n)
        {
            int m = 1; for (int i = 0; i < n; i++) m = (m << 1) | Bit(p, off + m);
            return m - (1 << n);
        }
        public int RevTree(ushort[] p, int off, int n)
        {
            int m = 1, s = 0;
            for (int i = 0; i < n; i++) { int b = Bit(p, off + m); m = (m << 1) | b; s |= b << i; }
            return s;
        }
    }

    public interface IByteSink { void Write(byte[] b, int off, int cnt); }

    public static class LzDecoder
    {
        static int DecLen(RangeDecoder rc, ushort[] p, int ps)
        {
            if (rc.Bit(p, 0) == 0) return rc.Tree(p, 2 + ps * 8, 3);
            if (rc.Bit(p, 1) == 0) return 8 + rc.Tree(p, 34 + ps * 8, 3);
            return 16 + rc.Tree(p, 66, 8);
        }

        public static void Decode(byte[] src, int start, long outLen, int dictBits, IByteSink sink)
        {
            RangeDecoder rc = new RangeDecoder(src, start);
            Model m = new Model();
            int R = 1 << dictBits;
            if (outLen < R) R = (int)Math.Max(outLen, 16);
            byte[] win = new byte[R];
            int wpos = 0, flushed = 0; long total = 0;
            int state = 0; uint r0 = 0, r1 = 0, r2 = 0, r3 = 0; int prev = 0;
            while (total < outLen)
            {
                int ps = (int)total & 3;
                if (rc.Bit(m.IsMatch, (state << 4) + ps) == 0)
                {
                    int b = 0x300 * (prev >> 5); int sym = 1;
                    if (state < 7) { while (sym < 0x100) sym = (sym << 1) | rc.Bit(m.Lit, b + sym); }
                    else
                    {
                        int mi = wpos - (int)r0 - 1; if (mi < 0) mi += R;
                        int mb = win[mi];
                        while (sym < 0x100)
                        {
                            int mbit = (mb >> 7) & 1; mb <<= 1;
                            int bit = rc.Bit(m.Lit, b + ((1 + mbit) << 8) + sym);
                            sym = (sym << 1) | bit;
                            if (mbit != bit) { while (sym < 0x100) sym = (sym << 1) | rc.Bit(m.Lit, b + sym); break; }
                        }
                    }
                    prev = sym & 0xFF; win[wpos++] = (byte)prev; total++;
                    if (wpos == R) { sink.Write(win, flushed, wpos - flushed); wpos = 0; flushed = 0; }
                    state = state < 4 ? 0 : (state < 10 ? state - 3 : state - 6);
                    continue;
                }
                int len;
                if (rc.Bit(m.IsRep, state) != 0)
                {
                    if (total == 0) throw new InvalidDataException("corrupt stream (rep at 0)");
                    if (rc.Bit(m.IsRepG0, state) == 0)
                    {
                        if (rc.Bit(m.IsRep0Long, (state << 4) + ps) == 0)
                        {
                            state = state < 7 ? 9 : 11;
                            int si = wpos - (int)r0 - 1; if (si < 0) si += R;
                            prev = win[si]; win[wpos++] = (byte)prev; total++;
                            if (wpos == R) { sink.Write(win, flushed, wpos - flushed); wpos = 0; flushed = 0; }
                            continue;
                        }
                    }
                    else
                    {
                        uint d;
                        if (rc.Bit(m.IsRepG1, state) == 0) d = r1;
                        else
                        {
                            if (rc.Bit(m.IsRepG2, state) == 0) d = r2; else { d = r3; r3 = r2; }
                            r2 = r1;
                        }
                        r1 = r0; r0 = d;
                    }
                    len = DecLen(rc, m.RepLen, ps);
                    state = state < 7 ? 8 : 11;
                }
                else
                {
                    r3 = r2; r2 = r1; r1 = r0;
                    len = DecLen(rc, m.Len, ps);
                    state = state < 7 ? 7 : 10;
                    int ls = len < 3 ? len : 3;
                    int slot = rc.Tree(m.PosSlot, ls << 6, 6);
                    if (slot >= 4)
                    {
                        int nd = (slot >> 1) - 1;
                        uint d = (uint)((2 | (slot & 1)) << nd);
                        if (slot < 14) d += (uint)rc.RevTree(m.PosSpec, (int)d - slot - 1, nd);
                        else { d += rc.Direct(nd - 4) << 4; d += (uint)rc.RevTree(m.Align, 0, 4); }
                        r0 = d;
                    }
                    else r0 = (uint)slot;
                }
                len += 2;
                if ((long)r0 >= total || r0 >= (uint)R) throw new InvalidDataException("corrupt stream (distance)");
                if (len > outLen - total) throw new InvalidDataException("corrupt stream (length)");
                int s = wpos - (int)r0 - 1; if (s < 0) s += R;
                total += len;
                while (len-- > 0)
                {
                    byte c = win[s++]; if (s == R) s = 0;
                    win[wpos++] = c;
                    if (wpos == R) { sink.Write(win, flushed, wpos - flushed); wpos = 0; flushed = 0; }
                }
                int pi = wpos - 1; if (pi < 0) pi += R; prev = win[pi];
                if (rc.Overrun) throw new InvalidDataException("corrupt stream (overrun)");
            }
            if (wpos > flushed) sink.Write(win, flushed, wpos - flushed);
        }
    }

    // x86 BCJ filter: E8/E9 rel32 <-> absolute (only values within +-16MB).
    public static class X86
    {
        public static void Run(byte[] d, int off, int len, bool encode)
        {
            int i = 0;
            while (i + 4 < len)
            {
                int o = off + i;
                if ((d[o] & 0xFE) == 0xE8)
                {
                    int v = d[o + 1] | (d[o + 2] << 8) | (d[o + 3] << 16) | (d[o + 4] << 24);
                    int top = v >> 24;
                    if (top == 0 || top == -1)
                    {
                        int a = encode ? v + (i + 5) : v - (i + 5);
                        a &= 0x1FFFFFF;
                        if ((a & 0x1000000) != 0) a |= unchecked((int)0xFE000000);
                        d[o + 1] = (byte)a; d[o + 2] = (byte)(a >> 8); d[o + 3] = (byte)(a >> 16); d[o + 4] = (byte)(a >> 24);
                    }
                    // Always skip the 4 operand bytes (converted or not), so encoder and
                    // decoder visit exactly the same opcode positions -> bijective.
                    i += 5; continue;
                }
                i++;
            }
        }
    }

    public sealed class Manifest
    {
        public int DictBits; public long StreamLen;
        public List<string> Dirs = new List<string>();
        public List<long> BlobLen = new List<long>();
        public List<byte> BlobFilter = new List<byte>();
        public List<byte[]> BlobHash = new List<byte[]>();
        public List<string> FilePath = new List<string>();
        public List<int> FileBlob = new List<int>();
        public List<long> FileTime = new List<long>();

        public byte[] Serialize()
        {
            MemoryStream raw = new MemoryStream();
            BinaryWriter w = new BinaryWriter(raw, new UTF8Encoding(false));
            w.Write(1); w.Write(DictBits); w.Write(StreamLen);
            w.Write(Dirs.Count); foreach (string s in Dirs) w.Write(s);
            w.Write(BlobLen.Count);
            for (int i = 0; i < BlobLen.Count; i++) { w.Write(BlobLen[i]); w.Write(BlobFilter[i]); w.Write(BlobHash[i]); }
            w.Write(FilePath.Count);
            for (int i = 0; i < FilePath.Count; i++) { w.Write(FilePath[i]); w.Write(FileBlob[i]); w.Write(FileTime[i]); }
            w.Flush();
            MemoryStream c = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(c, CompressionMode.Compress, true)) { byte[] r = raw.ToArray(); ds.Write(r, 0, r.Length); }
            return c.ToArray();
        }

        public static Manifest Parse(byte[] a, int off, int len)
        {
            MemoryStream raw = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(new MemoryStream(a, off, len), CompressionMode.Decompress)) ds.CopyTo(raw);
            raw.Position = 0;
            BinaryReader r = new BinaryReader(raw, new UTF8Encoding(false));
            Manifest m = new Manifest();
            if (r.ReadInt32() != 1) throw new InvalidDataException("unsupported version");
            m.DictBits = r.ReadInt32(); m.StreamLen = r.ReadInt64();
            int n = r.ReadInt32(); for (int i = 0; i < n; i++) m.Dirs.Add(r.ReadString());
            n = r.ReadInt32();
            for (int i = 0; i < n; i++) { m.BlobLen.Add(r.ReadInt64()); m.BlobFilter.Add(r.ReadByte()); m.BlobHash.Add(r.ReadBytes(32)); }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++) { m.FilePath.Add(r.ReadString()); m.FileBlob.Add(r.ReadInt32()); m.FileTime.Add(r.ReadInt64()); }
            return m;
        }
    }

    public delegate void BlobDone(int index, byte[] data);

    public sealed class BlobSink : IByteSink
    {
        readonly Manifest m; readonly BlobDone done; int idx; byte[] cur; int fill;
        public BlobSink(Manifest m, BlobDone done) { this.m = m; this.done = done; Start(); }
        void Start()
        {
            while (idx < m.BlobLen.Count && m.BlobLen[idx] == 0) { done(idx, new byte[0]); idx++; }
            if (idx < m.BlobLen.Count) { cur = new byte[m.BlobLen[idx]]; fill = 0; } else cur = null;
        }
        public bool Complete { get { return idx >= m.BlobLen.Count; } }
        public void Write(byte[] b, int off, int cnt)
        {
            while (cnt > 0)
            {
                if (cur == null) throw new InvalidDataException("extra data in stream");
                int k = Math.Min(cnt, cur.Length - fill);
                Buffer.BlockCopy(b, off, cur, fill, k);
                fill += k; off += k; cnt -= k;
                if (fill == cur.Length) { byte[] d = cur; cur = null; done(idx, d); idx++; Start(); }
            }
        }
    }

    public static class Unpacker
    {
        public static readonly byte[] Magic = { (byte)'M', (byte)'P', (byte)'K', (byte)'1' };

        public static string Unpack(string[] parts, string dest)
        {
            MemoryStream ms = new MemoryStream();
            foreach (string p in parts) { byte[] b = File.ReadAllBytes(p); ms.Write(b, 0, b.Length); }
            return Run(ms.ToArray(), dest);
        }

        static string SafePath(string root, string rel)
        {
            if (rel.Length == 0 || rel.StartsWith("/") || rel.IndexOf(':') >= 0 || rel.IndexOf('\\') >= 0) throw new InvalidDataException("bad path: " + rel);
            foreach (string seg in rel.Split('/')) if (seg == ".." || seg == "." || seg.Length == 0) throw new InvalidDataException("bad path: " + rel);
            return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        }

        // dest == null: verify only (decode + hash check, nothing written)
        public static string Run(byte[] a, string dest)
        {
            if (a.Length < 8 || a[0] != Magic[0] || a[1] != Magic[1] || a[2] != Magic[2] || a[3] != Magic[3]) throw new InvalidDataException("not an MPK archive");
            int mlen = BitConverter.ToInt32(a, 4);
            Manifest m = Manifest.Parse(a, 8, mlen);
            int start = 8 + mlen;
            List<int>[] filesOf = new List<int>[m.BlobLen.Count];
            for (int i = 0; i < filesOf.Length; i++) filesOf[i] = new List<int>();
            for (int i = 0; i < m.FilePath.Count; i++) filesOf[m.FileBlob[i]].Add(i);
            if (dest != null)
            {
                Directory.CreateDirectory(dest);
                foreach (string d in m.Dirs) Directory.CreateDirectory(SafePath(dest, d));
            }
            long written = 0;
            using (SHA256 sha = SHA256.Create())
            {
                BlobSink sink = new BlobSink(m, delegate(int bi, byte[] data)
                {
                    if (m.BlobFilter[bi] == 1) X86.Run(data, 0, data.Length, false);
                    byte[] h = sha.ComputeHash(data);
                    for (int k = 0; k < 32; k++) if (h[k] != m.BlobHash[bi][k]) throw new InvalidDataException("hash mismatch: " + (filesOf[bi].Count > 0 ? m.FilePath[filesOf[bi][0]] : bi.ToString()));
                    if (dest == null) return;
                    foreach (int fi in filesOf[bi])
                    {
                        string path = SafePath(dest, m.FilePath[fi]);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllBytes(path, data);
                        File.SetLastWriteTimeUtc(path, new DateTime(m.FileTime[fi], DateTimeKind.Utc));
                        written += data.Length;
                    }
                });
                LzDecoder.Decode(a, start, m.StreamLen, m.DictBits, sink);
                if (!sink.Complete) throw new InvalidDataException("stream ended early");
            }
            return string.Format("{0} files, {1} dirs, {2:N0} bytes OK (SHA-256 verified)", m.FilePath.Count, m.Dirs.Count, dest == null ? m.StreamLen : written);
        }
    }
}
'@
$cp = New-Object System.CodeDom.Compiler.CompilerParameters
$cp.CompilerOptions = '/optimize+'
$cp.GenerateInMemory = $true
[void]$cp.ReferencedAssemblies.Add('System.dll')
Add-Type -TypeDefinition $cs -Language CSharp -CompilerParameters $cp

Write-Host "解凍中: $($parts.Count) 個の圧縮データ -> $here"
$sw = [Diagnostics.Stopwatch]::StartNew()
$summary = [Mpk.Unpacker]::Unpack([string[]]$parts, $here)
Write-Host ("{0} ({1:0.0} 秒)" -f $summary, $sw.Elapsed.TotalSeconds)
Write-Host "復元完了: $here"
exit 0