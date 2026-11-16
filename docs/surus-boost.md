# Sürüş boost'u ve enerji

Oyuncu sınırlı enerji harcayarak kısa süreli hızlanabilir. Mevcut WASD/ok
davranışı değişmedi; boost ortak `RobotMotor` üzerinden, ikinci bir hareket
yazıcısı olmadan çalışır.

## Oynanış

| Tuş | İş |
| --- | --- |
| `W` `S` / `↑` `↓` | ileri, geri |
| `A` `D` / `←` `→` | dönüş |
| `LeftShift` | boost |

`Space` silah tarafında olduğu için boost varsayılanı `LeftShift`. Enerji
göstergesi ekranın sol altında: dolu mavi, boost sırasında turuncu.

Boost yalnızca **ileri** sürerken devreye girer. Geri viteste tuş basılı olsa
bile enerji harcanmaz ve geri hız tasarlanan sınırın üstüne çıkmaz.

## Varsayılan ayarlar

| Alan | Değer |
| --- | --- |
| Kapasite | 100 |
| Başlangıç enerjisi | 100 |
| Tüketim | 40 / saniye |
| Dolum | 25 / saniye |
| Dolum gecikmesi | 1 saniye |
| Yeniden başlama eşiği | 5 |
| Hız çarpanı | ×1.5 |
| Hızlanma çarpanı | ×1.5 |

Hızlanma çarpanı da gerekli: ortak motorda ölçülen sürtünme tabanı yüzünden
sadece üst hızı yükseltmek robotu yeni tavana yavaşça çıkarırdı.

## Ölçümler

Son koşudan (`DriveBoostTests`):

| Ölçüm | Değer |
| --- | --- |
| Normal üst hız | 6.00 m/s |
| Boost üst hız | 9.00 m/s |
| 1 saniye boost sonrası enerji | 100 → 59.2 |
| Bırakıp 2.5 saniye sonra | 96.7 |
| Hasarlı (mobility 0.5) hız | 0.24 m/s |
| Hasarlı + boost hız | 2.87 m/s |
| Hasarlı üst hız → boostlu | 3.00 → 4.50 m/s |

## Yapı

| Dosya | Sorumluluk |
| --- | --- |
| `Scripts/Robots/RobotMotor.cs` | `SetDriveModifier(speedScale, accelerationScale)` — geçici çarpan kanalı |
| `Scripts/Driving/DriveBoost.cs` | Enerji, tüketim, dolum ve çarpanın uygulanması |
| `Scripts/Driving/DriveBoostGauge.cs` | Sol alt enerji göstergesi |
| `Scripts/Driving/PlayerKeyboardDriver.cs` | Boost tuşu ve isteğin iletilmesi |
| `Scripts/Match/DrivingFeatureInstaller.cs` | MainArena bağlantısı ve maç sıfırlaması |

### Neden ayrı bir kanal

`SetDriveCondition` hasar oranları içindir: `Clamp01` olduğu için 1'in üstüne
çıkamaz ve her fizik adımında `RobotDriveMotor` tarafından yeniden yazılır.
Oraya yazılan bir boost hem sınırlanır hem de bir sonraki adımda silinirdi.
`SetDriveModifier` bu yüzden ayrı tutuldu ve hasar oranının **üstüne** çarpan
olarak biner: hasarlı robotta boost hasarı ezmez (6 × 0.5 × 1.5 = 4.5 m/s).

Varsayılan değer 1 olduğu için bu bileşeni taşımayan robotun — varsayılan botun
— motor davranışı değişmez. Çarpan `driveModifierCeiling` (3) ile sınırlıdır.

### MainArena bağlantısı

MainArena sahne dosyası bu kartın teslim alanı dışında, ama özellik ana sahnede
çalışmak zorunda. `DrivingFeatureInstaller` sahneye dokunmadan, çalışma anında
kurulur:

* `RuntimeInitializeOnLoadMethod` ile yalnızca MainArena sahnesinde devreye girer.
* Oyuncu robotunu `MatchController.Player` üzerinden bulur.
* Eksik bileşenleri ekler, var olanı tekrar eklemez — idempotenttir.
* Maç sayacı ya da maç durumu değişince `ResetState()` çağırır.

Installer Gameplay assembly'sindedir, çünkü `MatchController` orada ve
Driving → Gameplay referansı bağımlılık döngüsü yaratırdı.

## Uç durumlar

* **Enerji bitti**: boost kesilir, tuş basılı kalsa bile enerji eşiğe (5)
  ulaşana kadar yeniden başlamaz. Enerji negatife düşmez.
* **Kapasite**: dolum kapasitede durur.
* **Odak kaybı / kontrol kapanması**: istek ve çarpan bırakılır.
* **Pause**: `Time.timeScale = 0` fizik adımını durdurur, enerji donar.
* **İkinci maç**: enerji başlangıç değerine döner, aktif komut temizlenir.

## Ortak motorda bulgu — Emin'e

**Hasarlı robot pratikte hareket edemiyor.** `mobility` oranı hem üst hıza hem
ivmeye uygulanıyor; `mobility = 0.5` iken ivme 18 → 9 oluyor ve bu, ölçülen
sürtünme tabanının (11.8 m/s²) altında kalıyor. Sonuç: hasarlı robot 2 saniye
tam gazda **0.24 m/s**'e çıkabiliyor.

Kök sebep A2'de bildirilen tabanla aynı: `RobotMotor.FixedUpdate` her adımda
gövdenin gerçek hızını geri okuyor, dolayısıyla sürtünme ivmeden düşülüyor.

Tekrar adımı: `DriveBoostTests.HasarKosuluBoostlaBirlikteGecerliKalir` ölçümü
test çıktısına yazar.

Bu karta alınmadı: dosya sahipliği "hasar koşulu davranışını koru" diyor, yani
düzeltme ortak motorda ve Emin'in kararında.

Yan etki olarak boost bugün hasarın panzehiri gibi çalışıyor (hasarlı + boost
ivmesi 13.5, tabanın üstünde). Bu bilinçli bir denge kararı değil; tabanın
düzeltilmesiyle birlikte yeniden değerlendirilmeli.

## Testler

`RobotArena/Assets/Tests/Driving/PlayMode/DriveBoostTests.cs` — 10 test: aynı
koşulda hız farkı, bırakınca normal sınırın dönmesi, enerji tüketimi ve dolumu,
boş enerjide basılı tuş, kapasite sınırı, geri viteste boost olmaması,
sıfırlama, hasar koşuluyla birlikte çalışma, boostsuz motorun değişmemesi ve
tuşun sürücü üzerinden isteğe dönüşmesi.

Son koşu: **46/46 PlayMode**, **32/32 EditMode**.
