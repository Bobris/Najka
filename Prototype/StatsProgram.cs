using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NajkaLib;

namespace Prototype
{
    class StatsProgram
    {
        public class Stat
        {
            readonly Dictionary<string, int> _stat = new Dictionary<string, int>();
            readonly Dictionary<string, List<string>> _words = new Dictionary<string, List<string>>();

            public void Inc(string c, string w)
            {
                int count;
                if (_stat.TryGetValue(c, out count))
                {
                    _stat[c] = count + 1;
                    var l = _words[c];
                    if (l.Count < 3 && !l.Contains(w)) l.Add(w);
                }
                else
                {
                    _stat[c] = 1;
                    _words[c] = new List<string> { w };
                }
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var p in _stat.ToList().OrderBy(pair => pair.Key))
                {
                    sb.Append(p.Key);
                    sb.Append(':');
                    sb.Append(p.Value);
                    foreach (var word in _words[p.Key])
                    {
                        sb.Append(' ');
                        sb.Append(word);
                    }
                    sb.Append("\n\r");
                }
                return sb.ToString();
            }
        }

        public static void CalcStats(string inputNaj, string outputName)
        {
            var fsa = new FsaNajka(File.OpenRead(inputNaj));
            var stats = new Dictionary<char, Stat>();
            fsa.IterateAllRaw(sb =>
            {
                var parts = sb.ToString().Split(':');
                if (parts.Length != 3) return;
                var s = parts[2];
                Stat stat;
                if (!stats.TryGetValue(s[1], out stat))
                {
                    stat = new Stat();
                    stats[s[1]] = stat;
                }
                for (var i = 0; i < s.Length - 1; i += 2)
                {
                    stat.Inc(s.Substring(i, 2), parts[0]);
                }
            });
            using (var o=File.CreateText(outputName))
                foreach (var stat in stats.OrderBy(p=>p.Key))
                {
                    o.WriteLine("Kind "+stat.Key);
                    o.WriteLine(stat.Value);
                }
        }
    }
}