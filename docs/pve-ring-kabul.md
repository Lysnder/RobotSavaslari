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
| İki arka | 0° | +5,20° | 0,000 m | 7,02 m/s² | 2,49 m/s | 0,098 m |

25 fizik adımındaki son yerleşme ölçümünde şasi yüksekliği dört ondalık basamakta sabit kaldı. Konveyör, duvar ve dış darbe senaryoları geçti. Bot da aynı köşe desteğini kullandı. Yeni raund ve rövanşta gövde açısı, collider ölçüsü, hızlar ve dört teker geri geldi.

## Beş yeni silah

Üretim eşleşmelerinde her yeni silah iki rolde de gerçek hedefe isabet etti. Aşağıdaki sayılar Open arenasında olağan doğuşlardan sonuca kadar oynanan beş maçtan gelir. Bot kendi hareket ve saldırı kararlarını verdi. Test pilotu klavyeyle sürdü ve silahın gerçek menzil ve açısını kullandı.

| Oyuncu | Bot | Oyuncunun silah isabeti | Botun silah isabeti |
| --- | --- | ---: | ---: |
| Dikey Tambur | Üstten Çekiç | 3 | 1 |
| Üstten Çekiç | Fırlatıcı | 2 | 2 |
| Fırlatıcı | Kıskaç | 6 | 3 |
| Kıskaç | Testere | 7 | 22 |
| Testere | Dikey Tambur | 15 | 2 |

Her maçtan sonra farklı canlı kurulum seçildi ve üretilen tekrar iki silahın kayıtlı mesh imzalarıyla açıldı. Diğer testlerde Çekiç ıskasında hasar oluşmadı ve 1,24 sn toparlanma kaldı. Ölçülen yukarı hız Tambur için 1,145 m/s, Fırlatıcı için 2,831 m/s idi. Testere temas kesilince durdu. Kıskaç zaman aşımında, silah koptuğunda ve raund sonunda bıraktı.

## Arena ve kare süresi

[18 Windows görüntüsü, ölçümler ve ham CSV](evidence/pve-ring/arena/README.md). NVIDIA RTX 5090 üzerinde 1.200 kare örneklendi.

| Çözünürlük | CPU p95 kapalı → açık | GPU p95 kapalı → açık |
| --- | ---: | ---: |
| 1280 × 720 | 1,931 → 1,985 ms | 1,727 → 1,752 ms |
| 1920 × 1080 | 2,210 → 2,231 ms | 2,091 → 2,008 ms |

Aynı dört ölçümde GC p95 7.544 bayt/kare. Salon normal kalitede 16.346 üçgen ve 37 çizim grubu, düşük kalitede 11.018 üçgen ve 29 grup kullandı.

## Windows x64 dağıtımı

Normal dağıtım 194 dosyada **768.425.322 bayt**. Önceki birleşik pakete göre artış **29.400 bayt** (28,7 KiB). Oynanış DLL dosyasının SHA-256 özeti `8a6382b814d88c7f1bce252809a4fc833cfe06b05120eecb36a1141c4b51ffdb`. Proje içindeki paket yolu `RobotArena/Builds/Windows/RobotSavaslari.exe`.

Normal EXE’de geçici atölye, maç geçmişi ve tekrar arşiviyle iki maç oynandı. Klavye girdileri ve bot kararlarıyla iki tarafın da silah hasarı kaydedildi. Maç ve rövanş birer sonuç üretti, oyuncu sırasıyla 58,54 ve 80,98 canla kazandı. Test oyuncusunun ayrı Windows süreçleri beş karşılaşmayı yazıp yeniden açtı. Atölye, Tactical ve iki aşamalı sürüş hazırlığı da 720p ve 1080p’de geçti.

## Tarihsel tekrar dosyası

[Eski Kama–Lifter maçı](evidence/pve-ring/legacy-kama.rpl), önceki atölye dalı `097aad0` üzerinde geçici test verileriyle MainArena’da oynatılıp o sürümün kendi arşiv kaydedicisiyle yazıldı. Mavi Kama silahı 144, eski kırmızı Lifter 42.000 tepe içeriyor. Boyut 26.573 bayt, SHA-256 `1257ad160b70b648e0a86ce328e93e3ee3bbde481ddf04f414083b2b8ed46f26`. Editör regresyonu bu dosyayı ve güncel çiftle kaydedilen yeni karşılaşmayı farklı hazır robot seçiliyken açar. Ek bozulma testi, hasarlı kareyi reddedip açıklama verdiğini denetler.
