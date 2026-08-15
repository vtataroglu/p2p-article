using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScalableP2P
{
    // Zamana bagli sorgu is yuku: TBP (to-be-popular) icerik icin talep,
    // TStart'ta baslayip TPeak'e kadar dogrusal olarak QMax'a yukselir.
    // Diger sorgular, 2016 modelindeki gibi item populerligiyle artan
    // siklikta gelir: Zipf'ten cekilen sira r (1 = en sik) populerlik
    // degerine v = 101 - r ile eslenir; boylece en populer item (v=100)
    // en sik sorgulanan item olur.
    class TrendModel
    {
        // coklu TBP destegi: k. icerik TbpVals[k] degerini tasir ve kendi
        // rampasina sahiptir; tek icerikli varsayilan eski davranisa esdeger
        public int[] TbpVals = new int[] { 1000 };
        public int[] TStarts = new int[] { 2500 };
        public int[] TPeaks = new int[] { 7000 };
        public double[] QMaxes = new double[] { 0.2 };
        // talep sekli: 0 dogrusal rampa, 1 basamak (flash crowd), 2 ustel,
        // 3 darbe (yuksel-son: TPeak'e dogrusal cikis, ayni surede sifira inis)
        public int Shape = 0;

        Zipf zipf = new Zipf(0.3, 100);
        Random rnd;

        public TrendModel(int seed)
        {
            rnd = new Random(seed);
        }

        public int TbpVal
        {
            get { return TbpVals[0]; }
        }

        public double queryProbability(int t, int k)
        {
            if (t < TStarts[k]) return 0;
            if (Shape == 1) return QMaxes[k];
            if (Shape == 2)
            {
                double tau = (TPeaks[k] - TStarts[k]) / 3.0;
                return QMaxes[k] * (1 - Math.Exp(-(t - TStarts[k]) / tau));
            }
            if (Shape == 3)
            {
                int dur = TPeaks[k] - TStarts[k];
                int tEnd = TPeaks[k] + dur;
                if (t >= tEnd) return 0;
                if (t < TPeaks[k])
                    return QMaxes[k] * (t - TStarts[k]) / (double)dur;
                return QMaxes[k] * (tEnd - t) / (double)dur;
            }
            if (t >= TPeaks[k]) return QMaxes[k];
            return QMaxes[k] * (t - TStarts[k]) / (double)(TPeaks[k] - TStarts[k]);
        }

        public int getQueryValue(int t)
        {
            double u = rnd.NextDouble();
            double acc = 0;
            for (int k = 0; k < TbpVals.Length; k++)
            {
                acc += queryProbability(t, k);
                if (u < acc) return TbpVals[k];
            }
            return 101 - (int)zipf.getZipfValue(0.3, 100);
        }
    }
}
