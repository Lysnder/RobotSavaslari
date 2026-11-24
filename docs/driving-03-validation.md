# DRIVING-03 · Sürüş eğitiminden ilk maça hazırlık

Sürüş eğitiminin içinden açılan iki adımlı rota, kontrollü duruşu ve hassas dönüşü mevcut fizik değerlendiricileriyle çalıştırır. Rehber etkin tuşları gösterir. Hazırlık denemelerinin skorları ve hayaleti kişisel rekorlardan ayrıdır.

24 Kasım 2026 · Unity 6000.4.4f1 · Windows x64 · MainArena. Windows dağıtım kaynağı `7427518c937ca012e7729f141ae01967f901d76f`. Görsel kabul testi dahil kaynak `c88b2f8c4f3814163a9f49a247a4d61afccf6770`. Ortak taban `6200ddcaff7bda984a88d343db1446c1a6ce57c1`.

## Test sonuçları

| NUnit çıktısı | Geçen / toplam | Atlanan | Süre (sn) | SHA-256 ilk 16 karakter |
| --- | ---: | ---: | ---: | --- |
| `driving-boundaries.xml` | 2/2 | 0 | 1,65 | `FEE925718D186781` |
| `driving-edit.xml` | 19/19 | 0 | 0,15 | `658124AE04EF1107` |
| `driving-final.xml` | 99/99 | 0 | 130,86 | `944861938889AD48` |

XML dosyaları ve aynı adlı Unity günlükleri yerel görev kopyasının `RobotArena/Logs/DrivingExerciseEvidence/` klasöründe. Korumalı koşuların ardından tercihlerin, kişisel kayıtların ve tekrar arşivinin ilk durumla eşleştiği doğrulandı.

## Windows oyunu

Dağıtım 193 dosya ve 500.583.850 bayt. Unity başarı kaydı `driving-win.log`. Oyunun `RobotSavaslari.Gameplay.dll` SHA-256 değeri `61CB96BEC2A5F238A84DB3D021B0210EB35CAD9CC626A135D153C5BEF75FC6C8`.

Normal Windows dağıtımında işletim sistemi fare girdisiyle Sürüş eğitimi açıldı ve Maça hazırlık seçildi. 1280×720’de Ana menü düğmesine basıldı ve Maça başla ile geri sayımın ardından gerçek maç HUD’ı görüldü. 1920×1080’de ilk adımın hedef halkası, bağlı tuşları ve iki eylem düğmesi aynı anda görünüyordu.

Hazırlık, gerçek katalogdaki Kontrollü duruş ve Hassas dönüş tanımlarını kullanır. Rehber bağlamı zaman turu, kişisel rekor ve ghost dosyalarına yazmaz. Başarısız denemenin sonraki adımı açmaması, yeniden deneme, tuşu bırakmadan yeni komut verilememesi, farklı eşlemeler, kapatma ve ikinci oturum otomatik olarak sınandı.

Ayrı Windows x64 test oyuncusu iki çözünürlükte dört adımın tamamını bitirdi ve iki maçı başlattı (1/1, `RobotArena/Logs/PreparationPlayer/preparation-win64.xml`). Bu koşuda test robotu fizik gövdesiyle hedefe yerleştirdi. Hız, duruş süresi, yön ve başarı kararları oyunun gerçek değerlendiricisine aitti. Sekiz özgün PNG `RobotArena/Logs/PreparationPlayer/Frames` klasöründe.

| Pencere | Normal Windows oyunu | Windows test oyuncusu |
| --- | --- | --- |
| 1280×720 | [Hazırlık](evidence/driving-03/hazirlik-720.jpg) · [Başlayan maç](evidence/driving-03/mac-720.jpg) | [Hassas dönüş](evidence/driving-03/donus-720.jpg) |
| 1920×1080 | [Hazırlık](evidence/driving-03/hazirlik-1080.jpg) | [Rota tamamlandı](evidence/driving-03/tamamlandi-1080.jpg) · [Maça geçiş](evidence/driving-03/mac-1080.jpg) |
