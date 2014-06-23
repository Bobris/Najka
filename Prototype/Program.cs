using System;
using System.IO;
using NajkaLib;

namespace Prototype
{
    class Program
    {
        static void Main(string[] args)
        {
            var fsa = new FsaNajka(File.OpenRead("../../../w-lt.naj"));
            fsa.FindNicer("minut", CompareType.IgnoreCaseAndDiacritics, wlt =>
            {
                Console.WriteLine("{0} {1} {2}", wlt.Lemma, wlt.Taxonomy, wlt.Word);
                Console.WriteLine("     {0}", string.Join(", ", TaxonomyExplainer.CzechExplain(wlt.Taxonomy)));
            });
            Console.ReadKey();
        }
    }
}

