# B1 — Güvenli başlangıç ve maç durumları

İncelenen yerel başlangıç commit'i: `7a5bf00`. Emin'in ekip için belirlediği
başlangıç commit'i depoda belgelenmemiş; bu kimlik ekip tabanı olarak varsayılmadı.

## Çalıştırma

1. Unity 6000.4.4f1 ile RobotArena projesini aç.
2. `Assets/Scenes/Tests/MatchRegression.unity` sahnesini aç ve Play'e bas.
3. Hazır durumunda iki deneme girdisini dene: RED, iki izin KAPALI.
4. Başlat'a bas: iki izin AÇIK, girdiler KABUL. WASD/oklar hareket, Space silah.
5. Başlat'a tekrar tekrar bas: maç sayısı 1, robot sayısı 2 kalır.
6. Oyuncuyu yen veya Rakibi yen: Sonuç, iki izin KAPALI, girdiler RED.
7. Sonuç durumunda Başlat etkisizdir. Hazıra dön düğmesi robotları mevcut
   başlangıç pozlarına geri koyar; bundan sonra Başlat yeni maç açar.

Sahne üreticisi: `RobotSavaslari > Burak > Build B1 Test Scene`.
Var olan B1 sahnesini korur. Ortak Arena üzerinde kayıt yapmaz.

## Uygulama ve doğrulama

`MatchController` mevcut `StopCombat`, `ConfigureCombat`, `ResetMotion`
API'sini kullanır. Ortak hareket koduna ek yapılmadı. Sahnedeki eski maç ve
seçim bileşenleri yalnız Burak kopyasından kaldırıldı. Başlat robot üretmez;
yalnız Hazır → Oynanıyor geçişi yapar. Başka geliştiricinin yeni bileşeni gerekmez.

9 Kasım 2026, Unity Editor Play Mode: `MatchFlowChecks.Run()` başarılı.

- İki robotun kendi spawn XZ konumları ve birbirlerine bakış yönleri doğrulandı.
- Collider çiftlerinde `Physics.ComputePenetration`: robotlar çakışmıyor.
- Hazır / Oynanıyor / Sonuç durumlarında iki robotun izinleri ve deneme
  girdilerinin ret / kabul / ret sonuçları doğrulandı.
- Başlat'a 25 ek çağrı: tek maç, iki robot.
- İki farklı robotun yenilgisi ve Hazır'a dönerek ikinci maç açma doğrulandı.
- Play Mode kare sayısı 1976'dan 6740'a ilerledi; çalışma donmuş değildi.
- Console'da hata ve derleme hatası yok. Deneme paneli ekran görüntüsü incelendi.

Kontrolleri tekrar çalıştırmak için sahneyi yeni Play oturumunda aç;
Unity Pipeline `script eval` ile
`return RobotSavaslari.Match.Editor.MatchFlowChecks.Run();` çalıştır.
Kontrol, Hazır durumunda başlatılmalıdır; sonunda tekrar Hazır'a döner.

## Emin'e iletilecek ortak taban eksikleri

Karttaki ortak prefab ve iki mevcut spawn nesnesi bu commit'te bulunmuyor:
`Assets` altında `.prefab` dosyası yok; robotlar `Arena.unity` içinde sahne
nesneleri. Başlangıç konumları `ArenaMatchController` içinde sabit:
oyuncu (-3.15, 0.82, 0), rakip (3.15, 0.82, 0); Y yönleri +90 / -90 derece.

Burak kopyasında bu mevcut pozlardan iki yerel spawn işaretçisi oluşturuldu ve
mevcut iki robot korundu. Ortak prefab veya ortak spawn üretilmedi; bunlar kartın
teslim alanı dışında. Emin ortak tabanı sağladığında Burak sahnesindeki robotlar
bu prefab ile bağlanmalı ve spawn referansları karşılaştırılmalı.

Bu nedenle davranış kabul kontrolleri geçti; “iki ortak prefab örneği / mevcut
spawn nesneleri” şartının birebir ekip kabulü ve resmi başlangıç commit'i teyidi
Emin'e bağlıdır. Kart bu açık maddeler çözülmeden tamamen kapalı sayılmamalı.
Ana Arena, ortak kod ve proje ayarları değiştirilmedi. P05B kapsamına girilmedi.
