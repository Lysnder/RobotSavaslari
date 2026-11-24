# PvE salonu ve robot maçları

Bu geliştirme üç önceki etabın kodunu içerir. GitHub test tabanı `ce54eef`, o anda açık olan #73’ün başı `cdea653`. #70 ve #72 test dalındaydı. Önceki birleşik Windows paketi 194 dosyada 768.395.922 bayttı. Karşılaştırma Unity 6000.4.4f1 ve Windows x64 içindir.

## Oyunda deneme

Hazır ekranında Atölye’yi aç. Silah satırındaki oklar yedi seçeneği dolaşır. Her kart silahın avantajını ve bedelini gösterir. **Uygula** seçili lastiği, zırhı ve silahı maça taşır. Maç başladıktan sonra HUD silahın hazırlığını ve bekleme süresini gösterir.

Maç geçmişinden yeni bir karşılaşmayı seçince aynı iki robot, arena, bot kişiliği, zorluk ve kurallarla yeni bir maç açılır. Önceki sürümlerin yalnızca sonuç ve kural içeren kayıtları, kayıtlı kuralı güncel seçimlerle kullanır. Tekrar arşivi, eski Kama maçında kayıtlı kırmızı Lifter görünümünü canlı robota takmadan gösterir.

## Kabul kapsamı

Klavye girdisi üreten otomasyon, Windows test oyuncusu ve Unity Test Framework kullanıldı. Tevfik’in fiziksel klavye/fare ile proje geneli kabulü sonraki aşamadır.

YouTube bağlantısında zaman kodlu oynatma bu ortamda kullanılamadı. Ring çerçevesi, paneller, ışık şeridi, pilot ve seyirci yerleşiminde görev metnindeki tarif izlendi. Sonuçlar oyunun kendi kameralarından alındı.

## Teker kaybı

Open yerleşiminde aynı komutlar ve aynı şasi ile ölçüldü. Sapmanın eksi işareti solu, artı işareti sağı gösterir. Fren karşılaştırması her koşulda 2 m/s hızda başlar.

| Kopan teker | Yana yatış | Burun yukarı | Düz çizgi sapması | Hızlanma | Ölçülen hız | 2 m/s’den fren |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Yok | 0° | 0° | −0,001 m | 16,55 m/s² | 5,93 m/s | 0,054 m |
| Sol arka | +3,76° | +3,91° | −0,317 m | 12,34 m/s² | 4,22 m/s | 0,074 m |
| Sağ arka | −3,75° | +3,81° | +0,319 m | 13,66 m/s² | 4,22 m/s | 0,074 m |
| İki arka | 0° | +5,20° | 0,000 m | 8,38 m/s² | 2,49 m/s | 0,098 m |

Tablo son toplu PlayMode turundan alındı. İki arka teker kaybıyla ayrı hedefli koşuda 7,02 m/s² ölçüldü. 25 fizik adımındaki son yerleşme ölçümünde şasi yüksekliği dört ondalık basamakta sabit kaldı. Konveyör, duvar ve dış darbe senaryoları geçti. Bot da aynı köşe desteğini kullandı. Yeni raund ve rövanşta gövde açısı, collider ölçüsü, hızlar ve dört teker geri geldi.

## Beş yeni silah

Üretim eşleşmelerinde her yeni silah iki rolde de gerçek hedefe isabet etti. Aşağıdaki sayılar 60 FPS koşusunda Open arenasında olağan doğuşlardan sonuca kadar oynanan beş maçtan gelir. Bot kendi hareket ve saldırı kararlarını verdi. Test pilotu klavyeyle sürdü ve silahın gerçek menzil ve açısını kullandı.

| Oyuncu | Bot | Oyuncunun silah isabeti | Botun silah isabeti |
| --- | --- | ---: | ---: |
| Dikey Tambur | Üstten Çekiç | 3 | 1 |
| Üstten Çekiç | Fırlatıcı | 3 | 2 |
| Fırlatıcı | Kıskaç | 6 | 3 |
| Kıskaç | Testere | 5 | 22 |
| Testere | Dikey Tambur | 10 | 4 |

Her maçtan sonra farklı canlı kurulum seçildi ve üretilen tekrar iki silahın kayıtlı mesh imzalarıyla açıldı. Diğer testlerde Çekiç ıskasında hasar oluşmadı ve 1,24 sn toparlanma kaldı. Ölçülen yukarı hız Tambur için 1,145 m/s, Fırlatıcı için 2,831 m/s idi. Testere temas kesilince durdu. Kıskaç zaman aşımında, silah koptuğunda ve raund sonunda bıraktı. Aynı 0,2 sn geri sürüş komutunda hedef tutulurken 0,516 m/s, serbestken 2,957 m/s hızlandı. Sabit adım ölçümü beş maçta da ring dışına taşma göstermedi. Kıskaç–Testere maçında şasi yüksekliği 0,80–0,82 m, azami hız 6,29 m/s idi. Aynı beş maç 30 FPS’de de tamamlandı. Toplam on maçta on arşiv tekrar açıldı. Şasilerin tamamında ölçülen en yüksek hız 6,56 m/s oldu.

## Arena ve kare süresi

[18 Windows görüntüsü, ölçümler ve ham CSV](evidence/pve-ring/arena/README.md). NVIDIA RTX 5090 üzerinde 1.200 kare örneklendi.

| Çözünürlük | CPU p95 kapalı → açık | GPU p95 kapalı → açık |
| --- | ---: | ---: |
| 1280 × 720 | 2,8078 → 2,8513 ms | 2,6639 → 2,5935 ms |
| 1920 × 1080 | 2,8999 → 2,9740 ms | 2,6094 → 2,6051 ms |

Aynı dört ölçümde GC p95 7.544 bayt/kare. Salon normal kalitede 16.346 üçgen ve 37 çizim grubu, düşük kalitede 11.018 üçgen ve 29 grup kullandı.

## Windows x64 dağıtımı

Normal dağıtım 194 dosyada **768.426.346 bayt**. Önceki birleşik pakete göre artış **30.424 bayt** (29,7 KiB). Oynanış DLL dosyasının SHA-256 özeti `d2c22ccd3d1e47ad5f6df8d1a2a0276d3f0064cd81aa2688fd9cc04335a7c882`. Proje içindeki paket yolu `RobotArena/Builds/Windows/RobotSavaslari.exe`.

Normal EXE’de geçici atölye, maç geçmişi ve tekrar arşiviyle iki maç oynandı. Klavye girdileri ve bot kararlarıyla iki tarafın da silah hasarı kaydedildi. Maç ve rövanş birer sonuç üretti, oyuncu sırasıyla 56,90 ve 62,98 canla kazandı. Test oyuncusunun ayrı Windows süreçleri beş karşılaşmayı yazıp yeniden açtı. Atölye, Tactical ve iki aşamalı sürüş hazırlığı da 720p ve 1080p’de geçti.

## Tarihsel tekrar dosyası

[Eski Kama–Lifter maçı](evidence/pve-ring/legacy-kama.rpl), önceki atölye dalı `097aad0` üzerinde geçici test verileriyle MainArena’da oynatılıp o sürümün kendi arşiv kaydedicisiyle yazıldı. Mavi Kama silahı 144, eski kırmızı Lifter 42.000 tepe içeriyor. Boyut 26.573 bayt, SHA-256 `1257ad160b70b648e0a86ce328e93e3ee3bbde481ddf04f414083b2b8ed46f26`. Editör regresyonu bu dosyayı ve güncel çiftle kaydedilen yeni karşılaşmayı farklı hazır robot seçiliyken açar. Ek bozulma testi, hasarlı kareyi reddedip açıklama verdiğini denetler.

## Son test sonuçları

Unity EditMode süitinde 237 test geçti. Toplu PlayMode süitinde 314 test bulundu, 309 test geçti, hata sayısı sıfır. Editörün atladığı beş kontrol ayrı Windows oyuncularında geçti. Bunlar atölyeden maç ve rövanş, Tactical, iki aşamalı sürüş hazırlığı, çift çözünürlükte arena ölçümü ve ayrı oyun sürecinden beş kaydı yeniden açmadır.

Son Windows x64 test oyuncusu Kıskaç bırakma, aynı hedefi tutan iki Kıskaç ve beş doğal silah düellosu kontrollerinde 3/3 geçti. Beş düelloda da iki taraf isabet kaydetti. Gövdeler ring içinde kaldı ve beş maç arşivi yeniden açıldı. Sonuçlar oyuncunun doğrudan yazdığı NUnit XML dosyasından alındı. Oyuncu kapandıktan sonra Unity editörünün uzaktan sonuç bağlantısı zaman aşımına uğradı.

Süre sonunda kalan can oranı yüksek robot kazandı. Eşitlikte raund berabere sonuçlandı ve İlk 2 serisinde galibiyet sayısını artırmadan tekrarlandı. Eski kayıtların kuralları ile güncel seçimlerden rövanş ve yeni kayıtlarda tam kurulumla yeni kimlik oluşturma geçti.

[Çalıştırılan testlerin adları ve sonuçları](evidence/pve-ring/unity-results.json). Kişisel tercihlerin 46 kayıt defteri girdisi ve sekiz veri dosyası, kullanıcı tekrar arşiviyle birlikte koruma denetiminden geçti. Test ve derleme günlükleri yerel Unity projesinde `RobotArena/Logs/PveEvidence` altında bulunur.
