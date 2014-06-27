using System;
using System.IO;
using System.Text;

namespace NajkaLib
{
    [Flags]
    public enum CompareType
    {
        Exact = 0,
        CaseInsensitive = 1,
        IgnoreDiacritics = 2,
        IgnoreCaseAndDiacritics = 3
    }

    public struct WordLemmaTaxonomy
    {
        public WordLemmaTaxonomy(string word, string lemma, string taxonomy)
        {
            Word = word;
            Lemma = lemma;
            Taxonomy = taxonomy;
        }
        public readonly string Word;
        public readonly string Lemma;
        public readonly string Taxonomy;
    }

    public class FsaNajka
    {
        readonly byte _type;
        readonly char[] _id2Char;
        readonly char[][] _id2ConvertedChar;
        readonly byte[] _tree;
        readonly int _nodePrefix;
        readonly int _nodePostfix;

        public FsaNajka(Stream stream, bool leaveOpen = false)
        {
            using (var file = new BinaryReader(stream, Encoding.UTF8, leaveOpen))
            {
                if (new string(file.ReadChars(4)) != "@naj")
                    throw new Exception("Invalid header");
                if (file.ReadByte() != 1)
                    throw new Exception("Invalid version");
                _type = file.ReadByte();
                _id2Char = file.ReadChars(file.ReadByte());
                _tree = file.ReadBytes((int)(file.BaseStream.Length - file.BaseStream.Position));
            }
            _id2ConvertedChar = new char[4][];
            _id2ConvertedChar[0] = _id2Char;
            _id2ConvertedChar[1] = new char[_id2Char.Length];
            _id2ConvertedChar[2] = new char[_id2Char.Length];
            _id2ConvertedChar[3] = new char[_id2Char.Length];
            for (var i = 0; i < _id2Char.Length; i++)
            {
                _id2ConvertedChar[1][i] = char.ToLowerInvariant(_id2Char[i]);
                _id2ConvertedChar[2][i] = _id2Char[i].RemoveDiacritics();
                _id2ConvertedChar[3][i] = char.ToLowerInvariant(_id2ConvertedChar[2][i]);
            }
            _nodePrefix = SinkTo(0, '!');
            _nodePostfix = SinkTo(0, '^');
        }

        struct NodeHead
        {
            internal bool IsFinal;
            internal int Edges;
        }

        NodeHead DecodeNodeHead(ref int ofs)
        {
            var b = _tree[ofs];
            ofs++;
            return new NodeHead
            {
                IsFinal = (b & 128) != 0,
                Edges = b & 127
            };
        }

        int DecodeEdgeTarget(int ofs)
        {
            var b = _tree[ofs++];
            if ((b & 128) != 0) return ofs;
            b = _tree[ofs];
            if (b == 128) return -1;
            if (b > 127) return ofs + b - 127;
            var b2 = _tree[ofs + 1];
            if (b2 > 127) return ofs + 2 + ((b2 - 127) << 7) + b;
            var b3 = _tree[ofs + 2];
            if (b3 > 127) return ofs + ((b3 - 127) << 14) + (b2 << 7) + b + 131;
            throw new InvalidDataException("Probably invalid data or too long?");
        }

        void SkipEdgeTarget(ref int ofs)
        {
            while ((_tree[ofs++] & 128) == 0)
            {
            }
        }

        char DecodeChar(int ofs)
        {
            return _id2Char[_tree[ofs] & 127];
        }

        char DecodeChar(int ofs, CompareType mode)
        {
            return _id2ConvertedChar[(int)mode][_tree[ofs] & 127];
        }

        public void IterateAllRaw(Action<StringBuilder> onItem)
        {
            IterateRaw(new StringBuilder(100), 0, onItem);
        }

        public void FindNicer(string word, CompareType mode, Action<WordLemmaTaxonomy> onFound)
        {
            Find(word, mode, s => onFound(String2WordLemmaTaxonomy(s)));
        }

        WordLemmaTaxonomy String2WordLemmaTaxonomy(string text)
        {
            var split = text.Split(':');
            switch (_type)
            {
                case 129: // w-lt
                    return new WordLemmaTaxonomy(split[0], split[1], split[2]);
                case 131: // lt-w
                    return new WordLemmaTaxonomy(split[2], split[0], split[1]);
                case 132: // l-wt
                    return new WordLemmaTaxonomy(split[1], split[0], split[2]);
            }
            throw new ArgumentOutOfRangeException();
        }

        public void Find(string word, CompareType mode, Action<string> onFound)
        {
            switch (mode)
            {
                case CompareType.CaseInsensitive:
                    word = word.ToLowerInvariant();
                    break;
                case CompareType.IgnoreDiacritics:
                    word = word.RemoveDiacritics();
                    break;
                case CompareType.IgnoreCaseAndDiacritics:
                    word = word.ToLowerInvariant().RemoveDiacritics();
                    break;
            }
            var sb = new StringBuilder();
            Match(word, mode, sb, 0, ofs =>
            {
                if (word.Length != sb.Length) return;
                ofs = SinkTo(ofs, ':');
                if (ofs == -1) return;
                sb.Append(":");
                IterateRaw(sb, ofs, sb1 => onFound(ExpandByType(sb1)));
                sb.Length--;
            });
            Match(word, mode, sb, _nodePrefix, ofs =>
            {
                if (ofs != -1) ofs = SinkTo(ofs, ':');
                if (ofs == -1) return;
                Match(word, mode, sb, _nodePostfix, ofs2 =>
                {
                    if (word.Length != sb.Length) return;
                    ofs2 = SinkTo(ofs2, ':');
                    if (ofs2 == -1) return;
                    sb.Append(":");
                    IterateRaw(sb, ofs2, sb1 => onFound(ExpandByType(sb1)));
                    sb.Length--;
                });
            });
        }

        string ExpandByType(StringBuilder sb)
        {
            var firstq = 0;
            while (sb[firstq] != ':') firstq++;
            var secondq = firstq + 1;
            while (secondq < sb.Length && sb[secondq] != ':') secondq++;
            switch (_type)
            {
                case 129: // w-lt
                    {
                        var prefix = sb[firstq + 1] - 'A';
                        var removesuffix = sb[firstq + 2] - 'A';
                        return sb.ToString(0, firstq + 1) + sb.ToString(prefix, firstq - prefix - removesuffix) +
                               sb.ToString(firstq + 3, sb.Length - firstq - 3);

                    }
                case 131: // lt-w
                    {
                        var prefix = sb[secondq + 1] - 'A';
                        var removesuffix = sb[secondq + 2 + prefix] - 'A';
                        return sb.ToString(0, secondq + 1)
                               + sb.ToString(secondq + 2, prefix)
                               + sb.ToString(0, firstq - removesuffix)
                               + sb.ToString(secondq + 3 + prefix, sb.Length - (secondq + 3 + prefix));
                    }
                case 132: // l-wt
                    {
                        var prefix = sb[firstq + 1] - 'A';
                        var removesuffix = sb[firstq + 2 + prefix] - 'A';
                        return sb.ToString(0, firstq + 1)
                               + sb.ToString(firstq + 2, prefix)
                               + sb.ToString(0, firstq - removesuffix)
                               + sb.ToString(firstq + 3 + prefix, sb.Length - (firstq + 3 + prefix));
                    }
            }
            throw new ArgumentOutOfRangeException();
        }

        void Match(string word, CompareType mode, StringBuilder sb, int ofs, Action<int> onFound)
        {
            if (word.Length == sb.Length)
            {
                onFound(ofs);
                return;
            }
            if (ofs == -1)
            {
                onFound(ofs);
                return;
            }
            var backupOfs = ofs;
            var head = DecodeNodeHead(ref ofs);
            if (head.IsFinal)
            {
                onFound(backupOfs);
            }
            var someFound = false;
            for (var i = 0; i < head.Edges; i++)
            {
                if (word[sb.Length] == DecodeChar(ofs, mode))
                {
                    someFound = true;
                    sb.Append(DecodeChar(ofs));
                    Match(word, mode, sb, DecodeEdgeTarget(ofs), onFound);
                    sb.Length--;
                }
                SkipEdgeTarget(ref ofs);
            }
            if (someFound)
                return;
            onFound(backupOfs);
        }

        int SinkTo(int ofs, char ch)
        {
            var head = DecodeNodeHead(ref ofs);
            for (var i = 0; i < head.Edges; i++)
            {
                if (ch == DecodeChar(ofs))
                {
                    return DecodeEdgeTarget(ofs);
                }
                SkipEdgeTarget(ref ofs);
            }
            return -1;
        }

        void IterateRaw(StringBuilder sb, int ofs, Action<StringBuilder> onItem)
        {
            if (ofs == -1)
            {
                onItem(sb);
                return;
            }
            var head = DecodeNodeHead(ref ofs);
            if (head.IsFinal)
            {
                onItem(sb);
            }
            for (var i = 0; i < head.Edges; i++)
            {
                sb.Append(DecodeChar(ofs));
                IterateRaw(sb, DecodeEdgeTarget(ofs), onItem);
                sb.Length--;
                SkipEdgeTarget(ref ofs);
            }
        }
    }
}
