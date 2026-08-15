using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScalableP2P
{
    class Graph
    {
        ArrayList removed = new ArrayList();
        // TBP iceriklerin girecegi buyume adimlari ve yerlestirilme durumu;
        // tek icerikli varsayilan durumda tek elemanli dizidir
        public int[] TbpEntrySteps = new int[] { 2000 };
        bool[] tbpPlaced = null;
        // tek kopyali TBP tutuculari olcum boyunca churn'den korunur
        HashSet<int> protectedIds = new HashSet<int>();
        // >0 ise bu simulasyon adiminda TBP tutucusu agdan koparilir ve
        // icerik bir sonraki katilan dugumde yeniden ortaya cikar
        public int HolderCrashStep = 0;
        // kontrol taban cizgileri: her pencere bu kadar rastgele dugume ayni
        // join butcesi verilir (yerlestirmenin onemini izole eder)
        public int RandomRewireNodes = 0;
        // oracle dedektor: tutucu TStart'tan itibaren dis bilgiyle trend
        // modunda tutulur (tespit gecikmesinin maliyetini izole eder)
        public bool OracleDetector = false;
        // >0 ise oz-kalibrasyon yerine sabit mutlak esik kullanilir
        public double FixedThreshold = 0;
        // seviye kurali yuksek su isareti (5 x ShareFloor): pay bunun
        // uzerinde kaldigi surece dugum adaptasyon modundadir
        public double HotShare = 0.10;
        // giris kurali bit maskesi: 1=k-sigma/sabit esik, 2=CUSUM, 4=seviye.
        // Varsayilan 7 (tumu). Detektor ablasyonu (tek kural) ve temiz
        // sabit-esik kontrolu (rules=1) icin CLI'dan secilir
        public int Rules = 7;
        // Cooper (Middleware 2005) square-root-construct taban cizgisi:
        // trend katmani yerine her dugum d_k = dmax*sqrt(match/total) hedef
        // derecesine dogru rastgele eslerle link ekler/siler
        public bool CooperMode = false;
        public double CooperDmax = 160;
        public double CooperMu = 1.0;
        int lastWindowEntK = 0;
        int lastWindowEntC = 0;
        int lastWindowEntL = 0;
        // plato modu: buyume bittikten sonra nufus sabit tutulur (ayrilan her
        // dugumun yerine yenisi katilir); TBP yerlestirme bu evrede add-index
        // yerine simTime ile yapilir cunku plato adimlarinin cogunda katilim olmaz
        bool plateauActive = false;
        // Acquaintances-tarzi (Cholvi vd. 2004) istekci-tarafli reaktif taban
        // cizgisi: BASARILI bir TBP is-yuku sorgusundan sonra istekci, bulunan
        // tutucuya dogrudan link ekler. Yalniz TBP icin verilir (arka plan
        // harcamasindan muaf tutarak taban cizgisi LEHINE bir secim)
        // 0=kapali; 1=yalniz-TBP, sinirsiz (taban cizgisi LEHINE ust sinir:
        // kapsam oraclesi + butce siniri yok); 2=gercekci: TUM iceriklerin
        // basarili sorgulari, pencere basina 8 linklik esli butceyle (FCFS)
        public int ReactiveLinking = 0;
        public int ReactiveQuota = 8;
        int reactiveQuotaLeft = 8;
        int reactiveNodesWindow = 0;
        int reactiveLinksWindow = 0;
        public double Pp = 0;
        public double Pd = 1;
        public double Pt = 0;                  // trend agirligi (tercihli baglanmada)
        public bool RewireEnabled = false;     // trend esigini asanlar icin proaktif yeniden kablolama
        public TrendModel Trend = null;        // zamana bagli sorgu is yuku
        public System.IO.StreamWriter MetricsWriter = null;
        public int QueryEvery = 5;             // kac simulasyon adiminda bir is yuku sorgusu
        public int WorkloadTtl = 8;
        public int ProbeTtl = 12;
        public int WindowSize = 200;           // trend guncelleme/olcum penceresi
        // oz-kalibre karar limiti: D >= max(SigmaK * sigmaD, ShareFloor).
        // SigmaK klasik uc-sigma kuralidir; ShareFloor yerel trafigin yuzde
        // birinin altindaki talep artislarini onemsiz sayan maddilik tabanidir
        public double SigmaK = 5.0;
        public double ShareFloor = 0.02;
        // Bernoulli CUSUM esigi (fazla-isabet birimi): tabanin ShareFloor
        // ustundeki kalici yukselisler bu kadar birikince trend moduna girilir
        public double CusumTheta = 6.0;
        // gecici talep dalgalanmalarini elemek icin limitin art arda kac
        // pencere asilmasi gerektigi; gercek trendler surekli, gurultu tek pencerelik
        public int PersistWindows = 2;
        // yerel kural: filtreyi gecen her dugum pencere basina en fazla bu
        // kadar join baslatir; kuresel siralama veya es gudum gerektirmez
        public int RewireJoinsPerNode = 2;
        int lastWindowRewireNodes = 0;
        int lastWindowRewireJoins = 0;
        // zarar-yok olcumu icin arka plan sorgu hedefleri; sabit tohum sayesinde
        // tum konfigurasyon ve tohumlarda ayni hedef dizisi uretilir
        Zipf bgZipf = new Zipf(0.3, 100);
        int simTime = 0;
        int cnt;
        int target;
        int minDegree;
        int maxDegree = 10000 ;
        int N = -1;
        int tj = 2;
        int tl = 2;
        double u = 300;
        Node nodeHead;
        //Random rnd = new Random(1000003);
        //Random rnd = new Random(10007);

          Random rnd = new Random();

        public Graph(int degree)
        {
            minDegree = degree;
        }

        public Graph(int degree, int seed)
        {
            minDegree = degree;
            rnd = new Random(seed);
        }

        public Node createNode(int index)
        {
            return new Node(index);
        }

        public Link createLink(int index)
        {
            Link temp = new Link(index);
            temp.VertexLink = search(index);

            return temp;
        }
        public void initialGraph()
        {
            
            nodeHead = createNode(0);
            Node n1 = createNode(1);
            Node n2 = createNode(2);
            Node n3 = createNode(3);
            Node n4 = createNode(4);
            Node n5 = createNode(5);
            Node n6 = createNode(6);
            Node n7 = createNode(7);
            Node n8 = createNode(8);
            Node n9 = createNode(9);

            nodeHead.NextNode = n1;
            n1.NextNode = n2;
            n2.NextNode = n3;
            n3.NextNode = n4;
            n4.NextNode = n5;
            n5.NextNode = n6;
            n6.NextNode = n7;
            n7.NextNode = n8;
            n8.NextNode = n9;

            nodeHead.NextLink = createLink(1); nodeHead.Degree++;
            nodeHead.NextLink.NextLink = createLink(2); nodeHead.Degree++;
            nodeHead.NextLink.NextLink.NextLink = createLink(3); nodeHead.Degree++;
            n1.NextLink = createLink(0); n1.Degree++;
            n1.NextLink.NextLink = createLink(2); n1.Degree++;
            n1.NextLink.NextLink.NextLink = createLink(3); n1.Degree++;
            n1.NextLink.NextLink.NextLink.NextLink = createLink(9); n1.Degree++;
            
            n2.NextLink = createLink(1); n2.Degree++;
            n2.NextLink.NextLink = createLink(0); n2.Degree++;
            n2.NextLink.NextLink.NextLink = createLink(6); n2.Degree++;
            n3.NextLink = createLink(0); n3.Degree++;
            n3.NextLink.NextLink = createLink(4); n3.Degree++;
            n3.NextLink.NextLink.NextLink = createLink(1); n3.Degree++;
            n4.NextLink = createLink(3); n4.Degree++;
            n4.NextLink.NextLink = createLink(5); n4.Degree++;
            n4.NextLink.NextLink.NextLink = createLink(8); n4.Degree++;
            n5.NextLink = createLink(4); n5.Degree++;
            n5.NextLink.NextLink = createLink(8); n5.Degree++;
            n5.NextLink.NextLink.NextLink = createLink(9); n5.Degree++;
            n6.NextLink = createLink(2); n6.Degree++;
            n6.NextLink.NextLink = createLink(7); n6.Degree++;
            n6.NextLink.NextLink.NextLink = createLink(8); n6.Degree++;
            n7.NextLink = createLink(6); n7.Degree++;
            n7.NextLink.NextLink = createLink(9); n7.Degree++;
            n7.NextLink.NextLink.NextLink = createLink(8); n7.Degree++;

            n8.NextLink = createLink(6); n8.Degree++;
            n8.NextLink.NextLink = createLink(4); n8.Degree++;
            n8.NextLink.NextLink.NextLink = createLink(5); n8.Degree++;
            n8.NextLink.NextLink.NextLink.NextLink = createLink(7); n8.Degree++;

            n9.NextLink = createLink(7); n9.Degree++;
            n9.NextLink.NextLink = createLink(5); n9.Degree++;
            n9.NextLink.NextLink.NextLink = createLink(1); n9.Degree++;
            
            N = 9;//already have 10 nodes


            //ArrayList sub = createSubGraph(nodeHead, 4);
            //for (int i = 0; i < sub.Count; i++)
            //{
            //    Console.WriteLine(((Node)sub[i]).NodeID);
            //} 
                
            
        }
        public void grow(int nTarget)
        {
            target = nTarget + 10;
            for (int i = 0; i < nTarget+1; i++)
            {
                //if (i==2005|| i == 3000 || i == 4000 || i == 5000 || i == 6000 || i == 7000 || i == 8000 || i == 9000 || i == 10000)
                //    Console.WriteLine(i + "  " + searchForVal(1000));
                //if ((i + 200) % 200 == 0)
                //    Console.WriteLine(i);    
                addNode(i);
                
                double num = rnd.Next(1, 1000);
               
                if (num < u)
                {
                    Node nodeDelete;
                    int id;
                    do
                    {
                        id = rnd.Next(0, N);
                        nodeDelete = search(id);
                         
                    } while (nodeDelete == null || protectedIds.Contains(id));
                    
                    removed.Add(id);
                    leaveNode(nodeDelete, tl);
                    cnt++;
                    i--;
                }

                simTime++;
                // kontrollu tutucu cokmesi: dugum tum baglantilariyla ayrilir,
                // icerik bir sonraki katilan dugumde tek kopya olarak yeniden dogar
                if (HolderCrashStep > 0 && simTime == HolderCrashStep && Trend != null)
                {
                    Node holder = getHolder(Trend.TbpVal);
                    if (holder != null)
                    {
                        protectedIds.Remove(holder.NodeID);
                        removed.Add(holder.NodeID);
                        leaveNode(holder, tl);
                        cnt++;
                        TbpEntrySteps[0] = i + 1;
                        tbpPlaced[0] = false;
                    }
                }
                if (Trend != null && simTime % QueryEvery == 0)
                {
                    int dummy = 0;
                    Node requester = getRndNode();
                    int qval = Trend.getQueryValue(simTime);
                    int qhits = nfSearch(requester, qval, WorkloadTtl, true, ref dummy);
                    if (ReactiveLinking == 1 && qhits > 0 && qval == Trend.TbpVal)
                        reactiveLink(requester, qval);
                    else if (ReactiveLinking == 2 && qhits > 0 && reactiveQuotaLeft > 0)
                    {
                        reactiveLink(requester, qval);
                        reactiveQuotaLeft--;
                    }
                }
                if (simTime % WindowSize == 0)
                {
                    if (CooperMode)
                        cooperWindow();
                    else
                    {
                        updateTrends();
                        if (RewireEnabled) rewireTrending();
                    }
                    if (RandomRewireNodes > 0) rewireRandom();
                    if (ReactiveLinking > 0)
                    {
                        lastWindowRewireNodes = reactiveNodesWindow;
                        lastWindowRewireJoins = reactiveLinksWindow;
                        reactiveNodesWindow = 0;
                        reactiveLinksWindow = 0;
                        reactiveQuotaLeft = ReactiveQuota;
                    }
                    if (MetricsWriter != null) logMetrics();
                }
            }

            Console.WriteLine("\n Growing has been completed\n*********************************************************************\n");

        }

        // plato evresi: nufus VE kenar butcesi sabit, olgun ag. Churn olayi
        // slot-devralmadir: ayrilan dugumun kenar listesi oldugu gibi yeni
        // (gecmissiz, soguk detektor durumlu) bir dugume aktarilir; toplam
        // kenar sayisi olay basina tam korunur, boylece trendsiz taban
        // cizgisinde E sabittir ve trend katmaninin ekledigi her kenar
        // dogrudan olculebilir. TBP yerlestirme sirasi geldiginde kurban,
        // minimum dereceli bir dugumden secilir ki tutucu diger deneylerle
        // karsilastirilabilir bicimde derece-3'ten baslasin.
        public void plateauRun(int steps)
        {
            plateauActive = true;
            for (int j = 0; j < steps; j++)
            {
                double num = rnd.Next(1, 1000);
                if (num < u)
                {
                    // TBP yerlestirme zamani geldiyse dusuk dereceli slot sec
                    bool tbpDue = false;
                    if (tbpPlaced != null)
                        for (int k = 0; k < TbpEntrySteps.Length; k++)
                            if (!tbpPlaced[k] && simTime >= TbpEntrySteps[k]) { tbpDue = true; break; }
                    Node victim;
                    int id;
                    int tries = 0;
                    do
                    {
                        id = rnd.Next(0, N);
                        victim = search(id);
                        tries++;
                    } while (victim == null || protectedIds.Contains(id)
                             || (tbpDue && victim.Degree != minDegree && tries < 200));
                    removed.Add(id);
                    cnt++;
                    replaceNode(victim);
                }
                simTime++;
                if (Trend != null && simTime % QueryEvery == 0)
                {
                    int dummy = 0;
                    nfSearch(getRndNode(), Trend.getQueryValue(simTime), WorkloadTtl, true, ref dummy);
                }
                if (simTime % WindowSize == 0)
                {
                    if (CooperMode)
                        cooperWindow();
                    else
                    {
                        updateTrends();
                        if (RewireEnabled) rewireTrending();
                    }
                    if (RandomRewireNodes > 0) rewireRandom();
                    if (MetricsWriter != null) logMetrics();
                }
            }
            Console.WriteLine("\n Plateau completed at t=" + simTime + "\n");
        }

        // slot-devralma: yeni dugum, ayrilanin kenar listesini ve derecesini
        // aynen devralir (komsularin link kayitlari yeni kimlige cevrilir);
        // detektor durumu sifirdan baslar, icerik yeniden cekilis yapilir
        // (sirasi gelmis TBP icerigi varsa o yerlestirilir)
        private void replaceNode(Node victim)
        {
            N++;
            Node fresh = createNode(N);
            fresh.NextLink = victim.NextLink;
            fresh.Degree = victim.Degree;
            // komsularin victim'e bakan linkleri yeni dugume yonlendirilir
            Link it = fresh.NextLink;
            while (it != null)
            {
                Node peer = it.VertexLink;
                if (peer != null)
                {
                    Link pit = peer.NextLink;
                    while (pit != null)
                    {
                        if (pit.Id == victim.NodeID)
                        {
                            pit.Id = fresh.NodeID;
                            pit.VertexLink = fresh;
                            break;
                        }
                        pit = pit.NextLink;
                    }
                }
                it = it.NextLink;
            }
            // victim listeden cikarilir, fresh listeye eklenir
            if (victim == nodeHead)
                nodeHead = victim.NextNode;
            else
            {
                Node prev = nodeHead;
                while (prev != null && prev.NextNode != victim) prev = prev.NextNode;
                if (prev != null) prev.NextNode = victim.NextNode;
            }
            Node tail = nodeHead;
            while (tail.NextNode != null) tail = tail.NextNode;
            tail.NextNode = fresh;
            // icerik: sirasi gelmis TBP varsa yerlestir, yoksa rastgele cekilis
            int slot = -1;
            if (tbpPlaced != null)
                for (int k = 0; k < TbpEntrySteps.Length; k++)
                    if (!tbpPlaced[k] && simTime >= TbpEntrySteps[k]) { slot = k; break; }
            if (slot >= 0)
            {
                int val = Trend != null ? Trend.TbpVals[slot] : 1000;
                fresh.addItem(val);
                fresh.Item.Popularity = 1;
                protectedIds.Add(fresh.NodeID);
                tbpPlaced[slot] = true;
            }
            else
                fresh.addItemsRandomly();
        }

        public void addNode(int i)
        {
            N++;
            if (nodeHead == null)
            {
                nodeHead = createNode(N);
                nodeHead.addItemsRandomly();
            }else
            {
                Node n=null;
                int lastId = N - 1;
                do
                {
                    n = search(lastId);
                    if (n == null)
                        lastId = lastId - 1;
                } while (n == null);
                //Console.WriteLine("here"+N);
                n.NextNode = createNode(N);
                if (tbpPlaced == null) tbpPlaced = new bool[TbpEntrySteps.Length];
                int slot = -1;
                for (int k = 0; k < TbpEntrySteps.Length; k++)
                {
                    bool due = plateauActive ? simTime >= TbpEntrySteps[k]
                                             : i == TbpEntrySteps[k];
                    if (due && !tbpPlaced[k]) { slot = k; break; }
                }
                if (slot >= 0)
                {
                    int val = Trend != null ? Trend.TbpVals[slot] : 1000;
                    n.NextNode.addItem(val);
                    // TBP senaryosu: icerik aga girdiginde talep henuz olusmadigindan
                    // statik populerligi dusuk tutulur; talep sonradan Trend modeliyle yukselir
                    n.NextNode.Item.Popularity = 1;
                    protectedIds.Add(n.NextNode.NodeID);
                    tbpPlaced[slot] = true;
                }
                else
                    n.NextNode.addItemsRandomly();
                join();
            }
            // minDegree saglanincaya kadar tüm node'lar ile cift yonlu baglantı yapilir.
            //else if (minDegree >= N)
            //{
            //    Node iterator = search(N - 1);
            //    iterator.NextNode = createNode(N);
            //    iterator = iterator.NextNode;
            //    for (int i = N - 1; i >= 0; i--)
            //    {
            //        if (iterator.NextLink == null)
            //        {
            //            iterator.NextLink = createLink(i);
                        
            //        }
            //        else
            //            findLastLink(iterator.NextLink).NextLink = createLink(i);
            //        iterator.Degree++;
            //    }

            //    for (int i = 0; i < N; i++)
            //    {
            //        iterator = search(i);
            //        if (iterator.NextLink == null)
            //            iterator.NextLink = createLink(N);
            //        else
            //            findLastLink(iterator.NextLink).NextLink = createLink(N);
            //        iterator.Degree++;
            //    }
            //}
            //// minDegree kosulu saglandiktan sonra yeni node eklemeyi join fonksiyonu yapar
            

        }

        public void join()
        {
            int numOfLinks = 0;
            ArrayList randomNodes = new ArrayList();
            while (numOfLinks < minDegree)
            {
                // Baglanti kurmak icin agdan rastgele bir node secilir ve komsululuk alt grafi bulunur
                Node n;
                do
                {
                    n = search(rnd.Next(0, N));
                    if (!randomNodes.Contains(n))
                        randomNodes.Add(n);
                    else
                        continue;
                } while (n == null);
                
                //Node n = search(4);
                //Console.WriteLine("random"+n.NodeID);
               
                ArrayList nodes = createSubGraph(n,tj);
                Node current = search(N);
                numOfLinks += preferentialAttachment(current, nodes, 0, null, numOfLinks);
            }
        }
        public void join(Node current)
        {
            int numOfLinks = current.Degree;
            int currrentdegree = numOfLinks;
            ArrayList randomNodes = new ArrayList();
            int counter = 0;
            while (numOfLinks == currrentdegree && counter<3)
            {
                // Baglanti kurmak icin agdan rastgele bir node secilir ve komsululuk alt grafi bulunur
                counter++;
                Node n;
                do
                {

                    n = search(rnd.Next(0, N));
                    //if(n!=null) Console.WriteLine("here" + n.NodeID);
                    // Console.WriteLine("N..:"+N);
                    //if (n == null)
                    //    Console.WriteLine("null");
                    //else
                    //    Console.WriteLine("id.." + n.NodeID);
                    if (!randomNodes.Contains(n))
                        randomNodes.Add(n);
                    else
                        continue;
                } while (n == null || n.NodeID==current.NodeID);

                //Node n = search(4);
                //Console.WriteLine("random"+n.NodeID);

                ArrayList nodes = createSubGraph(n, tj);
                //Console.WriteLine("nodes.count.."+nodes.Count);
                int nlinks=preferentialAttachment(current, nodes, 1, null, numOfLinks);
                //Console.WriteLine("linkss.."+nlinks+ "   "+ nodes.Count);
                numOfLinks += nlinks;
            }
        }

        public ArrayList createSubGraph(Node n, int hop)
        {
            ArrayList nodes = new ArrayList();
            // buyuk alt-graflarda O(k^2)'ye donusen ArrayList.Contains yerine HashSet
            HashSet<Node> seen = new HashSet<Node>();
            ArrayList newNodes = new ArrayList();
            nodes.Add(n);
            seen.Add(n);
            newNodes.Add(n);
            for (int i = 0; i < hop; i++)
            {
                ArrayList newlyVisited = new ArrayList();
                for (int j = 0; j < newNodes.Count; j++)
                {
                    Link iteratorLink = ((Node)newNodes[j]).NextLink;
                    while (iteratorLink != null)
                    {
                        Node temp = iteratorLink.VertexLink;
                        if (!seen.Contains(temp))
                        {
                            seen.Add(temp);
                            nodes.Add(temp);
                            newlyVisited.Add(temp);
                        }
                        iteratorLink = iteratorLink.NextLink;
                    }
                }
                newNodes = newlyVisited;
            }
            return nodes;
        }
        
        public int preferentialAttachment(Node joiningNode, ArrayList subGraph, int limit, ArrayList immediateNeighbours, int totalNumberOfLinks)
        {
            int links = 0;
            int loopCount;
            //Node joiningNode = search(id);
            if (joiningNode == null)
                return 0;
            //subGraph = sort(subGraph);
            //Console.WriteLine("before PA "+ subGraph.Count);
            subGraph = sortWithPA(subGraph, Pp, Pd);
            //Console.WriteLine("after PA "+subGraph.Count);
            //for (int i = 0; i < subGraph.Count; i++)
            //{
            //    Console.WriteLine(((Node)subGraph[i]).NodeID + "  " + ((Node)subGraph[i]).Item.Popularity);
            //}
            //if (limit == 0 || subGraph.Count<limit)
             loopCount = subGraph.Count;
            //if (immediateNeighbours != null)
            //{
            //    while (subGraph.Count>0 && ((Node)subGraph[0]).Degree == maxDegree)
            //    {
            //        subGraph.RemoveAt(0);
            //    }
            //}
            if (subGraph.Count == 0) return 0;

            for (int i = 0; i < loopCount; i++)
            {
                Node item = (Node)subGraph[i];
                //Console.WriteLine(item.Degree + "  " + item.NodeID + "  " + joiningNode.NodeID);
                //if (((limit == 1 && doesLinkExist(item, joiningNode.NodeID)) || ((limit==1) && item.NodeID == joiningNode.NodeID)) && subGraph.Count > 1)
                //    do
                //    {
                //        item = (Node)subGraph[i + 1];
                //        i++;
                //    } while (i<subGraph.Count-1 &&  doesLinkExist(item, joiningNode.NodeID));
 
                //Console.Write(item.Degree + "  "+ item.NodeID+ "  "+joiningNode.NodeID);
                //Console.WriteLine(doesLinkExist(item, joiningNode.NodeID));
                //if (item.Degree > target)
                //{
                //    Console.Write(item.Degree + "  ");
                //    Link iterator = ((Node)subGraph[i]).NextLink;
                //    while (iterator != null)
                //    {
                //        Console.Write(iterator.Id+" ");
                //        iterator = iterator.NextLink;
                //    }
                //    Environment.Exit(0);
                //}
                //Console.WriteLine("before if");
                if (item.Degree < maxDegree && item.NodeID != joiningNode.NodeID && !doesLinkExist(item, joiningNode.NodeID))
                {
                    //Console.WriteLine("inside");
                    if (joiningNode.NextLink == null)
                        joiningNode.NextLink = createLink(item.NodeID);
                    else
                        findLastLink(joiningNode.NextLink).NextLink = createLink(item.NodeID);  // eklenen node'dan alt grafa baglanti
                    joiningNode.Degree++;

                    Link lastOne=findLastLink(item.NextLink);
                    if(lastOne!=null) // alt graftan eklenen node'a baglanti
                        lastOne.NextLink = createLink(joiningNode.NodeID);
                    else
                        item.NextLink = createLink(joiningNode.NodeID);
                    //used to prevent extra links when a node leaves 
                    if (immediateNeighbours != null)
                    {
                        immediateNeighbours.Remove(item);
                        //subGraph.Remove(item);
                    }
                    
                    item.Degree++;
                    links++;
                    if ((totalNumberOfLinks + links) >= minDegree)
                        return links;
                    if (limit == 1) return links;
                }
            }
            return links;
        }

        public void leaveNode(Node nodeDelete, int tl)
        {
            // silinecek node'un komsularindan olusan alt graf
            
            if (nodeDelete == null)
                return;
            ArrayList subGraph = createSubGraph(nodeDelete,tl);
            ArrayList immediateNeighbours = createSubGraph(nodeDelete, 1);
            foreach (Node item in immediateNeighbours)
            {
                 
                Link iteratorLink = item.NextLink;
                Link prevIterator = item.NextLink;
                while (iteratorLink != null)
                {
                   
                    // alt graftan silinen node'a yapilan baglantilar kopariliyor
                    if (iteratorLink.Id == nodeDelete.NodeID)
                    {
                        if (iteratorLink == prevIterator)
                            item.NextLink = iteratorLink.NextLink;
                        else
                            prevIterator.NextLink = iteratorLink.NextLink;
                        item.Degree--;
                        break;
                    }
                    prevIterator = iteratorLink;
                    iteratorLink = iteratorLink.NextLink;
                }
            }

            // agdan nodeDelete siliniyor
            if (nodeDelete == nodeHead)
                nodeHead = nodeHead.NextNode;
            else
            {
                Node iterator = nodeHead;
                Node prevNode = nodeHead;
                while (iterator != null)
                {
                    if (iterator == nodeDelete)
                    {
                        prevNode.NextNode = iterator.NextNode;
                        break;
                    }
                    prevNode = iterator;
                    iterator = iterator.NextNode;
                }
            }
             
            subGraph.Remove(nodeDelete);//remove the deleted node from subgraph
            immediateNeighbours.Remove(nodeDelete);
            ArrayList originalSubGraph = copyList(subGraph);
            //Console.WriteLine("....................................");
            //display();
            //Console.WriteLine("....................................");
            //Console.WriteLine("after removing six");
            //for (int i = 0; i < originalSubGraph.Count; i++)
            //{
            //    Console.WriteLine(((Node)originalSubGraph[i]).NodeID + "  " + ((Node)originalSubGraph[i]).Item.Popularity);
            //}
            //Console.WriteLine("immediate neighbours");
            //for (int i = 0; i < immediateNeighbours.Count; i++)
            //{
            //    Console.WriteLine(((Node)immediateNeighbours[i]).NodeID + "  " + ((Node)immediateNeighbours[i]).Item.Popularity);
            //}
            for (int i = 0; i < immediateNeighbours.Count; i++)
            {

                Node current = (Node)immediateNeighbours[i];
                Link iterator = current.NextLink;
                while (iterator != null)
                {
                     
                    subGraph.Remove(iterator.VertexLink);
                    iterator = iterator.NextLink;
                }
                subGraph.Remove(current);
                //if (subGraph.Count==0)
                //    Console.WriteLine("000000000000000000  "+ immediateNeighbours.Count+ "  "+ originalSubGraph.Count);
                //Console.WriteLine("calling for "+ current.NodeID);
                if (subGraph.Count == 0)
                {
                    //Console.WriteLine("00000000000000000000000000000000");
                    //int currentDegree = current.Degree;
                    //do
                    //{
                        
                        join(current);
                        
                    //} while (current.Degree == currentDegree);
                }
                else
                {

                        //int currentDegree = current.Degree;
                        //ArrayList copy = copyList(subGraph);
                        //Console.WriteLine("before "+subGraph.Count);
                        preferentialAttachment(current, subGraph, 1, immediateNeighbours, current.Degree);
                        //Console.WriteLine("after  " + subGraph.Count);
                        //if (currentDegree == current.Degree)
                        //{
                        //    Console.WriteLine("sameeeee" + ((Node)subGraph[0]).Degree + "  " + immediateNeighbours.Count + "  " + current.Degree +
                        //        doesLinkExist(current, ((Node)subGraph[0]).NodeID));
                        //}
                        //int counter = 0;
                        //while (currentDegree == current.Degree && counter<10)
                        //{
                        //    join(current);
                        //    counter++;
                        //}
                }
                //Console.WriteLine("....................................");
                //display();
                //Console.WriteLine("....................................");

                subGraph = copyList(originalSubGraph);
                

            }
            for (int i = 0; i < immediateNeighbours.Count; i++)
            {
                if (((Node)immediateNeighbours[i]).Degree < minDegree)
                {
                    Node current=(Node)immediateNeighbours[i];
                    //int currentDegree = current.Degree;
                    join(current);
                   
                }
            }
            
        }

       
        public void display()
        {
            Node iterator = nodeHead;
            if (nodeHead == null)
                Console.WriteLine("Empty Grapf");
            else
            {
                while (iterator != null)
                {
                    Link iteratorLink = iterator.NextLink;
                    //if (iteratorLink != null)
                    //{
                        Console.Write("[Node:" + iterator.NodeID +" "+iterator.Degree+ "](");
                        foreach (var item in iterator.Item.Values)
                        {
                            Console.Write(item + ",");
                        }
                        Console.Write(")\n");
                    //}
                    while (iteratorLink != null)
                    {
                        Node x = iteratorLink.VertexLink;
                        //Console.Write(x.NodeItem + "-");
                        iteratorLink = iteratorLink.NextLink;
                        Console.Write("[index:" + x.NodeID + "](");
                        for (int i = 0; i < x.Item.Values.Count; i++)
                        {
                            Console.Write(x.Item.Values[i] + ",");
                        }
                        Console.Write(")\n");
                    }
                    Console.WriteLine("\n--------------------------------------");
                    iterator = iterator.NextNode;
                }
            }
            Console.WriteLine("removed  "+cnt);
            for (int i = 0; i < removed.Count; i++)
            {
                Console.WriteLine(removed[i]);
            }
        }

        public Node search(int index)
        {
            Node iterator = nodeHead;
            while (iterator != null && iterator.NodeID.CompareTo(index) != 0)
            {
                iterator = iterator.NextNode;
            }
            return iterator;
        }
        public int searchForVal(int val)
        {
            Node iterator = nodeHead;
            while (!iterator.Item.Values.Contains(val))
            {
                iterator = iterator.NextNode;
            }
            return iterator.Degree;
        }

        public bool doesLinkExist(Node current, int id)
        {
            Link iterator = current.NextLink;
            while (iterator != null)
            {
                if (iterator.Id == id)
                    return true;
                iterator = iterator.NextLink;
            }
            return false;

        }
        public void addItemToNode(int index, object item)
        {
            Node n = search(index);
            n.addItem(item);
            //Console.WriteLine("popularity"+ n.NodeID + "  "+n.Item.Popularity);
        }

        // node un yaptigi en son baglantiyi dondurur
        public Link findLastLink(Link iteratorLink)
        {
            if (iteratorLink != null)
            {
                while (iteratorLink.NextLink != null)
                    iteratorLink = iteratorLink.NextLink;
            }
            return iteratorLink;
        }

        public ArrayList getAll()
        {
            ArrayList all = new ArrayList();
            Node iterator = nodeHead;
            while(iterator!=null)
            {
                all.Add(iterator);
                iterator = iterator.NextNode;
            }
            return all;
        }
        public void display(ArrayList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Console.Write(((Node)list[i]).Item.Popularity+ "  ");
            }
        }
        public ArrayList sort(ArrayList graph, double Pp, double Pd)
        {
            Node temp;
            if (graph.Count == 1) return graph;
            for (int i = 0; i < graph.Count; i++)
            {
                for (int j = 0; j < graph.Count - i - 1; j++)
                {
                    if (weightOf((Node)graph[j]) < weightOf((Node)graph[j + 1]))
                    {
                        temp = (Node)graph[j];
                        graph[j] = graph[j + 1];
                        graph[j + 1] = temp;
                    }
                }
            }
            return graph;
        }
        public ArrayList sortWithPA(ArrayList graph, double Pp, double Pd)
        {
            if (graph.Count <= 1) return graph;
            // Efraimidis-Spirakis agirlikli karistirma: sirali rulet cekilisiyle
            // (agirlikla orantili, iadesiz secim) ayni dagilimi O(k log k)'da verir;
            // eski O(k^2) rulet, hub'lu aglarda buyuyen alt-graflarda saatler aliyordu
            List<KeyValuePair<double, Node>> keyed = new List<KeyValuePair<double, Node>>();
            for (int i = 0; i < graph.Count; i++)
            {
                Node n = (Node)graph[i];
                double w = weightOf(n);
                double key = w <= 0 ? 0 : Math.Pow(rnd.NextDouble(), 1.0 / w);
                keyed.Add(new KeyValuePair<double, Node>(key, n));
            }
            keyed.Sort(delegate(KeyValuePair<double, Node> a, KeyValuePair<double, Node> b)
            {
                return b.Key.CompareTo(a.Key);
            });
            ArrayList sorted = new ArrayList();
            for (int i = 0; i < keyed.Count; i++)
            {
                sorted.Add(keyed[i].Value);
            }
            return sorted;
        }
        public ArrayList sortWithPA2(ArrayList graph, double Pp, double Pd)
        {
            double selected;
            ArrayList sorted = new ArrayList();
            graph = sort(graph, Pp, Pd);
            double totalPop = 0;
            double prev = 0;
            for (int i = 0; i < graph.Count; i++)
            {
                totalPop += ((Node)graph[i]).Item.Popularity  ;
            }
            do
            {

                selected = rnd.Next(0, (int)(totalPop + 1));
                prev = 0;
                for (int i = 0; i < graph.Count; i++)
                {
                    if (selected <= prev + ((Node)graph[i]).Item.Popularity  )
                    {
                        sorted.Add((Node)graph[i]);
                        totalPop -= (((Node)graph[i]).Item.Popularity  );
                        graph.RemoveAt(i);
                        break;
                    }
                    prev += ((Node)graph[i]).Item.Popularity  ;
                }
            } while (graph.Count > 1);

            //Console.WriteLine("here");
            //if(graph.Count>0) sorted.Add((Node)graph[0]);
            //for (int i = 0; i < sorted.Count; i++)
            //{
            //    Console.Write(((Node)sorted[i]).Degree);
            //}
            //Console.WriteLine();

            return sorted;
        }
        public bool randomWalk(int val, int ttl)
        {
            int id;
            Node begin;
            bool flag = false;
            do
            {
                id = rnd.Next(0, N - 1);
                //id = 7;
                Console.WriteLine("id for random walk...:" + id);
                begin = search(id);
            } while (begin == null);
            Node previous = begin;
            Link iterator;
            int randomNeighbour;
            for (int j = 0; j < ttl; j++)
            {
                int counter=0;
                do
                {
                    counter++;
                    randomNeighbour = rnd.Next(0, begin.Degree);
                    iterator = begin.NextLink;
                    for (int i = 0; i < randomNeighbour; i++)
                    {
                        iterator = iterator.NextLink;
                    }
                } while (iterator.Id == previous.NodeID && counter<maxDegree);
                Node temp = iterator.VertexLink;
                if (temp == null)
                    return false;
                if (temp.Item.Values.Contains(val))
                    flag = true;
                previous = begin;
                begin = iterator.VertexLink;
            }
            
            return flag;
        }

        public bool flooding(int val, int ttl)
        {
            int nodeCount = 1;
            bool flag = false;
            ArrayList neighbours = new ArrayList();
            ArrayList newNeighbours = new ArrayList();
            ArrayList visited = new ArrayList();
            //int id = 9;//rnd.Next(0, N - 1);
            int id;
            Node begin;
            do
            {
                id = rnd.Next(0, N - 1);
                //id = 7;
                Console.WriteLine("id for  flooding...:" + id);
                begin = search(id);
            } while (begin == null);
            if (begin.Item.Values.Contains(val))
                flag = true;
            //Node previous = begin;
            Link iterator;
            neighbours.Add(begin);
            newNeighbours.Add(begin);
            newNeighbours.Add(new Node(-1));
            //newNeighbours.Add(new Node(-2));
            //newNeighbours.Add(new Node(-3));
            
            //visited.Add(begin);
            for (int i = 0; i < ttl; i++)
            {
                neighbours = copyList(newNeighbours);
                
                //for (int l = 0; l < neighbours.Count; l++)
                //{
                //    Console.WriteLine(((Node)neighbours[l]).NodeID);
                //}
                newNeighbours.Clear();
                for (int j = 0; j < neighbours.Count; j=j+2)
                {
                    begin = (Node)neighbours[j];

                    iterator = begin.NextLink;
                    while (iterator != null)
                    {
                        if (iterator.Id != ((Node)neighbours[j+1]).NodeID)
                        {
                            nodeCount++;
                            Node temp = iterator.VertexLink;
                            if (temp.Item.Values.Contains(val))
                                flag = true;
                            if (!newNeighbours.Contains(temp))
                            {
                                newNeighbours.Add(temp);
                                newNeighbours.Add(begin);
                                //visited.Add(temp);
                            }
                        }
                        iterator = iterator.NextLink;
                        
                    }
                    //for (int k = 0; k < newNeighbours.Count; k++)
                    //{
                    //    Console.WriteLine("newneighbours of " + ((Node)neighbours[j]).NodeID + "  " + ((Node)newNeighbours[k]).NodeID);
                    //}
                    //Console.WriteLine("done");
                      
                }
                //previous = (Node)visited[i];
   
            }//for times ttl
            Console.WriteLine(nodeCount);
            return flag;
        }

        public Node getRndNode()
        {
            int id;
            Node begin;
            do
            {
                id = rnd.Next(0, N - 1);
                //id = 7;
                //Console.WriteLine("id for normalized flooding...:" + id);
                begin = search(id);
            } while (begin == null);
            return begin;
        }

        // baglanma agirligindaki trend terimi yalniz kalicilik filtresini
        // gecmis (istatistiksel olarak anlamli) trendleri kullanir; boylece
        // gurultu, populerlik-tabanli baglanma yapisini bozamaz
        public double weightOf(Node n)
        {
            double trendTerm = n.Persistent ? n.TrendScore : 0;
            return n.Item.Popularity * Pp + n.Degree * Pd + trendTerm * Pt;
        }

        // normalized flooding ile ayni yayilim kurali (her dugum en fazla minDegree
        // rastgele komsuya iletir, parent haric); buyuk aglarda olcum icin HashSet'li verimli hal.
        // observe=true ise ziyaret edilen dugumler sorguyu trend kestirimine kaydeder.
        public int nfSearch(Node begin, int val, int ttl, bool observe, ref int visitedCount)
        {
            int hits = 0;
            HashSet<Node> visited = new HashSet<Node>();
            List<Node[]> frontier = new List<Node[]>();
            visited.Add(begin);
            if (observe) begin.observeQuery(val);
            if (begin.Item.Values.Contains(val)) hits++;
            frontier.Add(new Node[] { begin, null });
            for (int h = 0; h < ttl && frontier.Count > 0; h++)
            {
                List<Node[]> next = new List<Node[]>();
                for (int f = 0; f < frontier.Count; f++)
                {
                    Node current = frontier[f][0];
                    Node parent = frontier[f][1];
                    List<Node> neighbours = new List<Node>();
                    Link iterator = current.NextLink;
                    while (iterator != null)
                    {
                        if (iterator.VertexLink != null && (parent == null || iterator.Id != parent.NodeID))
                            neighbours.Add(iterator.VertexLink);
                        iterator = iterator.NextLink;
                    }
                    int forward = Math.Min(minDegree, neighbours.Count);
                    for (int k = 0; k < forward; k++)
                    {
                        int pick = rnd.Next(k, neighbours.Count);
                        Node swap = neighbours[pick]; neighbours[pick] = neighbours[k]; neighbours[k] = swap;
                        Node candidate = neighbours[k];
                        if (visited.Contains(candidate))
                            continue;
                        visited.Add(candidate);
                        if (observe) candidate.observeQuery(val);
                        if (candidate.Item.Values.Contains(val)) hits++;
                        next.Add(new Node[] { candidate, current });
                    }
                }
                frontier = next;
            }
            visitedCount += visited.Count;
            return hits;
        }

        public Node getHolder(int val)
        {
            Node iterator = nodeHead;
            while (iterator != null)
            {
                if (iterator.Item.Values.Contains(val))
                    return iterator;
                iterator = iterator.NextNode;
            }
            return null;
        }

        private void updateTrends()
        {
            lastWindowEntK = 0;
            lastWindowEntC = 0;
            lastWindowEntL = 0;
            Node iterator = nodeHead;
            while (iterator != null)
            {
                iterator.updateTrendWindow(SigmaK, ShareFloor, PersistWindows, CusumTheta, FixedThreshold, HotShare, Rules);
                // ag genelinde giris atfi: bu pencerede adaptasyona giren
                // dugumlerin hangi kuralla girdigi sayilir (false-alarm dahil)
                switch (iterator.WindowEntryRule)
                {
                    case 1: lastWindowEntK++; break;
                    case 2: lastWindowEntC++; break;
                    case 3: lastWindowEntL++; break;
                }
                iterator = iterator.NextNode;
            }
            // oracle: tutucu, rampa basladigi andan itibaren trend modunda tutulur
            if (OracleDetector && Trend != null && simTime >= Trend.TStarts[0])
            {
                Node holder = getHolder(Trend.TbpVal);
                if (holder != null) holder.forceTrending();
            }
        }

        // Cooper square-root-construct penceresi: her dugum kumulatif
        // eslesen/gorulen sayaclarini gunceller, ardindan hedef derecesine
        // dogru rastgele eslerle link ekler veya kendi linklerinden siler.
        // Eklenen linkler rewire kolonlarina yazilir (maliyet karsilastirmasi)
        private void cooperWindow()
        {
            lastWindowRewireNodes = 0;
            lastWindowRewireJoins = 0;
            Node iterator = nodeHead;
            while (iterator != null)
            {
                iterator.updateCooperWindow(CooperMu);
                iterator = iterator.NextNode;
            }
            iterator = nodeHead;
            while (iterator != null)
            {
                int target = iterator.cooperTargetDegree(CooperDmax, minDegree);
                if (target > iterator.Degree)
                {
                    int before = iterator.Degree;
                    int add = target - iterator.Degree;
                    for (int a = 0; a < add; a++) cooperAddLink(iterator);
                    if (iterator.Degree > before)
                    {
                        lastWindowRewireNodes++;
                        lastWindowRewireJoins += iterator.Degree - before;
                    }
                }
                else if (target < iterator.Degree)
                {
                    int drop = iterator.Degree - target;
                    for (int d = 0; d < drop; d++) cooperDropLink(iterator);
                }
                iterator = iterator.NextNode;
            }
        }

        // rastgele bir ese cift yonlu link ekler (Cooper'in hostcatcher'dan
        // rastgele es secimi); mevcut linkler ve kendisi atlanir
        private void cooperAddLink(Node n)
        {
            for (int tries = 0; tries < 5; tries++)
            {
                Node peer = getRndNode();
                if (peer == null || peer.NodeID == n.NodeID || doesLinkExist(n, peer.NodeID))
                    continue;
                if (n.NextLink == null)
                    n.NextLink = createLink(peer.NodeID);
                else
                    findLastLink(n.NextLink).NextLink = createLink(peer.NodeID);
                n.Degree++;
                Link last = findLastLink(peer.NextLink);
                if (last != null)
                    last.NextLink = createLink(n.NodeID);
                else
                    peer.NextLink = createLink(n.NodeID);
                peer.Degree++;
                return;
            }
        }

        // kendi linklerinden rastgele birini cift yonlu sokar; agin bag-
        // lantililigini korumak icin karsi ucu join minimumunun altina
        // dusurecek silimler atlanir (taban cizgisi lehine muhafazakar secim)
        private void cooperDropLink(Node n)
        {
            if (n.Degree <= minDegree || n.NextLink == null) return;
            int idx = rnd.Next(0, n.Degree);
            Link it = n.NextLink;
            Link prev = null;
            for (int i = 0; i < idx && it.NextLink != null; i++)
            {
                prev = it;
                it = it.NextLink;
            }
            Node peer = it.VertexLink;
            if (peer == null || peer.Degree <= minDegree) return;
            if (prev == null) n.NextLink = it.NextLink; else prev.NextLink = it.NextLink;
            n.Degree--;
            Link pit = peer.NextLink;
            Link pprev = null;
            while (pit != null && pit.Id != n.NodeID)
            {
                pprev = pit;
                pit = pit.NextLink;
            }
            if (pit != null)
            {
                if (pprev == null) peer.NextLink = pit.NextLink; else pprev.NextLink = pit.NextLink;
                peer.Degree--;
            }
        }

        // istekci-tarafli reaktif kisayol: basarili TBP sorgusunun ardindan
        // istekci ile tutucu arasina cift yonlu link eklenir (mevcutsa atlanir)
        private void reactiveLink(Node requester, int val)
        {
            Node holder = getHolder(val);
            if (holder == null || requester == null || holder.NodeID == requester.NodeID)
                return;
            if (doesLinkExist(requester, holder.NodeID))
                return;
            if (requester.NextLink == null)
                requester.NextLink = createLink(holder.NodeID);
            else
                findLastLink(requester.NextLink).NextLink = createLink(holder.NodeID);
            requester.Degree++;
            Link last = findLastLink(holder.NextLink);
            if (last != null)
                last.NextLink = createLink(requester.NodeID);
            else
                holder.NextLink = createLink(requester.NodeID);
            holder.Degree++;
            reactiveNodesWindow++;
            reactiveLinksWindow++;
        }

        // kontrol taban cizgisi: ayni join butcesi rastgele dugumlere verilir
        private void rewireRandom()
        {
            lastWindowRewireNodes = 0;
            lastWindowRewireJoins = 0;
            for (int i = 0; i < RandomRewireNodes; i++)
            {
                Node candidate = getRndNode();
                int before = candidate.Degree;
                for (int j = 0; j < RewireJoinsPerNode; j++)
                    join(candidate);
                lastWindowRewireNodes++;
                lastWindowRewireJoins += candidate.Degree - before;
            }
        }

        // kalicilik filtresini gecen dugumler, mevcut join mekanizmasiyla ek
        // baglanti kurarak agda daha merkezi konuma cekilir. Kural tamamen
        // yereldir: her dugum yalniz kendi trend durumuna bakar; kuresel
        // siralama veya secim yoktur. Pencere basina fiilen kac dugumun kac
        // join yaptigi olculup CSV'ye yazilir.
        private void rewireTrending()
        {
            lastWindowRewireNodes = 0;
            lastWindowRewireJoins = 0;
            ArrayList trending = new ArrayList();
            Node iterator = nodeHead;
            while (iterator != null)
            {
                if (iterator.Persistent)
                    trending.Add(iterator);
                iterator = iterator.NextNode;
            }
            for (int i = 0; i < trending.Count; i++)
            {
                Node candidate = (Node)trending[i];
                int before = candidate.Degree;
                for (int j = 0; j < RewireJoinsPerNode; j++)
                    join(candidate);
                lastWindowRewireNodes++;
                lastWindowRewireJoins += candidate.Degree - before;
            }
        }

        // toplam kenar sayisi (derece toplami / 2); es maliyet karsilastirmasi icin
        public long countEdges()
        {
            long total = 0;
            Node iterator = nodeHead;
            while (iterator != null)
            {
                total += iterator.Degree;
                iterator = iterator.NextNode;
            }
            return total / 2;
        }

        // son topolojinin derece histogrami (CCDF figuru icin)
        public void writeDegreeHistogram(string path)
        {
            Dictionary<int, int> hist = new Dictionary<int, int>();
            Node iterator = nodeHead;
            while (iterator != null)
            {
                if (!hist.ContainsKey(iterator.Degree)) hist[iterator.Degree] = 0;
                hist[iterator.Degree]++;
                iterator = iterator.NextNode;
            }
            List<int> keys = new List<int>(hist.Keys);
            keys.Sort();
            using (System.IO.StreamWriter w = new System.IO.StreamWriter(path))
            {
                w.WriteLine("degree,count");
                for (int i = 0; i < keys.Count; i++)
                    w.WriteLine(keys[i] + "," + hist[keys[i]]);
            }
        }

        private void logMetrics()
        {
            int tbpVal = Trend != null ? Trend.TbpVal : 1000;
            Node holder = getHolder(tbpVal);
            int probes = 20;
            // TTL=12 ag yarisini gezip metrigi doyurdugundan, doymayan dusuk
            // TTL'lerde de olculur; basari farki ancak boyle yorumlanabilir
            int[] ttls = new int[] { 4, 6, 8, 12 };
            int[] success = new int[ttls.Length];
            int[] visitedTotal = new int[ttls.Length];
            for (int t = 0; t < ttls.Length; t++)
            {
                for (int k = 0; k < probes; k++)
                {
                    int visitedCount = 0;
                    if (nfSearch(getRndNode(), tbpVal, ttls[t], false, ref visitedCount) > 0)
                        success[t]++;
                    visitedTotal[t] += visitedCount;
                }
            }
            // zarar-yok olcumu: siradan (Zipf) iceriklere TTL=8 probe; trend
            // katmaninin arka plan aramalarini bozmadigi bu kolonlarla gosterilir
            int bgProbes = 20;
            int bgSuccess = 0;
            int bgHits = 0;
            int bgVisited = 0;
            for (int k = 0; k < bgProbes; k++)
            {
                // is yuku ile ayni dagilim: populer item daha sik hedeflenir
                int bgVal = 101 - (int)bgZipf.getZipfValue(0.3, 100);
                int visitedCount = 0;
                int h = nfSearch(getRndNode(), bgVal, 8, false, ref visitedCount);
                if (h > 0) bgSuccess++;
                bgHits += h;
                bgVisited += visitedCount;
            }
            int holderDegree = holder == null ? -1 : holder.Degree;
            double holderTrend = holder == null ? 0 : holder.TrendScore;
            int holderTrending = holder != null && holder.Persistent ? 1 : 0;
            // coklu TBP kosusunda 1..K-1 icerikleri icin derece/basari/trend durumu
            StringBuilder extra = new StringBuilder();
            if (Trend != null && Trend.TbpVals.Length > 1)
            {
                for (int k = 1; k < Trend.TbpVals.Length; k++)
                {
                    Node h = getHolder(Trend.TbpVals[k]);
                    int succ = 0;
                    for (int p = 0; p < probes; p++)
                    {
                        int vc = 0;
                        if (nfSearch(getRndNode(), Trend.TbpVals[k], 8, false, ref vc) > 0)
                            succ++;
                    }
                    extra.Append(",").Append(h == null ? -1 : h.Degree)
                         .Append(",").Append(succ)
                         .Append(",").Append(h != null && h.Persistent ? 1 : 0);
                }
            }
            // CSV her zaman nokta ondalikli yazilmali (locale'den bagimsiz)
            StringBuilder line = new StringBuilder();
            line.Append(simTime).Append(",").Append(N).Append(",").Append(cnt).Append(",").Append(holderDegree);
            for (int t = 0; t < ttls.Length; t++)
                line.Append(",").Append(success[t]).Append(",").Append(visitedTotal[t] / probes);
            line.Append(",").Append(holderTrend.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            line.Append(",").Append(bgSuccess).Append(",").Append(bgHits).Append(",").Append(bgVisited / bgProbes);
            line.Append(",").Append(lastWindowRewireNodes).Append(",").Append(lastWindowRewireJoins).Append(",").Append(countEdges()).Append(",").Append(holderTrending);
            // giris atfi kolonlari: bu pencerede kural basina giris sayisi ve
            // tutucunun son girisini yapan kural (0 yok, 1 k-sigma, 2 CUSUM, 3 seviye)
            line.Append(",").Append(lastWindowEntK).Append(",").Append(lastWindowEntC).Append(",").Append(lastWindowEntL);
            line.Append(",").Append(holder == null ? 0 : holder.LastEntryRule);
            line.Append(extra);
            MetricsWriter.WriteLine(line.ToString());
            MetricsWriter.Flush();
            Console.WriteLine("t=" + simTime + "  N=" + N + "  holderDeg=" + holderDegree +
                "  probe(ttl4/6/8/12)=" + success[0] + "/" + success[1] + "/" + success[2] + "/" + success[3] + " of " + probes);
        }

        public int normalizedFlooding(Node begin, int val, int ttl, ref int nodeCount)
        {
            //int nodeCount = 1;
            int hits = 0;
            ArrayList neighbours = new ArrayList();
            ArrayList newNeighbours = new ArrayList();
            ArrayList visited = new ArrayList();
            //int id;
            //Node begin;
            //do
            //{
            //    id = rnd.Next(0, N - 1);
            //    //id = 7;
            //    //Console.WriteLine("id for normalized flooding...:" + id);
            //    begin = search(id);
            //} while (begin == null);
            if (begin.Item.Values.Contains(val))
            {
                hits++;
            }
            visited.Add(begin);
            //Node previous = begin;
            Link iterator;
            neighbours.Add(begin);
            newNeighbours.Add(begin);
            newNeighbours.Add(new Node(-1));
             
            //visited.Add(begin);
            for (int i = 0; i < ttl; i++)
            {
                neighbours = copyList(newNeighbours);
                //for (int l = 0; l < neighbours.Count; l++)
                //{
                //    Console.WriteLine(((Node)neighbours[l]).NodeID);
                //}
                newNeighbours.Clear();
                for (int j = 0; j < neighbours.Count; j=j+2)
                {
                    ArrayList toBeSent = new ArrayList();
                    begin = (Node)neighbours[j];
                    toBeSent = selectNeighbours(begin, (Node)neighbours[j+1]);
                    //nodeCount += toBeSent.Count; 
                    iterator = begin.NextLink;
                    int counter = 0;
                    while (iterator != null)
                    {
                        
                        if (toBeSent.Contains(counter)&&  iterator.Id != ((Node)neighbours[j+1]).NodeID)
                        {
                            
                            Node temp = iterator.VertexLink;
                            
                            if (!newNeighbours.Contains(temp))
                            {
                                newNeighbours.Add(temp);
                                newNeighbours.Add(begin);
                                //visited.Add(temp);
                                if (!visited.Contains(temp))
                                {
                                    visited.Add(temp);
                                    if (temp.Item.Values.Contains(val))
                                    {
                                        hits++;
                                    }
                                }
                            }
                            
                            
                        }
                        counter++;
                        iterator = iterator.NextLink;
                        
                    }
                    //for (int k = 0; k < newNeighbours.Count; k++)
                    //{
                    //    Console.WriteLine("newneighbours of " + ((Node)neighbours[j]).NodeID + "  " + ((Node)newNeighbours[k]).NodeID);
                    //}
                    //Console.WriteLine("done");
                      
                }
                //previous = (Node)visited[i];
   
            }//for times ttl
            //Console.WriteLine("Node Count..:"+nodeCount);
            nodeCount+=  visited.Count;
            return hits;
        }
        //selects neighbour for a normalized flooding
        private ArrayList selectNeighbours(Node current, Node parent)
        { 
            ArrayList toBeSent = new ArrayList();
            int degree = current.Degree;
            int starting = 0;
            if (parent.NodeID != -1)
            {
                starting = 1;
                Link iterator = current.NextLink;
                Link prev = iterator;
                if (iterator.Id != parent.NodeID)
                {
                    current.NextLink = createLink(parent.NodeID);
                    current.NextLink.NextLink = iterator;
                    while (iterator.Id != parent.NodeID)
                    {
                        prev = iterator;
                        iterator = iterator.NextLink;
                    }
                    prev.NextLink = iterator.NextLink;
                }
            }
            
            if (degree > minDegree)//means we have to select minimumDegree neigbours randomly
            {
                while (toBeSent.Count < minDegree)
                {
                    int selectedNeighbour = rnd.Next(starting, degree);
                    if (!toBeSent.Contains(selectedNeighbour))
                        toBeSent.Add(selectedNeighbour);
                }
            }
            else
            {
               
                for (int k =starting; k < degree; k++)
                {
                    toBeSent.Add(k);
                }
            }
            return toBeSent;
        }


        //copies an ArraYlist into another one
        private ArrayList copyList(ArrayList list)
        {
            ArrayList copy = new ArrayList();
            for (int i = 0; i < list.Count; i++)
            {
                copy.Add(list[i]);
            }
            return copy;
        }

        private ArrayList copyListSingles(ArrayList list)
        {
             
            ArrayList copy = new ArrayList();
            for (int i = 0; i < list.Count; i=i+2)
            {
                copy.Add(list[i]);
            }

            return copy;
        }
        public void countDegrees()
        {
            int[] degrees = new int[maxDegree+1];
            Node iterator = nodeHead;
            //Console.WriteLine(iterator.Degree);
            while (iterator != null)
            {
                if (iterator.Degree>maxDegree)
                    Console.WriteLine("highhhhhhhhhhhhhhhhhhhhhhhhhhhh"+iterator.NodeID);
                degrees[iterator.Degree]++;
                iterator = iterator.NextNode;
            }
            int total = 0;
            for (int i = 0; i < degrees.Length; i++)
            {
                if (degrees[i]!=0)
                Console.WriteLine("degree..:"+i+ " number of nodes...:   "+ degrees[i]);
                total += degrees[i];
            }
            Console.WriteLine(total);
        }
    }
}
