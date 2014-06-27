using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;

namespace NajkaLib
{
    public static class TaxonomyExplainer
    {
        public static string[] CzechExplain(string taxonomy)
        {
            if (taxonomy == null) return new[] { "null" };
            if (taxonomy.Length < 2 || taxonomy[0] != 'k' || (taxonomy.Length % 2) != 0) return new[] { "invalid", taxonomy };
            var res = new string[taxonomy.Length / 2];
            switch (taxonomy[1])
            {
                case '0':
                    res[0] = "Citoslovce";
                    break;
                case '1':
                    res[0] = "Podstatn� jm�no";
                    for (var i = 2; i < taxonomy.Length; i += 2)
                    {
                        switch (taxonomy[i])
                        {
                            case 'c':
                                res[i / 2] = CzechExplainCase(taxonomy[i + 1]);
                                break;
                            case 'g':
                                res[i / 2] = CzechExplainRod(taxonomy[i + 1]);
                                break;
                            case 'n':
                                res[i / 2] = CzechExplainCislo(taxonomy[i + 1]);
                                break;
                            case 'x':
                                res[i / 2] = taxonomy[i + 1] == 'P' ? "Speci�ln� vzor p�l" : CzechExplainBad(taxonomy, i);
                                break;
                            default:
                                res[i / 2] = CzechExplainBad(taxonomy, i);
                                break;
                        }
                    }
                    break;
                case '2':
                    res[0] = "P��davn� jm�no";
                    for (var i = 2; i < taxonomy.Length; i += 2)
                    {
                        switch (taxonomy[i])
                        {
                            case 'c':
                                res[i / 2] = CzechExplainCase(taxonomy[i + 1]);
                                break;
                            case 'g':
                                res[i / 2] = CzechExplainRod(taxonomy[i + 1]);
                                break;
                            case 'n':
                                res[i / 2] = CzechExplainCislo(taxonomy[i + 1]);
                                break;
                            case 'd':
                                res[i / 2] = CzechExplainStupen(taxonomy[i + 1]);
                                break;
                            case 'e':
                                res[i/2] = CzechExplainNegace(taxonomy[i + 1]);
                                break;
                            default:
                                res[i / 2] = CzechExplainBad(taxonomy, i);
                                break;
                        }
                    }
                    break;
                case '3':
                    res[0] = "Z�jmeno";
                    for (var i = 2; i < taxonomy.Length; i += 2)
                    {
                        switch (taxonomy[i])
                        {
                            case 'c':
                                res[i / 2] = CzechExplainCase(taxonomy[i + 1]);
                                break;
                            case 'g':
                                res[i / 2] = CzechExplainRod(taxonomy[i + 1]);
                                break;
                            case 'n':
                                res[i / 2] = CzechExplainCislo(taxonomy[i + 1]);
                                break;
                            case 'p':
                                res[i / 2] = CzechExplainOsoba(taxonomy[i + 1]);
                                break;
                            case 'x':
                                res[i / 2] = CzechExplainZajmenoX(taxonomy[i + 1]);
                                break;
                            case 'y':
                                res[i / 2] = CzechExplainZajmenoY(taxonomy[i + 1]);
                                break;
                            default:
                                res[i / 2] = CzechExplainBad(taxonomy, i);
                                break;
                        }
                    }
                    break;
                case '4':
                    res[0] = "��slovka";
                    break;
                case '5':
                    res[0] = "Sloveso";
                    break;
                case '6':
                    res[0] = "P��slovce";
                    break;
                case '7':
                    res[0] = "P�edlo�ka";
                    for (var i = 2; i < taxonomy.Length; i += 2)
                    {
                        switch (taxonomy[i])
                        {
                            case 'c':
                                res[i / 2] = CzechExplainCase(taxonomy[i + 1]);
                                break;
                            default:
                                res[i / 2] = CzechExplainBad(taxonomy, i);
                                break;
                        }
                    }
                    break;
                case '8':
                    res[0] = "Spojka";
                    break;
                case '9':
                    res[0] = "��stice";
                    break;
                case 'A':
                    res[0] = "Zkratka";
                    break;
                case 'Y':
                    res[0] = "By, aby, kdyby";
                    break;
                default:
                    for (var i = 0; i < taxonomy.Length; i += 2)
                    {
                        res[i / 2] = CzechExplainBad(taxonomy, i);
                    }
                    break;
            }
            return res;
        }

        static string CzechExplainZajmenoY(char c)
        {
            switch (c)
            {
                case 'F':
                    return "Reflexivn�";
                case 'Q':
                    return "T�zac�";
                case 'R':
                    return "Vzta�n�";
                case 'N':
                    return "Z�porn�";
                case 'I':
                    return "Neur�it�";
                default:
                    return "Unknown z�jmeno y" + c;
            }
        }

        static string CzechExplainZajmenoX(char c)
        {
            switch (c)
            {
                case 'P':
                    return "Osobn�";
                case 'O':
                    return "P�ivlast�ovac�";
                case 'D':
                    return "Ukazovac�";
                case 'T':
                    return "Vymezovac�";
                default:
                    return "Unknown z�jmeno x" + c;
            }
        }

        static string CzechExplainOsoba(char c)
        {
            switch (c)
            {
                case '1':
                    return "Prvn� osoba";
                case '2':
                    return "Druh� osoba";
                case '3':
                    return "T�et� osoba";
                default:
                    return "Unknown osoba " + c;
            }
        }

        static string CzechExplainNegace(char c)
        {
            switch (c)
            {
                case 'N':
                    return "Negace";
                case 'A':
                    return "Afirmace";
                default:
                    return "Unknown negace " + c;
            }
        }

        static string CzechExplainStupen(char c)
        {
            switch (c)
            {
                case '1':
                    return "1. stupe� pozitiv";
                case '2':
                    return "2. stupe� komparativ";
                case '3':
                    return "3. stupe� superlativ";
                default:
                    return "Unknown stupe� " + c;
            }
        }

        static string CzechExplainCislo(char c)
        {
            switch (c)
            {
                case 'P':
                    return "Mno�n� ��slo";
                case 'S':
                    return "Jednotn� ��slo";
                default:
                    return "Unknown ��slo " + c;
            }
        }

        static string CzechExplainRod(char c)
        {
            switch (c)
            {
                case 'F':
                    return "�ensk� rod";
                case 'I':
                    return "Mu�sk� rod ne�ivotn�";
                case 'M':
                    return "Mu�sk� rod �ivotn�";
                case 'N':
                    return "St�edn� rod";
                case 'R':
                    return "Rodina (p��jmen�)";
                default:
                    return "Unknown rod " + c;
            }
        }

        static string CzechExplainBad(string taxonomy, int pos)
        {
            return "Unknown " + taxonomy.Substring(pos, 2);
        }

        static string CzechExplainCase(char c)
        {
            if (c < '1' || c > '7') return "Unknown case " + c;
            return c + ". p�d";
        }
    }
}