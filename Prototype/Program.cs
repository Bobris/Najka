using System;
using System.Collections.Generic;
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
            var fsa = new FsaNajka(File.OpenRead("../../../lt-w.naj"));
            fsa.Find("desetiintelektualsky", CompareType.IgnoreCaseAndDiacritics, Console.WriteLine);
            Console.ReadKey();
        }
    }
}

