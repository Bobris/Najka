using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Najka
{
    public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    {
        public static readonly ReferenceEqualityComparer<T> Instance;

        static ReferenceEqualityComparer()
        {
            Instance = new ReferenceEqualityComparer<T>();
        }

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    internal struct FsaLink
    {
        internal FsaNode Child;
        internal char Ch;
        internal bool Final;
        internal int Len;
    }

    [Flags]
    internal enum FsaNodeType
    {
        Final = 1,
        Nonfinal = 2,
        Both = 3
    }

    internal class FsaNode
    {
        internal FsaNodeType Type;
        internal int Offset;
        internal FsaLink[] Links;

        internal FsaNode SplitFinalNonfinal(Dictionary<FsaNode, FsaNode> remap, bool final)
        {
            Offset = -1;
            for (var i = 0; i < Links.Length; i++)
            {
                if (Links[i].Child == null) continue;
                Links[i].Child = Links[i].Child.SplitFinalNonfinal(remap, Links[i].Final);
            }
            if (final)
            {
                FsaNode res;
                if (remap.TryGetValue(this, out res)) return res;
            }
            if ((Type & FsaNodeType.Both) != FsaNodeType.Both)
                return this;
            Type -= FsaNodeType.Final;
            var finalnode = new FsaNode
            {
                Type = FsaNodeType.Final,
                Offset = -1,
                Links = Links.ToArray()
            };
            remap[this] = finalnode;
            return final ? finalnode : this;
        }

        internal void GatherNodes(HashSet<FsaNode> nodes)
        {
            if (nodes.Contains(this)) return;
            nodes.Add(this);
            for (var i = 0; i < Links.Length; i++)
            {
                if (Links[i].Child == null) continue;
                Links[i].Child.GatherNodes(nodes);
            }
        }

        internal void CalcCharacterHistogram(Dictionary<char, int> histogram, HashSet<FsaNode> visitedNodes)
        {
            if (visitedNodes.Contains(this)) return;
            visitedNodes.Add(this);
            for (var i = 0; i < Links.Length; i++)
            {
                int c;
                histogram.TryGetValue(Links[i].Ch, out c);
                histogram[Links[i].Ch] = c + 1;
                if (Links[i].Child == null) continue;
                Links[i].Child.CalcCharacterHistogram(histogram, visitedNodes);
            }
        }

        internal void CalcOffset(ref int lastOffset)
        {
            if (Offset >= 0) return;
            for (var i = 0; i < Links.Length; i++)
            {
                if (Links[i].Child == null) continue;
                Links[i].Child.CalcOffset(ref lastOffset);
            }
            for (var i = Links.Length - 1; i >= 0; i--)
            {
                if (Links[i].Child == null)
                {
                    Links[i].Len = 1;
                    lastOffset += 2;
                    continue;
                }
                if (Links[i].Child.Offset == lastOffset)
                {
                    Links[i].Len = 0;
                    lastOffset += 1;
                    continue;
                }
                var len = VaribleByteLen(lastOffset + 1 - Links[i].Child.Offset);
                Links[i].Len = len;
                lastOffset += len + 1;
            }
            lastOffset++;
            Offset = lastOffset;
        }

        static int VaribleByteLen(int value)
        {
            var res = 0;
            while (value > 0)
            {
                value--;
                value >>= 7;
                res++;
            }
            return res;
        }

        public int SerializeNode(byte[] content, Dictionary<char, int> charRemap)
        {
            var ofs = content.Length - Offset;
            content[ofs++] = (byte)((((Type & FsaNodeType.Final) != 0) ? 128 : 0) + Links.Length);
            for (var i = 0; i < Links.Length; i++)
            {
                content[ofs] = (byte)charRemap[Links[i].Ch];
                if (Links[i].Child == null)
                {
                    content[ofs + 1] = 128;
                    ofs += 2;
                    continue;
                }
                var chofs = content.Length - Links[i].Child.Offset;
                if (ofs + 1 == chofs)
                {
                    content[ofs++] += 128;
                    continue;
                }
                chofs -= ofs + Links[i].Len;
                ofs++;
                while (chofs > 0)
                {
                    chofs--;
                    content[ofs++] = (byte)(chofs & 127);
                    chofs >>= 7;
                }
                content[ofs - 1] |= 128;
            }
            return ofs - content.Length + Offset;
        }

        public int SerializeNodeCheck(byte[] content, Dictionary<char, int> charRemap)
        {
            var ofs = content.Length - Offset;
            content[ofs++]++;
            for (var i = 0; i < Links.Length; i++)
            {
                content[ofs]++;
                if (Links[i].Child == null)
                {
                    content[ofs + 1]++;
                    Debug.Assert(Links[i].Len == 1);
                    ofs += 2;
                    continue;
                }
                var chofs = content.Length - Links[i].Child.Offset;
                if (ofs + 1 == chofs)
                {
                    Debug.Assert(Links[i].Len == 0);
                    ofs++;
                    continue;
                }
                chofs -= ofs + Links[i].Len;
                ofs++;
                var len = 0;
                while (chofs > 0)
                {
                    chofs--;
                    content[ofs++]++;
                    chofs >>= 7;
                    len++;
                }
                Debug.Assert(len == Links[i].Len);
            }
            return ofs - content.Length + Offset;
        }
    }

    class MajkaFsa
    {
        const int GotoOffset = 1;
        readonly int _gotoLength;
        public readonly byte Type;
        readonly byte[] _dict;

        static readonly char[] Iso88592 = new char[256];

        static MajkaFsa()
        {
            var table1 = new byte[256];
            var table2 = new byte[256];
            for (var i = 0; i < 256; i++) table1[i] = table2[i] = 0;
            // Iso8859-2 to UTF-8
            table1[161] = 196; table2[161] = 132; table1[177] = 196; table2[177] = 133; // Ąą
            table1[163] = 197; table2[163] = 129; table1[179] = 197; table2[179] = 130; // Łł
            table1[165] = 196; table2[165] = 189; table1[181] = 196; table2[181] = 190; // Ľľ
            table1[166] = 197; table2[166] = 154; table1[182] = 197; table2[182] = 155; // Śś
            table1[169] = 197; table2[169] = 160; table1[185] = 197; table2[185] = 161; // Šš
            table1[170] = 197; table2[170] = 158; table1[186] = 197; table2[186] = 159; // Şş
            table1[171] = 197; table2[171] = 164; table1[187] = 197; table2[187] = 165; // Ťť
            table1[172] = 197; table2[172] = 185; table1[188] = 197; table2[188] = 186; // Źź
            table1[174] = 197; table2[174] = 189; table1[190] = 197; table2[190] = 190; // Žž
            table1[175] = 197; table2[175] = 187; table1[191] = 197; table2[191] = 188; // Żż
            table1[192] = 197; table2[192] = 148; table1[224] = 197; table2[224] = 149; // Ŕŕ
            table1[193] = 195; table2[193] = 129; table1[225] = 195; table2[225] = 161; // Áá
            table1[194] = 195; table2[194] = 130; table1[226] = 195; table2[226] = 162; // Ââ
            table1[195] = 196; table2[195] = 130; table1[227] = 196; table2[227] = 131; // Ăă
            table1[196] = 195; table2[196] = 132; table1[228] = 195; table2[228] = 164; // Ää
            table1[197] = 196; table2[197] = 185; table1[229] = 196; table2[229] = 186; // Ĺĺ
            table1[198] = 196; table2[198] = 134; table1[230] = 196; table2[230] = 135; // Ćć
            table1[199] = 195; table2[199] = 135; table1[231] = 195; table2[231] = 167; // Çç
            table1[200] = 196; table2[200] = 140; table1[232] = 196; table2[232] = 141; // Čč
            table1[201] = 195; table2[201] = 137; table1[233] = 195; table2[233] = 169; // Éé
            table1[202] = 196; table2[202] = 152; table1[234] = 196; table2[234] = 153; // Ęę
            table1[203] = 195; table2[203] = 139; table1[235] = 195; table2[235] = 171; // Ëë
            table1[204] = 196; table2[204] = 154; table1[236] = 196; table2[236] = 155; // Ěě
            table1[205] = 195; table2[205] = 141; table1[237] = 195; table2[237] = 173; // Íí
            table1[206] = 195; table2[206] = 142; table1[238] = 195; table2[238] = 174; // Îî
            table1[207] = 196; table2[207] = 142; table1[239] = 196; table2[239] = 143; // Ďď
            table1[208] = 195; table2[208] = 144; table1[240] = 195; table2[240] = 176; // Đđ
            table1[209] = 197; table2[209] = 131; table1[241] = 197; table2[241] = 132; // Ńń
            table1[210] = 197; table2[210] = 135; table1[242] = 197; table2[242] = 136; // Ňň
            table1[211] = 195; table2[211] = 147; table1[243] = 195; table2[243] = 179; // Óó
            table1[212] = 195; table2[212] = 148; table1[244] = 195; table2[244] = 180; // Ôô
            table1[213] = 197; table2[213] = 144; table1[245] = 197; table2[245] = 145; // Őő
            table1[214] = 195; table2[214] = 150; table1[246] = 195; table2[246] = 182; // Öö
            table1[216] = 197; table2[216] = 152; table1[248] = 197; table2[248] = 153; // Řř
            table1[217] = 197; table2[217] = 174; table1[249] = 197; table2[249] = 175; // Ůů
            table1[218] = 195; table2[218] = 154; table1[250] = 195; table2[250] = 186; // Úú
            table1[219] = 197; table2[219] = 176; table1[251] = 197; table2[251] = 177; // Űű
            table1[220] = 195; table2[220] = 156; table1[252] = 195; table2[252] = 188; // Üü
            table1[221] = 195; table2[221] = 157; table1[253] = 195; table2[253] = 189; // Ýý
            table1[222] = 197; table2[222] = 162; table1[254] = 197; table2[254] = 163; // Ţţ
            for (var i = 0; i < 256; i++)
            {
                Iso88592[i] = (char)32;
            }
            for (var i = 32; i < 128; i++)
            {
                Iso88592[i] = (char)i;
            }
            for (var i = 0; i < 256; i++)
            {
                if (table1[i] == 0) continue;
                Iso88592[i] = (char)(((table1[i] - 0xc0) << 6) + table2[i] - 0x80);
            }
        }

        public MajkaFsa(string fileName)
        {
            var header = new byte[20];
            using (var stream = File.OpenRead(fileName))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                {
                    throw new InvalidDataException("Failed to read 20 bytes of FSA header");
                }
                if (header[0] != '\\' || header[1] != 'f' || header[2] != 's' || header[3] != 'a')
                {
                    throw new InvalidDataException("Invalid header should start with '\\fsa'");
                }
                if (header[9] != 1)
                {
                    throw new InvalidDataException("Invalid major version of majka");
                }
                _gotoLength = header[7] & 0xf;
                if (_gotoLength < 1 || _gotoLength > 4)
                {
                    throw new InvalidDataException("Goto length is outside of range <1,4>");
                }
                Type = header[8];
                _dict = new byte[stream.Length - stream.Position];
                if (stream.Read(_dict, 0, _dict.Length) != _dict.Length)
                {
                    throw new InvalidDataException("Failed to read content of FSA");
                }
                var code = 27021979u;
                for (var pos = FirstNode(); pos + 4 <= _dict.Length; pos += 4, code++)
                {
                    var v = UnpackUInt32Le(_dict, pos);
                    PackUInt32Le(_dict, pos, v ^ code);
                }
            }
        }

        static void PackUInt32Le(byte[] data, int offset, uint value)
        {
            data[offset] = unchecked((byte)value);
            data[offset + 1] = unchecked((byte)(value >> 8));
            data[offset + 2] = unchecked((byte)(value >> 16));
            data[offset + 3] = unchecked((byte)(value >> 24));
        }

        static uint UnpackUInt32Le(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        public int FirstNode()
        {
            return GotoOffset + _gotoLength;
        }

        public int DiveIntoNode(int currentNode)
        {
            if ((_dict[currentNode + GotoOffset] & 4) != 0)
                return currentNode + GotoOffset + 1;
            switch (_gotoLength)
            {
                case 1:
                    return _dict[currentNode + GotoOffset] >> 3;
                case 2:
                    return (_dict[currentNode + GotoOffset] + _dict[currentNode + GotoOffset + 1] * 256) >> 3;
                case 3:
                    return (_dict[currentNode + GotoOffset] + _dict[currentNode + GotoOffset + 1] * 256 + _dict[currentNode + GotoOffset + 2] * 256 * 256) >> 3;
                case 4:
                    return (_dict[currentNode + GotoOffset] + _dict[currentNode + GotoOffset + 1] * 256 + _dict[currentNode + GotoOffset + 2] * 256 * 256 + _dict[currentNode + GotoOffset + 3] * 256 * 256 * 256) >> 3;
                default:
                    throw new InvalidOperationException();
            }
        }

        public int NextNode(int currentNode)
        {
            if ((_dict[currentNode + GotoOffset] & 2) != 0) return 0; // Was last node
            return currentNode + GotoOffset + _gotoLength;
        }

        public char GetLetter(int currentNode)
        {
            return Iso88592[_dict[currentNode]];
        }

        public bool IsFinal(int currentNode)
        {
            return (_dict[currentNode + GotoOffset] & 1) != 0;
        }
    }

    static class Program
    {
        static void Main(string[] args)
        {
            const string maptype = "l-wt";
            const string mainFsa = "../../../majka." + maptype;
            var origSize = new FileInfo(mainFsa).Length;
            var fsa = new MajkaFsa(mainFsa);
            if (true)
            {
                var mapBuildFsaNodes = new Dictionary<int, FsaNode>();
                var root = BuildFsaTree(fsa, mapBuildFsaNodes, fsa.DiveIntoNode(fsa.FirstNode()), false);
                Console.WriteLine(mapBuildFsaNodes.Count);
                var nodes1 = new HashSet<FsaNode>(ReferenceEqualityComparer<FsaNode>.Instance);
                root.GatherNodes(nodes1);
                root.SplitFinalNonfinal(new Dictionary<FsaNode, FsaNode>(ReferenceEqualityComparer<FsaNode>.Instance), false);
                var nodes2 = new HashSet<FsaNode>(ReferenceEqualityComparer<FsaNode>.Instance);
                var charhist = new Dictionary<char, int>();
                root.CalcCharacterHistogram(charhist, nodes2);
                var count = 0;
                foreach (var ch in charhist.Keys.ToArray())
                {
                    charhist[ch] = count++;
                }
                Console.WriteLine("Nodes before split {0} after {1}", nodes1.Count, nodes2.Count);
                var betterlen = 0;
                root.CalcOffset(ref betterlen);
                Console.WriteLine("My len {0}", betterlen);
                var content = new byte[betterlen];
                var checklen = 0;
                foreach (var fsaNode in nodes2)
                {
                    checklen += fsaNode.SerializeNodeCheck(content, charhist);
                }
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] != 1) Console.WriteLine("Error {0} {1}", i, content[i]);
                }
                Debug.Assert(checklen == betterlen);
                checklen = 0;
                foreach (var fsaNode in nodes2)
                {
                    checklen += fsaNode.SerializeNode(content, charhist);
                }
                Debug.Assert(checklen == betterlen);
                var d = charhist.ToDictionary(p => p.Value, p => p.Key);
                long finalSize = 0;
                using (var file = new BinaryWriter(File.OpenWrite("../../../" + maptype + ".naj"), Encoding.UTF8))
                {
                    file.Write("@naj".ToCharArray());
                    file.Write((byte)1);
                    file.Write(fsa.Type);
                    file.Write((byte)d.Count);
                    for (var i = 0; i < d.Count; i++)
                    {
                        file.Write(d[i]);
                    }
                    file.Write(content);
                    file.Flush();
                    finalSize = file.BaseStream.Position;
                    file.BaseStream.SetLength(file.BaseStream.Position);
                }
                Console.WriteLine("Original size {0} Final size {1} ratio {2:F1}%", origSize, finalSize, 100.0 * finalSize / origSize);
            }
            //using (var file = new BinaryWriter(File.OpenWrite(maptype + ".org"), Encoding.UTF8))
            //{
            //   var sb = new StringBuilder();
            //    Print(file, fsa, sb, fsa.FirstNode());
            //}
        }

        static FsaNode BuildFsaTree(MajkaFsa fsa, Dictionary<int, FsaNode> mapBuildFsaNodes, int node, bool final)
        {
            if (node == 0) return null;
            FsaNode res;
            if (mapBuildFsaNodes.TryGetValue(node, out res))
            {
                res.Type |= final ? FsaNodeType.Final : FsaNodeType.Nonfinal;
                return res;
            }

            res = new FsaNode();
            mapBuildFsaNodes[node] = res;
            res.Type = final ? FsaNodeType.Final : FsaNodeType.Nonfinal;
            res.Offset = -1;
            var len = 0;
            for (var cn = node; cn != 0; cn = fsa.NextNode(cn))
            {
                len++;
            }
            res.Links = new FsaLink[len];
            len = 0;
            for (var cn = node; cn != 0; cn = fsa.NextNode(cn))
            {
                res.Links[len] = new FsaLink
                {
                    Final = fsa.IsFinal(cn),
                    Ch = fsa.GetLetter(cn),
                    Child = BuildFsaTree(fsa, mapBuildFsaNodes, fsa.DiveIntoNode(cn), fsa.IsFinal(cn))
                };
                len++;
            }
            return res;
        }

        static void Print(BinaryWriter file, MajkaFsa fsa, StringBuilder sb, int firstNode)
        {
            firstNode = fsa.DiveIntoNode(firstNode);
            for (var cn = firstNode; cn != 0; cn = fsa.NextNode(cn))
            {
                sb.Append(fsa.GetLetter(cn));
                if (fsa.IsFinal(cn))
                {
                    file.Write(sb.ToString().ToCharArray());
                    file.Write(new[] { '\n', '\r' });
                }
                Print(file, fsa, sb, cn);
                sb.Length--;
            }
        }
    }
}
