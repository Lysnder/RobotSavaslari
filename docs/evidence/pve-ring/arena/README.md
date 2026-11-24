# MainArena salonunun Windows kabulü

Unity 6000.4.4f1 ile MainArena sahnesinde gerçek maçlar açıldı. Her çözünürlükte aynı Open düellosu ve oyuncunun kullanabildiği aynı takip kamerası seçildi. Atmosfer kapalıyken sahnenin özgün tel kafesi ve zemin materyali geri gelir. Açıkken cam koruma panelleri, mat metal zemin, kenar ışıkları, pilot bölmeleri ve tribünler görünür. Yeni görsel parçaların collider'ı yoktur.

## Görüntüler

| Durum | 1280 × 720 | 1920 × 1080 |
| --- | --- | --- |
| Open, atmosfer kapalı | [720p](720-off-playing.jpg) | [1080p](1080-off-playing.jpg) |
| Open, geri sayım | [720p](720-on-countdown.jpg) | [1080p](1080-on-countdown.jpg) |
| Open, oyun | [720p](720-on-playing.jpg) | [1080p](1080-on-playing.jpg) |
| Open, bot isabeti | [720p](720-on-hit.jpg) | [1080p](1080-on-hit.jpg) |
| Open, sonuç | [720p](720-on-result.jpg) | [1080p](1080-on-result.jpg) |
| Tactical | [720p](720-on-Tactical.jpg) | [1080p](1080-on-Tactical.jpg) |
| PulseArena | [720p](720-on-PulseArena.jpg) | [1080p](1080-on-PulseArena.jpg) |
| SweepArena | [720p](720-on-SweepArena.jpg) | [1080p](1080-on-SweepArena.jpg) |

## Ölçüm yöntemi

VSync kapalı, kare sınırı serbest. Her koşul sahneyi yeniden yükledi. Kameranın yatay açısı 12°, eğimi 37°, uzaklığı 16 m. Oyuncu giriş vermedi, rakip üretim botunu kullandı. İlk 45 kareden sonra ardışık 300 kare kaydedildi. Önce 720p kapalı/açık, ardından 1080p açık/kapalı çalıştı. CPU ve GPU süreleri Unity FrameTimingManager'dan, fizik süresi Physics.Simulate sayacından geldi. P95 sıralı 300 örneğin 285'incisidir. [Ham kareler](frames.csv) · [Windows oyuncusu test sonucu](windows.xml)

NVIDIA GeForce RTX 5090, Direct3D 12 (feature level 12.2). Test oyuncusunda dört düzenin 18 görüntüsü doğru çözünürlükte üretildi. Grafik kabulü 63,60 saniyede geçti. Açık ve kapalı ölçüm hücrelerinin her biri tek bir maç örneğidir.

| Çözünürlük | Atmosfer | CPU p95, ms | GPU p95, ms | Fizik p95, ms | GC p95, B/kare | Toplam bellek medyanı, MiB |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 720p | Kapalı | 1,9309 | 1,7267 | 0,0757 | 7.544 | 234,60 |
| 720p | Açık | 1,9848 | 1,7523 | 0,0797 | 7.544 | 236,50 |
| 1080p | Kapalı | 2,2102 | 2,0910 | 0,0723 | 7.544 | 236,54 |
| 1080p | Açık | 2,2306 | 2,0078 | 0,0907 | 7.544 | 237,46 |

## PlayMode regresyonları

Üç test geçti. Dört düzende aynı fizik sınırları ve iki üretim robotu korundu. Yakın koruma paneli ve dikmeleri alçak kamerada gizlendi, kamera yükseldiğinde geri geldi. Meydan Okuma seçim ve özet pencerelerinde, duraklatmada ve tekrar izlerken atmosfer sustu. Pencere kapandığında geri sayım, oynanış ve kazanan takım sinyali çalıştı. [ArenaVenueTests sonuçları](play.xml)

Tüm kenarlar görünürken normal kalitede 37 render grubu ve 16.346 üçgen, düşük kalitede 29 render grubu ve 11.018 üçgen sayıldı. İki kalitede de tribün, cam ve ışık geometrileri çarpışma üretmez. Normal kalite dört gölgesiz yerel ışık kullanır.

Referans klibin resmi oynatıcısı bu ortamda “Video unavailable” yanıtı verdi. Zaman kodlarındaki düzen için görevde tarif edilen koruyucu çerçeve, ışıklar ve pilot/seyirci yerleşimi kullanıldı. [TLC Türkiye videosu](https://www.youtube.com/watch?v=i5dLa91MeVg)
