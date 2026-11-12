using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// V2 story: who the shooter is, what each contract uncovers and where it leads. Three chapters (one per map), a
    /// line of intelligence after every contract, the handler KONTROL whose own name starts showing up in the files,
    /// and three decisions (the chemist, Baran's safe, the disk) that change how it ends.
    /// </summary>
    public static class Story
    {
        public class Chapter { public int number; public string region, title, text; }
        public class Choice { public string key, question, a, aText, b, bText; }

        public static readonly Chapter[] Chapters =
        {
            new Chapter { number = 1, region = "harbor", title = "LİMAN — KRAL'IN AĞI",
                text = "Adın yok. Ordu seni üç yıl önce ölü yazdı. KONTROL seni bir askeri hastanede buldu ve tek bir teklif yaptı: " +
                       "limandaki kaçakçılık ağını çökert, geçmişin silinsin.\n\nHer sözleşme bir isim. Her isim, bir sonrakine giden yol." },
            new Chapter { number = 2, region = "city", title = "ŞEHİR — BARAN HOLDİNG",
                text = "Liman çöktü ama para akmaya devam ediyor. Viktor Kral'ın telefonundaki son numara Kordon Plaza'ya çıkıyor: Baran Holding.\n\n" +
                       "Bu kez hedefler takım elbiseli. Kalabalığın içinde, cam duvarların ardında, polislerin yanında." },
            new Chapter { number = 3, region = "mountain", title = "KARTAL TEPE — KURUL",
                text = "Baran'ın kasasından çıkan dosyalar beş isim veriyor: Kurul. Yıllardır Baran'ı besleyen ve koruyan beş kişi, yılbaşı haftasını " +
                       "her yıl Kartal Tepe'de geçiriyor.\n\nDosyaların arasında bir imza daha var. Tanıdık bir imza." },
        };

        /// <summary>What the contract uncovered (shown after it is done) — and where it leads.</summary>
        static readonly Dictionary<string, string> beats = new Dictionary<string, string>
        {
            { "m1", "Selim Kaya'nın cebinden çıkan rüşvet makbuzlarında tek bir imza var: liman yönetiminden C. Aydın." },
            { "m2", "Can Aydın'ın sildiği kayıtlar kurtarıldı. Gelecek sevkiyatın alıcısı: Kuzgun lakaplı Tarık Bal." },
            { "m3", "Kuzgun'un arabasındaki çanta boş değil: depoları koruyan paralı askerlerin maaş listesi. Başlarında Rico Vargas." },
            { "m4", "Rico'nun telsizinde bir uyarı: \"Depoda köstebek var.\" Örgüt de kendi içinde birini arıyor." },
            { "m5", "Köstebek Kerim'in sattığı bilgi bir teslimata işaret ediyor: Tilki çalınan belgeleri rıhtımdan kaçıracak." },
            { "m6", "Tilki'nin belgeleri bir gemi manifestosu: Meridian Star'ın ambarında örgütün silah yükü var." },
            { "m7", "Kaptan Demir'in seyir defterinde tek bir isim tekrar ediyor: yükün sahibi Viktor Kral." },
            { "m8", "Kral düştü. Telefonundaki son arama Kordon Plaza'ya: Baran Holding. Ama örgüt hâlâ nefes alıyor — ve bir tanığı susturmak istiyor." },
            { "m9", "Ayten Şahin ifadesini verdi. Örgütün para kuryesi Emre Doğan şehirden kaçmaya hazırlanıyor." },
            { "m10", "Kuryenin çantasındaki hesaplar bir kimyagere ödeme gösteriyor: Dr. Selin Aras." },
            { "m11", "Kartal karanlıkta kaldı. Silah sevkiyatının parası bir laboratuvara gidiyor: Meridian Star'ın ambarı." },
            { "m12", "Dr. Aras konuştu: laboratuvarın asıl sahibi Baran Holding. Liman bitti; sırada şehir var." },
            { "m13", "Defne Aksoy'un ekranında son işlem: Baran'ın rüşvet defteri Postacı'ya teslim edildi." },
            { "m14", "Rüşvet defteri ele geçti. Sayfalardan biri gemilerin sahibine ayrılmış: Cevdet Aydemir." },
            { "m15", "Aydemir'in balkonda bıraktığı telefon: Baran, Başsavcı Nermin Arslan'ı susturmak için iki kişi tutmuş." },
            { "m16", "Savcı konuşmasını bitirdi. Tetikçinin cebinden bir not çıktı: \"Muhasebeci konuşmadan işini bitir.\" Baran'ın muhasebecisi her şeyi biliyor — onu canlı istiyoruz." },
            { "m17", "Muhasebeci Koç'un hesapları gazeteci Selin Ay'ın cinayetine ödeme gösteriyor. Tetikçi Çakal, dosyayı kapatan bir komiser." },
            { "m18", "Çakal'ın motosikletinde Baran'ın güvenlik şefinin numarası: Tolga Demirel. Bize karşı her hamleyi o planlamış." },
            { "m19", "Demirel'in tabletinde senin kullandığın bütün atış noktaları var — hepsi işten önce kaydedilmiş. Bunları ona kim verdi? Dosyanın üstünde tek harf: \"K.\"" },
            { "m20", "Baran'ın son mesajı yarım kaldı: \"KONTROL'e söyle, anlaşma hâlâ geçerli…\" Dosyalar Kartal Tepe'yi ve beş ismi gösteriyor: Kurul." },
            { "m21", "Banker Kurt'un tableti Kurul'un ödeme defterine bağlanıyor. Her ödemenin altında aynı onay kodu: KONTROL." },
            { "m22", "Hakim Oral'ın kasasında Baran davalarının kararları — duruşmalardan önce yazılmış. Biri de senin eski birliğine ait." },
            { "m23", "Müteahhit Tan'ın telefonunda Kurul'un kuryesinin rotası: şifreli diskle vadiyi geçecek." },
            { "m24", "Kuryenin diski açıldı: Kurul'un bütün ödemeleri — ve KONTROL'e yapılan aylık ödemeler. Bize iş verenler, Kurul'un kendi temizlikçileri." },
            { "m25", "Bülent Er'in konvoyundaki evraklar: Kurul'a karşı açılan her soruşturmayı KONTROL'ün talimatıyla kapatmış." },
            { "m26", "Elif Demir yaşıyor ve yarın konuşacak. Tetikçileri tutan adamın adı onun dosyasında: Orhan Sezer, Kurul'un temizlikçisi." },
            { "m27", "Sezer'in telefonundaki son arama Patron'dan değil. KONTROL'den. Senden sonra temizlenecek kişi sendin." },
            { "m28", "Kurul bitti. Disk hâlâ sende." },
        };

        /// <summary>Decisions at the end of three contracts.</summary>
        static readonly Dictionary<string, Choice> choices = new Dictionary<string, Choice>
        {
            { "m12", new Choice { key = "chemist", question = "Dr. Aras baygın, tekne bekliyor. Onu kime teslim edeceksin?",
                a = "KONTROL'E", aText = "Kimyager KONTROL'ün hücresine gitti. Ondan bir daha haber alınmadı.",
                b = "SAVCILIĞA", bText = "Kimyager savcılığa bırakıldı. İfadesi Baran'a açılan davanın ilk delili oldu." } },
            { "m20", new Choice { key = "safe", question = "Baran'ın kasası açık: dosyalar ve iki milyon avro. Ne yapacaksın?",
                a = "PARAYI AL", aText = "Parayı aldın (+20.000). KONTROL'e sadece dosyaları gönderdin.",
                b = "DOKUNMA", bText = "Parayı bıraktın. Dosyaların kopyasını Başsavcı Arslan'a yolladın." } },
            { "m28", new Choice { key = "disk", question = "Kurul'un bütün ödemeleri ve KONTROL'ün imzası bu diskte. Kime vereceksin?",
                a = "KONTROL'E", aText = "Diski KONTROL'e verdin. Ertesi sabah Kurul'un boş koltukları dolmaya başladı — yeni isimlerle. Sözleşme tamam; sen yine kimsesin.",
                b = "SAVCIYA", bText = "Diski Başsavcı Nermin Arslan'a bıraktın. Bir hafta sonra KONTROL'ün kapısına polis dayandı. Adın yine yok — ama bu sefer kendi seçimin." } },
        };

        public static Chapter ChapterOf(MissionDef m)
        {
            if (m == null) return null;
            foreach (var c in Chapters) if (c.region == m.region) return c;
            return null;
        }

        /// <summary>The chapter card is shown before the first contract of each map.</summary>
        public static bool OpensChapter(MissionDef m)
        {
            if (m == null) return false;
            var list = MissionDefs.InLocation(m.region);
            return list.Count > 0 && list[0] == m;
        }

        public static string Beat(MissionDef m)
        {
            if (m == null || !beats.TryGetValue(m.id, out var b)) return null;
            // the decisions taken earlier colour some of the lines
            if (m.id == "m16" && Decision("chemist") == 2) b += " Kimyagerin ifadesi davayı ayakta tutuyor.";
            if (m.id == "m21" && Decision("safe") == 1) b += " Baran'ın parasını aldığını KONTROL biliyor.";
            return b;
        }

        public static Choice ChoiceFor(MissionDef m) => m != null && choices.TryGetValue(m.id, out var c) ? c : null;

        /// <summary>0 = not taken yet, 1 = the first option, 2 = the second.</summary>
        public static int Decision(string key) => PlayerPrefs.GetInt(Save.K + "story_" + key, 0);

        public static void Decide(MissionDef m, int option)
        {
            var c = ChoiceFor(m);
            if (c == null) return;
            PlayerPrefs.SetInt(Save.K + "story_" + c.key, option);
            PlayerPrefs.Save();
            if (c.key == "safe" && option == 1) Progress.AddCash(20000, "story");
            Analytics.Event("story_choice", "key", c.key, "option", option);
        }

        /// <summary>Contracts done so far with their lines (the case board in the menu).</summary>
        public static List<KeyValuePair<MissionDef, string>> Board()
        {
            var res = new List<KeyValuePair<MissionDef, string>>();
            foreach (var m in MissionDefs.All)
                if (Save.Completed(m.id) && beats.ContainsKey(m.id)) res.Add(new KeyValuePair<MissionDef, string>(m, Beat(m)));
            return res;
        }
    }
}
