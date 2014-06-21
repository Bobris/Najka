using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NajkaLib;

namespace Prototype
{
    class Program
    {
        static void Main(string[] args)
        {
            var fsa = new FsaNajka(File.OpenRead("../../../l-wt.naj"));
            fsa.FindNicer("minut", CompareType.IgnoreCaseAndDiacritics, wlt => Console.WriteLine("{0} {1} {2}", wlt.Lemma,wlt.Taxonomy, wlt.Word));
            Console.ReadKey();
        }
    }
}

