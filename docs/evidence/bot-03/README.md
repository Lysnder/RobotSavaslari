# BOT-03 · Arena riskini okuyan rakipler

Görev [#58](https://github.com/Lysnder/RobotSavaslari/issues/58), [BOT-02 görevindeki](https://github.com/Lysnder/RobotSavaslari/issues/47) mevcut [bot rotası](https://github.com/Lysnder/RobotSavaslari/pull/53) ve [arena tehlikeleri](https://github.com/Lysnder/RobotSavaslari/pull/49) üzerine kuruldu. İlk karşılaştırma, `test` tabanı `6200ddcaff7bda984a88d343db1446c1a6ce57c1` üstünde yalnızca ölçüm kancası ve karşılaşma fixture’ı eklenen `d8d5685d29ec699488845390915bbef5ec52d5d3` ile yapıldı.

## Oyunda gözlenen davranış

MainArena menüsündeki rakip ve zorluk seçimi geçerlidir. Bot fiziksel ayak izini rotaya katar. Warning/Active durumunu algıladığında Pulse karesini veya Sweep şeridini mevcut gezilebilir alandan geçici olarak çıkarır. Hedef riskin içindeyse güvenli mesafede bekler. Tehlike robotun altında belirirse işaretin dört çıkış kenarı arasından diğer riskleri, rakibi ve engelleri aşmadan erişebileceği en yakınını arar. Doğrudan geçiş riskliyse aynı sınırlı A* aramasıyla başka bir geçiş arar. Recovery/Idle gözlemi yeniden yaklaşmaya izin verir. Hedef hareketleri, bariyerler, konveyörler ve sıkışma denetimleri mevcut fizik yolunu kullanır.

Counter rotasına gövde payına ek olarak 0,25 m, Aggressive 0,05 m güvenlik aralığı ekler. Zorluk, mevcut karar örnekleme aralığını ve kaçırılan gözlemleri belirler. Hasar, azami can ve motor parametreleri robotun mevcut değerleridir. Ayarlarda duraklatma, yeni raund ve yeniden doğma bir önceki risk gözlemini geçersiz kılar. Unity Inspector’da botun `Draw Navigation Gizmos` seçeneği açıldığında hedefi, ara segmenti, karar nedenini, plan ve sorgu sayaçlarını Scene görünümünde izleyebilirsiniz.

## Ölçüm düzeni

`BotRiskMatrixTests.SixteenSeededArenaDuelsFinishWithinBudget` dört arenada iki kişiliği iki zorlukta çalıştırır. Karar tohumu 740, tek maç süresi 18 oyun saniyesidir. Open ve Tactical kendi başlangıç noktalarını kullanır. Pulse ve Sweep'te ilk canlı uyarı geldiğinde iki robot işaretli hücrenin iki yanına, aralarında 7 m olacak şekilde yerleştirilir. Oyuncu hareket tuşu kullanmaz, her iki gövde normal çarpışma, silah ve hasar kurallarıyla çalışır. Matris geçici geçmiş ve tekrar klasörü kullanır.

CSV dosyalarında her satır tek bir fiziksel maçı gösterir. `hazardHits/hazardDamage` botun aldığı çevresel isabet ve can kaybıdır. `firstWeaponHitSeconds` parkurun başlangıcından rakibe ilk gerçek yakın dövüş isabetine kadar geçen süredir. –1 hiç isabet olmadığını gösterir. `pathMetres` gövdenin katettiği mesafedir. `recoveries` botun sıkışma kurtarma sayacındaki artıştır. `maxQueries` ve `maxExpansions` fizik adımı başına tepe değerlerdir. `p95Milliseconds` ilk sekiz adım sonrasında rakibin gerçek karar adımlarındaki sürelerin 95. yüzdeliğidir. `maxAllocatedBytes` aynı adımların en yüksek yönetilen iş parçacığı tahsisidir. Fizik motoru, görüntü ve maç sistemleri bu iki karar ölçümünün dışındadır.

Unity Test Runner’da PlayMode altında `RobotSavaslari.Tests.BotRiskMatrixTests`, `BotNavigationTests` ve `BotControllerTests` seçilebilir. Matris çıktısındaki her maç satırı `BOT03,` ile başlar. Önceki sürümün 16 maçı [baseline.csv](baseline.csv) içinde.


## 24 Kasım 2026 doğrulaması

Oynatılan kaynak `8b196d6f3b010f1cc62344533eff171fa58fab09` ve Unity sürümü `6000.4.4f1 (360f97ecca93)` idi. Doğrulama kopyasının kayıt kimliği `ArenaValidation24K/BotMatrix` olarak ayrıldı. Tablodaki süre sütunu 16 maçın ayrı ayrı hesaplanan p95 değerlerinin en büyüğüdür. Duvar saati ölçümleri makinedeki diğer işlemlerden etkilenebilir. Aynı karar tohumu farklı fizik koşularında birebir konum ve süre garantisi vermez.

| Koşu | Geçerli bitiş | Çevresel isabet / hasar | Sıkışma kurtarması | En çok plan / maç | En çok sorgu / adım | En çok düğüm / adım | En yüksek p95 | En yüksek GC / adım |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Önce, Windows Editor | 16/16 | 24 / 240 | 10 | 10 | 62 | 56 | 0,1068 ms | 0 B |
| Sonra, Windows Editor | 16/16 | 9 / 90 | 0 | 12 | 62 | 50 | 0,1324 ms | 0 B |
| Sonra, Windows x64 Player | 16/16 | 8 / 80 | 0 | 12 | 62 | 49 | 0,1194 ms | 0 B |

Her maçta botun silahı gerçek hedefe isabet etti. Önceki sürümde 5 maç, son Editor ve Windows koşularında üçer maç süre dolumuyla sonuçlandı. PulseArena'da Aggressive/Hard tehlikeden aldığı 40 hasarı son Editor koşusunda sıfıra indirirken ilk silah isabeti 1,54 saniyeden 4,40 saniyeye çıktı. SweepArena'da Aggressive/Easy 3 kurtarma ve 10,72 saniyelik ilk isabetten 0 kurtarma ve 4,44 saniyelik ilk isabete geçti. Maç başına sayılar [Editor matrisi](editor.csv) ve [Windows matrisi](windows.csv) içindedir.

PulseArena'da Counter hâlâ hasar alıyor. Windows Player'da Easy için 50, Hard için 30 can kaybı ölçüldü. İki profil de dört gerçek silah isabeti yaptı. Sabit hedefe karşı yapılan 16 maç, klavyeden hareket eden bir oyuncunun tüm yaklaşımlarını temsil etmez. Açık fizik testinde zorluk gözlemleri 0,8 saniyede Easy için 2, Hard için 5 örnek verdi. Aynı tohumla ölçülen son nişan hatası sırasıyla 5,845° ve −1,221° idi.

Son Editor PlayMode paketi 39/39 geçti. Bot kontrolü 18, geometri ve navigasyon 10, gerçek bot oynanışı 9, yeni MainArena ve risk matrisi 2 testi kapsadı. Ayrı iki-bot ölçümünde 120 ısınma ve 500 örnek sonunda p50 0,0087 ms, p95 0,0386 ms, tepe 0,0974 ms ve yönetilen tahsis 0 B/adım ölçüldü. Ölçüm makinesinde Ryzen 9 9950X3D2, 63.152 MB sistem belleği ve Direct3D 11 kullanıldı.

Windows x64 Test Player iki testi de geçti. MainArena için geri sayım, Warning'de alternatif, Active'de bekleme ve çıkış, oyun zamanının durması ve devam etmesi, Recovery'de gerçek hareket, normal maç sonucu ve yeniden maç denendi. Önceki hedefli düzenek fazı hızlandırırken oyuncunun parçalarını parkura düşürüyordu. Son düzenek fazı ilerletirken iki robotu da sağlam tutuyor. Çevresel hasarı matris doğrudan oyun kurallarıyla ölçüyor.

Ham NUnit ve Player raporları görev kopyasının `RobotArena/Logs/Bot03/` klasöründeki `baseline.xml`, `acceptance-editor.xml`, `acceptance-win64.xml`, `acceptance-editor.log` ve `acceptance-win64-player.log` dosyalarıdır. Test Player içindeki `RobotSavaslari.Gameplay.dll` için SHA-256 `86524615D85A14129E4E8C75FB69567DD5413307D7CAB16B5427364611CC3CAD` değeridir.
