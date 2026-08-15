using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScalableP2P
{
    class Node
    {
        static Zipf zipf = new Zipf(0.3, 100);
        static Zipf zipf2 = new Zipf(0.3, 100);

        Item item;
        Link nextLink;
        int nodeID;
        Node nextNode;
        int degree;
        double shortEwma;
        double longEwma;
        double meanTraversals;
        double trendScore;
        double baselineShare;
        double cusum;
        int windowHits;
        int windowTraversals;
        bool prevTrigger1;
        bool prevTrigger2;
        bool trending;
        bool hotLevel;
        int hotStreak;
        bool ewmaInitialized;
        // giris atfi: 0 yok, 1 k-sigma/sabit esik, 2 CUSUM, 3 seviye kurali.
        // windowEntryRule yalniz gecisin oldugu pencerede doludur; lastEntryRule kalicidir
        int windowEntryRule;
        int lastEntryRule;
        // Cooper (Middleware 2005) square-root-construct taban cizgisi icin
        // kumulatif eslesen/gorulen sorgu sayaclari (mu ile yaslandirilabilir)
        double coopMatch;
        double coopTotal;
        static Random rnd = new Random(100003);

        public int Degree
        {
            get { return degree; }
            set { degree = value; }
        }
        internal Item Item
        {
            get { return item; }
            set { item = value; }
        }
        public int NodeID
        {
            get { return nodeID; }
            set { nodeID = value; }
        }
        public Node NextNode
        {
            get { return nextNode; }
            set { nextNode = value; }
        }
        public Link NextLink
        {
            get { return nextLink; }
            set { nextLink = value; }
        }

        public Node(int id)
        {
            item = new Item();
            nodeID = id;
            nextNode = null;
            nextLink = null;
            //degree = 3;
        }

        public double TrendScore
        {
            get { return trendScore; }
        }

        public int WindowEntryRule
        {
            get { return windowEntryRule; }
        }

        public int LastEntryRule
        {
            get { return lastEntryRule; }
        }

        // trend modundaki dugum: giris, art arda persistWindows pencere
        // istatistiksel olarak anlamli yukselisle (3-sigma) olur; cikis,
        // talep payi tetik anindaki tabana geri dustugunde (histerezis).
        // Hem yeniden kablolama hem baglanma agirligindaki trend terimi
        // yalniz bu durumu kullanir
        public bool Persistent
        {
            get { return trending || hotLevel; }
        }

        // her kosunun (tohumun) bagimsiz olmasi icin rastgelelik kaynaklarini
        // verilen tohumdan turetilen degerlerle yeniden baslatir
        public static void resetSeeds(int seed)
        {
            zipf = new Zipf(0.3, 100, 10007 + seed);
            zipf2 = new Zipf(0.3, 100, 20011 + seed);
            rnd = new Random(100003 + seed);
        }

        // uzerinden gecen her sorgu sayilir (q); dugumun sahip oldugu bir
        // icerige denk geliyorsa isabet de sayilir (h). Sinyal, gorunurlukten
        // bagimsiz olan isabet payi h/q'dur: dugumun baglantisi arttikca hem
        // h hem q ayni oranda buyudugunden hub'lasma sahte trend uretmez.
        public void observeQuery(int val)
        {
            windowTraversals++;
            if (item.Values.Contains(val))
                windowHits++;
        }

        // Pencere kapanirken isabet payinin kisa/uzun donem EWMA farki D
        // yukselis sinyalini verir. Giris limiti oz-kalibredir ve p-chart
        // pratigindeki gibi dogrudan binom varyansindan gelir: pencere
        // isabetleri h ~ Binom(q, p) oldugundan Var(pay) = p(1-p)/q'dur ve
        // p yerine uzun donem ortalama L, q yerine ziyaret EWMA'si konur.
        // Var(S-L) = (a_s/(2-a_s) + a_l/(2-a_l)) * Var(pay) = 0.121 * Var(pay).
        // Pencere tetigi: D >= max(k * sigmaD, shareFloor) VE pencerede en az
        // bir taze isabet (h >= 1). Kalicilik, Western Electric kosu kurali
        // bicimindedir: son uc pencerenin en az persistWindows tanesinde
        // tetik varsa ve sonuncusu tetikliyse trend moduna girilir; giriste
        // uzun donem pay taban olarak dondurulur. Cikis (histerezis): kisa
        // donem pay, tabanin shareFloor bandina geri dustugu ILK pencerede
        // mod kapanir; boylece adaptasyon, talep yukselisi surdukce degil
        // talep YUKSEK kaldikca surer ve talep sondugunde kendiliginden
        // durur. Trend modunda skor, tabanin uzerindeki yukselti olarak
        // raporlanir (baglanma agirligi icin). Ilk gozlem penceresinde
        // EWMA'lar gozlenen payla baslatilir; boylece aga yeni katilan
        // dugumler sahte tetik uretmez.
        // oracle taban cizgisi: dis bilgiyle trend moduna zorla (deneysel ust sinir)
        public void forceTrending()
        {
            trending = true;
            baselineShare = 0;
        }

        // fixedThreshold > 0 ise oz-kalibrasyon kapatilir: tek sabit mutlak
        // esik D >= fixedThreshold kullanilir ve CUSUM devre disi kalir
        // (oz-kalibrasyonun degerini izole eden kontrol taban cizgisi).
        // rules bit maskesi giris kurallarini secer (1=k-sigma/sabit esik,
        // 2=CUSUM, 4=seviye; varsayilan 7=tumu) — detektor ablasyonu ve
        // temiz sabit-esik kontrolu icin. Giris atfi windowEntryRule'a yazilir.
        public void updateTrendWindow(double sigmaK, double shareFloor, int persistWindows, double cusumTheta, double fixedThreshold, double hotShare, int rules)
        {
            windowEntryRule = 0;
            bool wasPersistent = trending || hotLevel;
            int enteredBy = 0;
            if (windowTraversals > 0)
            {
                double share = (double)windowHits / windowTraversals;
                if (!ewmaInitialized)
                {
                    shortEwma = share;
                    longEwma = share;
                    meanTraversals = windowTraversals;
                    ewmaInitialized = true;
                }
                else
                {
                    bool freshEvidence = windowHits >= 1;
                    double prevLong = longEwma;
                    shortEwma = 0.2 * share + 0.8 * shortEwma;
                    longEwma = 0.02 * share + 0.98 * longEwma;
                    meanTraversals = 0.05 * windowTraversals + 0.95 * meanTraversals;
                    if (!trending)
                    {
                        trendScore = Math.Max(0, shortEwma - longEwma);
                        double shareVar = longEwma * (1 - longEwma) / Math.Max(meanTraversals, 1);
                        double sigmaD = Math.Sqrt(0.121 * shareVar);
                        // Kural 1 (hizli yol): pencerede taze isabet VE
                        // k-sigma / maddilik limitinin asilmasi
                        double limit = fixedThreshold > 0
                            ? fixedThreshold
                            : Math.Max(sigmaK * sigmaD, shareFloor);
                        bool triggered = (rules & 1) != 0 && freshEvidence && trendScore >= limit;
                        int recent = (triggered ? 1 : 0) + (prevTrigger1 ? 1 : 0) + (prevTrigger2 ? 1 : 0);
                        // Kural 2 (guvenlik agi): Bernoulli CUSUM, taban payin
                        // shareFloor uzerindeki her kalici yukselisi zaman
                        // icinde biriktirir; yavas veya duzensiz aralikli
                        // trendleri pencere kuantizasyonundan bagimsiz yakalar
                        cusum = Math.Max(0, cusum + windowHits - windowTraversals * (prevLong + shareFloor));
                        bool rule1Entry = triggered && recent >= persistWindows;
                        bool cusumEntry = (rules & 2) != 0 && fixedThreshold <= 0 && cusum >= cusumTheta;
                        if (rule1Entry || cusumEntry)
                        {
                            trending = true;
                            enteredBy = rule1Entry ? 1 : 2;
                            // taban: trend oncesi kalici talep duzeyi
                            baselineShare = prevLong;
                            prevTrigger1 = false;
                            prevTrigger2 = false;
                            cusum = 0;
                        }
                        else
                        {
                            prevTrigger2 = prevTrigger1;
                            prevTrigger1 = triggered;
                        }
                    }
                    else
                    {
                        trendScore = Math.Max(0, shortEwma - baselineShare);
                        if (shortEwma < baselineShare + shareFloor)
                        {
                            trending = false;
                            trendScore = Math.Max(0, shortEwma - longEwma);
                        }
                    }
                    // Seviye kurali (dinamik populerlik): turev sinyali,
                    // talep zaten yuksekken doğan dugumlerde (cokme sonrasi
                    // yeniden giren kopya, ani flash-crowd, sicak icerige gec
                    // katilan tutucu) hic yukselmez cunku sicak baslatma
                    // yuksek talebi taban olarak ogrenir. Payin yuksek su
                    // isareti hotShare uzerinde KALMASI da adaptasyon
                    // gerektirir: en populer siradan icerigin payinin bile
                    // birkac kati olan bu seviye, talebin kendisinin dugumu
                    // merkeze tasimayi hak ettigi esiktir. Cikis histerezisi
                    // hotShare - shareFloor bandindadir.
                    if ((rules & 4) != 0)
                    {
                        if (freshEvidence && shortEwma >= hotShare)
                            hotStreak++;
                        else if (shortEwma < hotShare - shareFloor)
                        {
                            hotStreak = 0;
                            hotLevel = false;
                        }
                        if (hotStreak >= persistWindows) hotLevel = true;
                        if (hotLevel && !trending)
                            trendScore = Math.Max(trendScore, shortEwma - (hotShare - shareFloor));
                    }
                }
            }
            if (!wasPersistent && (trending || hotLevel))
            {
                windowEntryRule = trending ? enteredBy : 3;
                lastEntryRule = windowEntryRule;
            }
            windowHits = 0;
            windowTraversals = 0;
        }

        // Cooper (Middleware 2005) square-root-construct taban cizgisi:
        // kumulatif eslesen/gorulen sayaclar pencere kapanisinda toplanir ve
        // mu ile yaslandirilir (mu=1: saf kumulatif, Cooper'in varsayilani;
        // mu<1: degisen populerlik icin onerdigi decay). Detektor yoktur;
        // hedef derece dogrudan kestirimden gelir.
        public void updateCooperWindow(double mu)
        {
            coopMatch = mu * coopMatch + windowHits;
            coopTotal = mu * coopTotal + windowTraversals;
            windowHits = 0;
            windowTraversals = 0;
        }

        // hedef derece d_k = round(dmax * sqrt(Q_match/Q_total)), [dmin, dmax]
        // araliginda; hic gozlem yoksa mevcut derece korunur
        public int cooperTargetDegree(double dmax, int dmin)
        {
            if (coopTotal <= 0) return degree;
            int d = (int)Math.Round(dmax * Math.Sqrt(coopMatch / coopTotal));
            if (d < dmin) d = dmin;
            if (d > (int)dmax) d = (int)dmax;
            return d;
        }

        public void addItem(object value)
        {
            item.addItem(value);
        }
        public void addItemsRandomly()
        {

            //int limit = rnd.Next(maximumItem);

            int limit = (int)zipf2.getZipfValue(0.3, 100);
            //Console.Write(limit+" ");
            limit = 1;
            for (int i = 0; i <limit ; i++)
            {
                int val = (int)zipf.getZipfValue(0.3, 100);
                //Console.Write(val + " ");
                //int val = (int)rnd.Next(100);
                item.addItem(val); 
            }
        }
    }
}
