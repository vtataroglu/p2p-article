using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;

namespace ScalableP2P
{
    class Program
    {
        const string CsvHeader = "step,maxId,removed,holderDegree,s4,v4,s6,v6,s8,v8,s12,v12,holderTrend,bgSucc,bgHits,bgVis,rewireNodes,rewireJoins,edges,holderTrending,entK,entC,entL,holderEntryRule";

        // 2016 calismasindaki baslangic ag icerigi
        static void seedItems(Graph graf)
        {
            graf.addItemToNode(0, 1); graf.addItemToNode(0, 2);
            graf.addItemToNode(0, 3); graf.addItemToNode(1, 1);
            graf.addItemToNode(1, 2); graf.addItemToNode(1, 3);
            graf.addItemToNode(1, 4); graf.addItemToNode(2, 1);
            graf.addItemToNode(2, 2); graf.addItemToNode(3, 1);
            graf.addItemToNode(3, 2); graf.addItemToNode(4, 1);
            graf.addItemToNode(4, 2); graf.addItemToNode(4, 3);
            graf.addItemToNode(4, 4); graf.addItemToNode(4, 5);
            graf.addItemToNode(4, 6); graf.addItemToNode(4, 7);
            graf.addItemToNode(7, 1); graf.addItemToNode(9, 8);
        }

        // tek konfigurasyonu tum parametreleriyle kosar; TBP senaryosu
        // (giris adimi, rampa baslangici/tepesi) ag boyutuyla oranli olceklenir.
        // crash: >0 ise tutucu o adimda koparilir. shape: 0 dogrusal, 1 basamak,
        // 2 ustel. multi: >1 ise bu kadar es zamanli TBP icerigi kademeli girer.
        // randrw: >0 ise pencere basina bu kadar rastgele dugume ayni join
        // butcesi verilir. oracle: tutucu TStart'tan itibaren trend modunda.
        // fixedthr: >0 ise oz-kalibrasyon yerine sabit mutlak esik.
        // rules: giris kurali bit maskesi (1=k-sigma, 2=CUSUM, 4=seviye; 7=tumu).
        // cooper: >0 ise trend katmani yerine Cooper (2005) square-root-construct.
        // noscale: 1 ise TBP zamanlamasi ag boyutuyla olceklenmez (10K degerleri).
        static void runOne(string outdir, string name, int seed, double pp, double pd, double pt,
                           bool rewire, double sigmaK, int persist, double shareFloor, double qmax, int nTarget,
                           double cusumTheta, int crash, int shape, int multi, int randrw, bool oracle, double fixedthr,
                           int rules, bool cooper, double cooperDmax, double cooperMu, bool noScale, int plateau,
                           int reactive)
        {
            Directory.CreateDirectory(outdir);
            Console.WriteLine("\n================ " + name + "  seed=" + seed +
                "  (Pp=" + pp + " Pd=" + pd + " Pt=" + pt + " rewire=" + rewire +
                " k=" + sigmaK + " c=" + persist + " eps=" + shareFloor + " qmax=" + qmax + " N=" + nTarget +
                " theta=" + cusumTheta + " crash=" + crash + " shape=" + shape + " multi=" + multi +
                " randrw=" + randrw + " oracle=" + oracle + " fixedthr=" + fixedthr +
                " rules=" + rules + " cooper=" + cooper + " dmax=" + cooperDmax + " mu=" + cooperMu +
                " noscale=" + noScale + " plateau=" + plateau + " reactive=" + reactive + ") ================");
            Node.resetSeeds(seed);
            Graph graf = new Graph(3, seed);
            graf.Pp = pp;
            graf.Pd = pd;
            graf.Pt = pt;
            graf.RewireEnabled = rewire;
            graf.SigmaK = sigmaK;
            graf.PersistWindows = persist;
            graf.ShareFloor = shareFloor;
            graf.CusumTheta = cusumTheta;
            graf.HolderCrashStep = crash;
            graf.RandomRewireNodes = randrw;
            graf.OracleDetector = oracle;
            graf.FixedThreshold = fixedthr;
            graf.Rules = rules;
            graf.CooperMode = cooper;
            graf.CooperDmax = cooperDmax;
            graf.CooperMu = cooperMu;
            graf.ReactiveLinking = reactive;
            TrendModel tm = new TrendModel(777 + seed);
            tm.Shape = shape;
            int items = Math.Max(1, multi);
            tm.TbpVals = new int[items];
            tm.TStarts = new int[items];
            tm.TPeaks = new int[items];
            tm.QMaxes = new double[items];
            graf.TbpEntrySteps = new int[items];
            // noscale: buyuk aglarda talep suresinin oranli uzamasini kapatan
            // kontrol — boyut etkisini sure etkisinden ayirir (10K zamanlamasi)
            long sb = noScale ? 10000L : nTarget;
            for (int k = 0; k < items; k++)
            {
                tm.TbpVals[k] = 1000 + k;
                if (plateau > 0 && shape == 3)
                {
                    // plato darbeleri: buyume (~t=14,300) bittikten sonra sabit
                    // boyutlu olgun agda ardisik yuksel-son dongusu; zamanlar
                    // mutlak simTime, tutucu her rampadan 1000 adim once katilir
                    tm.TStarts[k] = 16000 + 3000 * k;
                    tm.TPeaks[k] = tm.TStarts[k] + 1500;
                    graf.TbpEntrySteps[k] = tm.TStarts[k] - 1000;
                }
                else if (items > 1 && shape == 3)
                {
                    // ardisik sonumlu darbeler: ortusmeyen yuksel-son dongulerinin
                    // uzun ufukta bayat hub birikimi yaratip yaratmadigini test eder
                    tm.TStarts[k] = (int)((2200L + 2400L * k) * sb / 10000);
                    tm.TPeaks[k] = tm.TStarts[k] + (int)(1500L * sb / 10000);
                    graf.TbpEntrySteps[k] = (int)((1120L + 1680L * k) * sb / 10000);
                }
                else
                {
                    // kademeli girisler ve rampalar; tek icerikte eski degerlerle ozdes
                    tm.TStarts[k] = (int)((items > 1 ? 3000L + 600L * k : 2500L) * sb / 10000);
                    tm.TPeaks[k] = tm.TStarts[k] + (int)(4500L * sb / 10000);
                    graf.TbpEntrySteps[k] = (int)((2000L + (items > 1 ? 400L * k : 0)) * sb / 10000);
                }
                tm.QMaxes[k] = qmax;
            }
            graf.Trend = tm;
            graf.initialGraph();
            seedItems(graf);
            string stem = Path.Combine(outdir, name + "-s" + seed);
            using (StreamWriter writer = new StreamWriter(stem + ".csv"))
            {
                StringBuilder header = new StringBuilder(CsvHeader);
                for (int k = 1; k < items; k++)
                    header.Append(",deg").Append(k).Append(",s8i").Append(k).Append(",trending").Append(k);
                writer.WriteLine(header.ToString());
                graf.MetricsWriter = writer;
                graf.grow(nTarget);
                if (plateau > 0) graf.plateauRun(plateau);
                graf.MetricsWriter = null;
            }
            graf.writeDegreeHistogram(stem + "-degrees.csv");
        }

        static double D(string s)
        {
            return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        static void Main(string[] args)
        {
            // tam parametrik kosu:
            // one <outdir> <name> <seed> <pp> <pd> <pt> <rewire 0/1> [k] [c] [eps]
            //     [qmax] [n] [theta] [crash] [shape] [multi] [randrw] [oracle] [fixedthr]
            //     [rules] [cooper 0/1] [cooperDmax] [cooperMu] [noscale 0/1]
            if (args.Length >= 8 && args[0] == "one")
            {
                runOne(args[1], args[2],
                       int.Parse(args[3]), D(args[4]), D(args[5]), D(args[6]),
                       args[7] == "1",
                       args.Length > 8 ? D(args[8]) : 5.0,
                       args.Length > 9 ? int.Parse(args[9]) : 2,
                       args.Length > 10 ? D(args[10]) : 0.02,
                       args.Length > 11 ? D(args[11]) : 0.2,
                       args.Length > 12 ? int.Parse(args[12]) : 10000,
                       args.Length > 13 ? D(args[13]) : 6.0,
                       args.Length > 14 ? int.Parse(args[14]) : 0,
                       args.Length > 15 ? int.Parse(args[15]) : 0,
                       args.Length > 16 ? int.Parse(args[16]) : 1,
                       args.Length > 17 ? int.Parse(args[17]) : 0,
                       args.Length > 18 && args[18] == "1",
                       args.Length > 19 ? D(args[19]) : 0,
                       args.Length > 20 ? int.Parse(args[20]) : 7,
                       args.Length > 21 && args[21] == "1",
                       args.Length > 22 ? D(args[22]) : 160,
                       args.Length > 23 ? D(args[23]) : 1.0,
                       args.Length > 24 && args[24] == "1",
                       args.Length > 25 ? int.Parse(args[25]) : 0,
                       args.Length > 26 ? int.Parse(args[26]) : 0);
                return;
            }

            // varsayilan: uc ana politika, tek tohum
            int seed = args.Length > 0 ? int.Parse(args[0]) : 42;
            string resultsDir = Path.GetFullPath("results");
            Console.WriteLine("Results dir: " + resultsDir + "  seed=" + seed);

            // ayni sorgu is yuku altinda uc topoloji buyutme politikasi:
            // degree     : derece-tabanli tercihli baglanma
            // popularity : populerlik-tabanli (2016 taban cizgisi)
            // trend      : populerlik + trend terimi + proaktif yeniden kablolama (onerilen)
            runOne(resultsDir, "degree", seed, 0, 100, 0, false, 5.0, 2, 0.02, 0.2, 10000, 6.0, 0, 0, 1, 0, false, 0, 7, false, 160, 1.0, false, 0, 0);
            runOne(resultsDir, "popularity", seed, 100, 0, 0, false, 5.0, 2, 0.02, 0.2, 10000, 6.0, 0, 0, 1, 0, false, 0, 7, false, 160, 1.0, false, 0, 0);
            runOne(resultsDir, "trend", seed, 100, 0, 25000, true, 5.0, 2, 0.02, 0.2, 10000, 6.0, 0, 0, 1, 0, false, 0, 7, false, 160, 1.0, false, 0, 0);

            Console.WriteLine("\nAll configs completed.");
        }
    }
}
