# DUEL-01 · Hazır düellolar ve aynı koşullarla rövanş

Sağ kurulum sütununa üç hazır düello ekler. Oyuncu arena, davranış, zorluk, süre ve biçimi düzenleyebilir. Başlayan karşılaşmanın kurulumu rövanş için saklanır. Sonuç ekranı nakavt ve süre sonunu ayırır.

24 Kasım 2026 · Unity 6000.4.4f1 · Windows x64 · MainArena. Windows dağıtım kaynağı `bde3b3d1dbca6d312d480b1404ad826930905422`. Görsel kabul testi dahil kaynak `bde3b3d1dbca6d312d480b1404ad826930905422`. Ortak taban `6200ddcaff7bda984a88d343db1446c1a6ce57c1`.

## Test sonuçları

| NUnit çıktısı | Geçen / toplam | Atlanan | Süre (sn) | SHA-256 ilk 16 karakter |
| --- | ---: | ---: | ---: | --- |
| `duel-contrast-play.xml` | 36/36 | 0 | 36,16 | `7CAE22324A6975D3` |
| `duel-contrast-rendered-win.xml` | 1/1 | 0 | 13,11 | `F16F6FD353E1E098` |
| `duel-modals.xml` | 17/17 | 0 | 97,24 | `739E4BD75AD8B1B6` |

XML dosyaları ve aynı adlı Unity günlükleri yerel görev kopyasının `RobotArena/Logs/DrivingExerciseEvidence/` klasöründe. Korumalı koşuların ardından tercihlerin, kişisel kayıtların ve tekrar arşivinin ilk durumla eşleştiği doğrulandı.

## Windows oyunu

Dağıtım 193 dosya ve 500.583.594 bayt. Unity başarı kaydı `duel-release-final.log`. Oyunun `RobotSavaslari.Gameplay.dll` SHA-256 değeri `DAED735753C8FF9B85E1DF0E020D58AC87FB047C1D869F058DA076D0068789C4`.

Normal Windows dağıtımında işletim sistemi fare girdisiyle Taktik düello seçildi. 1920×1080’de Tactical, karşı saldırı, zor ve 120 saniyelik tek maç ayarlarıyla oyun başladı. Rakip son dağıtım koşusunda 33,0 saniyede nakavt kazandı. Sonuç ekranındaki “Aynı ayarla yeniden oyna” düğmesinden yeni geri sayım açıldı. Önceki 2436d7e dağıtımının 1280×720 kontrolünde hazır seçenekler, süre, zorluk ve arşiv düğmeleri birlikte görünüyordu.

Maç akışı regresyonları süre sonunda can oranı, eşitlik, ilk iki galibiyet, eski geçmişten rövanş ve yerel mücadeleyi kapsar. Menü regresyonlarında eğitim, tuş yakalama, arşiv, son vuruş tekrarı ve inceleme sırasında giriş sahipliği kontrol edildi.

Sonuç paneli parlak zeminde küçük yazının okunabilmesi için mevcut koyu menü yüzeyini kullanıyor. Son kaynakta 36 maç ve menü testi tekrar geçti. Windows test oyuncusunda iki çözünürlükte Taktik düello, gerçek RobotHealth hasarından sonra sonuç ekranı, rövanş geri sayımı ve ikinci maç doğrulandı (1/1). Önceki 17 modal regresyonu `2436d7e` üzerinde çalıştırıldı. Sekiz özgün test görüntüsü `RobotArena/Logs/MenuEvidence/RenderedPlayer` altında.

| Pencere | Kurulum | Sonuç ve rövanş |
| --- | --- | --- |
| 1280×720 | [Taktik düello](evidence/duel-01/kurulum-720.jpg) | [Windows test oyuncusu sonucu](evidence/duel-01/sonuc-720.jpg) |
| 1920×1080 | [Taktik düello](evidence/duel-01/kurulum-1080.jpg) · [Maç başlangıcı](evidence/duel-01/mac-1080.jpg) | [Windows oyunu sonucu](evidence/duel-01/sonuc-1080.jpg) · [Rövanş](evidence/duel-01/rovans-1080.jpg) · [Test oyuncusunda diğer kazanan](evidence/duel-01/test-oyuncusu-sonuc-1080.jpg) |
