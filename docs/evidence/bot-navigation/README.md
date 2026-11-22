# BOT-02 doğrulaması

[Issue #47](https://github.com/Lysnder/RobotSavaslari/issues/47) için ana senaryo `MainArena`. Rota planlayıcı açık yolda arama yapmaz. Yol kapandığında 0,5 m grid üzerinde robotun ayak izini hesaba katar ve hareket etmeden önce bir sonraki segmenti güncel collider’larla kontrol eder. Motor ve konveyör bileşenleri fiziksel hareketi yönetmeye devam eder. Botun hedefi ve saldırı ölçümleri rakibe bağlı kalır.

`BotNavigationTests` açık takip, fiziksel detur, dar geçit, erişilemeyen hedef, yükselen bariyer, dolan sorgu tamponu ve üç konveyör yönünü kapsar. İki botun ölçümü 120 ısınma adımı ve 500 örnek kullanır. Sahnedeki toplu navigasyon maliyetini incelemek için Unity Profiler’da `RobotBot.Navigation` örneklerini fizik adımı başına toplayın. Fizik sorguları bot başına 64, genişletilen düğümler 128, saklanan ara noktalar 128 ile sınırlıdır.

Otomatik maç matrisi için `MainArena` Windows Player’ını `-bot-navigation-matrix -bot-trace-file <tsv-yolu> -logFile <log-yolu>` ile başlatın. Her süreç Open ve Tactical düzenlerinde Aggressive ve Counter botları Easy ve Hard zorluklarıyla, 740, 741 ve 123 tohumlarında ikişer maç çalıştırır. Bu 48 maçı ikinci bir süreçte tekrar edin. Logda 48 tamamlanmış maç beklenir. TSV, saldırı niyeti, başlayan saldırı, isabet, gerçek hasar ve navigasyon sayaçlarını ayrı alanlara yazar.

Matris otomatik giriş ve hızlandırılmış oyun zamanı kullanır. Normal Windows kabulünü komut satırı bayrakları olmadan, grafik açık ve gerçek klavye ile yürütün. [Kullanıcı verisi aracını](../../windows-acceptance/Preserve-UserData.ps1) kabul öncesinde yedek almak, kabul sonrasında da geri yüklenen ayar ve kayıtları doğrulamak için kullanın. Paylaşılan kanıta kaynak commit’i, Unity sürümünü, test sayıları ve durumlarını yazın. Ham logları ve kişisel kayıt yedeklerini yerelde saklayın.

22 Kasım 2026 doğrulamasında Unity 6000.4.4f1, Windows 11 ve ayrı kullanıcı verisi alanı olan bir proje kopyası kullanıldı. EditMode 205/205 ve odaklı bot PlayMode koşusu 26/26 geçti. Tam PlayMode koşusu 420 saniyelik üst sınıra ulaştı ve XML sonuç üretmedi. Son loglar tekrarlanan maç sonu inceleme testine işaret ediyor. Ham raporlar yerel `RobotArena/Logs/issue47-validation` dizininde.

İki botlu ölçüm Ryzen 5 5600X, 24.499 MB RAM ve 640×480 başsız Editor’da, iki botun tüm `Tick` çağrıları üzerinden alındı. p50 0,0257 ms, p95 0,1255 ms, maksimum 0,4244 ms ve maksimum yönetilen tahsis 0 B/adım ölçüldü.
