# REPLAY-03 · Dört arenada tekrar arşivi

Eray · [#62](https://github.com/Lysnder/RobotSavaslari/issues/62) · [REPLAY-02](https://github.com/Lysnder/RobotSavaslari/issues/45) · [PR #54](https://github.com/Lysnder/RobotSavaslari/pull/54) · [Önceki arşiv doğrulaması](replay-archive-validation-20261122.md). Ortak taban `6200ddcaff7bda984a88d343db1446c1a6ce57c1`. Unity `6000.4.4f1`, Windows x64.

## Oyuncu akışı

MainArena’da maç veya seri bittikten sonra ana menüde **Tekrar arşivi** açılır. Olayları darbe, parça, tehlike veya raund olarak süzmek ve bir raunda daraltmak mümkündür. Bir satır veya önceki/sonraki olay düğmesi oynatmayı o anda duraklatır. **−1 kare** ve **+1 kare** sonraki veya önceki gerçek örnek zamanına gider. Raund sınırları zaman çizgisinde işaretlidir. Arşiv kapatılınca Ready menüsü ve önceki kamera/girdi sahipliği geri gelir.

## Biçim ve görsel doğruluk

| Özellik | v1 | v2 içerik revizyonu |
| --- | --- | --- |
| RSRA başlık şeması | 1 | 1 |
| Open ve Tactical | Asıl kayıt dosyasından okunur | Yeni kayıt yazılır |
| Pulse ve Sweep | Hücre ve nesne anahtarları reddediliyordu | Her zemin işaretçisi bir dinamik iz kullanır |
| Tehlike görünümü | Kayıtta yok | 20 Hz dünya konumu, dönüşü, ölçeği, görünürlüğü ve canlı faz |
| Darbe kaynağı | Kaynak belirsiz | Silah bayrağı, robot kaynak referansı veya kaynak belirsiz |

Pulse/Sweep küp mesh’i eski sürümde de çizilebilir durumdaydı. Eski kaydedici hücreyi ilk denetimde reddederdi. Bu engel tek başına kaldırıldığında tehlikenin grup/işaretçi yolu yine beyaz listeyi geçemezdi. Anahtarlar ve hücreler kabul edilse bile eski dinamik pozisyon alanları Pulse’un ölçeğini, Sweep’in faz değişimindeki dönüş yolunu ve Recovery durumunu temsil edemezdi. Yeni sürüm yalnız mevcut Pulse/Sweep hücre koordinatlarına ait iki işaretçi adını ve küp mesh imzasını çözümler.

Bir tehlike işaretçisi kare başına 22 baytlık dinamik poza 12 bayt ölçek ve 1 bayt faz ekler. Toplam maliyeti 35 bayt/karedir. Kesintisiz fazda hareket ve ölçek iki örnek arasında enterpole edilir. Faz değiştiğinde oynatıcı önceki örneği tutup yeni durumun gerçek örnek zamanında geçer. Idle ve Recovery aynı canlı geometriye sahip olabilir. Olay listesi bunları kayıtlı faza göre ayırır. Oynatma sırasında sahnede yalnız mesh ve çizgi sunumu kurulur. RobotHit üzerinde arena tehlikesini kesinleştiren bir kaynak yoktur. Tehlike fazı ve darbe ayrı olaylardır. Eşzamanlılık darbenin kaynağını kanıtlamaz.

V1 fixture’ları 23 Kasım 2026’daki yerel Unity çalışmasından arşivlenmiş özgün dosyalardır. Open kaydında tek raund ve üç olay bulunur. Tactical kaydının başlığı 2–0 seridir, ancak dosyada bir raund, iki olay ve kaydın raund ortasında başladığına dair özgün Eksik uyarısı vardır. Geçiş bu eksikliği korur. İki fixture’ın tüm metin alanları çözümlendi. Yalnız rastgele karşılaşma/arşiv kimlikleri, UTC zamanı, oyunun Türkçe başlıkları, sabit robot/mesh ve yerleşim adları ile Tactical’in uyarısı bulunur. Kullanıcı adı, makine yolu ve oyuncu profili bulunmadı.

| Özgün fixture | SHA-256 |
| --- | --- |
| OpenV1.bytes | `08ec1a3b869e2d15ed088805eada629b7e8c4a2939b03b85f626534ff4e98539` |
| TacticalV1.bytes | `059394ce400bb45b6df5920829043104b7563a8e8351936d4ee074cb1bf5b5ca` |

Dosyalar yeniden yazılmaz. İleri revizyon, bozuk sağlama ve bilinmeyen nesne için hata arşiv satırında gösterilir. İz sınırı 128, olay bütçesi 4.096 + ayrılmış sınır olayları, dosya sınırı 128 MiB ve toplam arşiv kotası 10 kayıt / 512 MiB olarak korunur. Kota veya iz sınırında Eksik durumu ve nedeni arşivde görünür. Tek bir kesintiden sonra aynı maç için ikinci dosya oluşturulmaz. Uygulamanın normal kapanışında açık kayıt Eksik olarak sonuçlanır. İşlemin zorla sonlandırılması tamamlanmamış geçici dosya bırakabilir. Bu dosya sonraki açılışta temizlenir.

## Doğrulama

Windows x64 test oynatıcısının ilk çalıştırması dört tek maç ve iki raundluk bir seri üretti. Beş karşılaşma testinin beşi geçti. Ölçülen oyun kaynakları `0c7139c0048318bef8d0ad4a64e7c693b13dc9c3`, sonraki kod değişikliği dar ekranda inceleme ve olay panellerinin genişliğini düzenler. Ana sahnenin gerçek kaydedicisi, arşiv penceresi, filtresi ve oynatıcısı kullanıldı. Dört maçta aynı 4,4 saniyelik senaryoda robotlar ayrı tutuldu, bir darbe verildi ve maç sonucu tetiklendi. Her dosya yeniden açılıp 100 rastgele zamana gidildi. Yazma p95 değeri pozların okunması, olaylar ve arşiv tamponuna yazmayı kapsar. Arama p95 değeri okuyucuyla birlikte proxy güncellemesini içerir.

| Windows x64 | İz | Kare | Dosya, bayt | Yazma p95, ms | Arama p95, ms | Kaydedici / okuyucu belleği, bayt |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Open | 115 | 89 | 239.499 | 0,203 | 0,291 | 234.820 / 191.852 |
| Tactical | 117 | 89 | 239.709 | 0,072 | 0,294 | 235.332 / 191.852 |
| PulseArena | 117 | 90 | 248.769 | 0,104 | 0,414 | 236.908 / 197.092 |
| SweepArena | 115 | 89 | 246.050 | 0,069 | 0,362 | 236.396 / 197.092 |

Sınırlar yazma p95 için 2 ms, arama p95 için 200 ms ve kaydedici + okuyucu belleği için 64 MiB. Süre gerçek oyun saatinden örneklendiği için kare sayısı iki koşuda bir kare farklı olabilir. EditMode’da 128 iz ve 128 MiB dosya sınırı, CRC, bozuk dosya, ileri şema, kota ve bekleyen arşivin yeniden açılması da sınandı.

İlk Windows süreci yalnız yeni, ayrı bir QA klasörüne beş dosya bıraktı. Sonraki Windows süreci aynı klasörü yeni bir Ready ekranından açtı. Beş kimlik ve dört farklı arena doğrulandı. Tamamlanan seri dâhil her kayıtta ilgili son raund süzülerek sonuç olayına gidildi. Yeniden başlatma testi **1/1 geçti**. Kapsamı tekrar etmek için `REPLAY03_ACCEPTANCE_DIR` ortam değişkenine boş bir QA klasörünün mutlak yolunu verin. Windows Test Runner’da önce `FullEncounterReopensWithFilteredEventsAndRecordedHazards|SeriesRecordsEveryRoundWithOrderedBoundaries`, ardından yeni bir `StandaloneWindows64` çalıştırmasında `ReopenEncountersAfterPlayerRestart` filtresini kullanın. Kişisel kayıtlar ve tercihler koşulardan sonra karşılaştırıldı.

## Eski tabanla karşılaştırma

Aynı Unity Editor ve donanımda, aynı 4,4 saniyelik maç ve aynı tohumlu 100 arama iki sürümde de çalıştırıldı. Önceki sürümün dört vakası ayrı geçici proje kopyasında çalıştı. Oyun kaynakları birebir ortak tabandı. Ölçüm için yalnız test sınıfına aynı senaryo eklendi. Pulse/Sweep eski sürümde dosya üretmediği için süre ve boyut ölçülemedi.

| Editor / arena | Eski iz / kare / bayt | Yeni iz / kare / bayt | Eski → yeni yazma p95, ms | Eski → yeni arama p95, ms |
| --- | --- | --- | --- | --- |
| Open | 115 / 90 / 241.834 | 115 / 90 / 241.835 | 0,214 → 0,153 | 0,296 → 0,324 |
| Tactical | 117 / 89 / 239.708 | 117 / 90 / 242.045 | 0,131 → 0,158 | 0,328 → 0,332 |
| PulseArena | Hücre reddedildi | 117 / 90 / 248.769 | — → 0,132 | — → 0,302 |
| SweepArena | Hücre reddedildi | 115 / 89 / 246.050 | — → 0,096 | — → 0,338 |

90 karelik iki Open kaydı arasında tek bayt fark vardır. Bu senaryoda bir darbe kaynağı saklanır.

## Regresyonlar ve kanıt

Son Editor PlayMode koşusunda kaynak `62d193da8d838261559a43be7e60d373787be7b4` ile 24 vaka geçti. Windows’a özgü yeniden başlatma vakası Editor listesinden platform koşuluyla çıkarıldı. Tamamlanmış ve terk edilmiş seri, eski Open/Tactical dosyalarını Ready ekranından oynatma, kesilen maçı tek dosyayla kapatma, sonuç tekrarı, parça incelemesi, kamera/girdi sahipliği ve menüye dönüş aynı pakette doğrulandı. İlk koşuda Ready durumundaki fizik gövdesi iki kare arasında küçük bir konum farkı üretmişti. Son test canlı gövdeyi sabitleyip arşiv öncesi ve sonrası konumu tam eşitlikle denetledi.

Uzun süreli Editor testi 20 Hz için `Time.captureDeltaTime = 0.05` kullandı. 5 dakikada 6.001 kare ve 14,1 MiB, 15 dakikada 18.001 kare ve 42,4 MiB kaydedildi. 15 dakikalık koşuda yazma p95 0,110 ms, arama p95 0,506 ms, tüm CRC doğrulaması 141 ms sürdü. Kaydedici 229 KiB ve okuyucu 187 KiB tuttu.

Ham XML ve Unity logları yerel çalışma kopyasında `RobotArena/Logs/DrivingExerciseEvidence/` altında saklanır. Baseline koşusunu yeniden üretmek için aynı dizindeki `replay-base-instrumentation.diff` yalnız eski tabanın test fixture’ına uygulanır.

| Koşu | XML ve log dosyalarının ortak adı | Sonuç |
| --- | --- | --- |
| Eski tabanda dört arena | `replay-base` | 4/4 |
| Arşiv biçimi, kota ve eski dosyalar | `replay-edit-final` | 21/21 |
| MainArena Editor PlayMode | `replay-play-final` | 24 geçti, 1 Windows vakası dışlandı |
| Gerçek Pulse hasarı ve kaynak etiketleri | `replay-source` | 1/1 |
| Windows x64 maç ve seri kaydı | `replay-win-record` | 5/5 |
| Windows x64 ikinci süreçte beş kaydın açılması | `replay-win-restart` | 1/1 |
| Son Windows x64 derlemesinde Pulse hasarı ve yeniden açma | `replay-win-final` | 2/2 |

Son Windows x64 derlemesi `62d193da8d838261559a43be7e60d373787be7b4` ile çalıştı. İkinci süreç için ayrılmış aynı beş arşivi açtı ve canlı Pulse hasarının belirsiz bir kaynak olarak saklandığını doğruladı.
