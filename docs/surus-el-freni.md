# El freni

Hız karşılığında daha keskin dönüş. Ayrı bir sürüş motoru yok: ortak
`RobotMotor` üzerindeki geçici çarpan kanalına açık bir manevra koşulu
uygulanır.

## Oynanış

| Tuş | İş |
| --- | --- |
| `LeftCtrl` | el freni |

`Space` silah tarafında olduğu için el freni varsayılanı `LeftCtrl`.

El freni **1.5 m/s üstünde** devreye girer. Altında devreye girmez: yerinde
sınırsız dönüş üretilemez ve duvara dayanıp hız düşünce kendiliğinden bırakılır.

## Ölçümler

Aynı başlangıç hızından (6.00 m/s) aynı sürede (1 saniye) dönüş:

| Ölçüm | Normal | El freni |
| --- | --- | --- |
| Dönülen açı | 124.3° | **222.2°** |
| Çıkış hızı | 6.00 m/s | **3.30 m/s** |

Dönüş sınırı 324 °/s (180 × 1.8). En büyük tek fizik adımı 5.71°, yani tek
karede sıçrama yok.

## Varsayılan ayarlar

| Alan | Değer |
| --- | --- |
| Dönüş çarpanı | ×1.8 → 324 °/s |
| Hız çarpanı | ×0.55 → 3.30 m/s |
| Devreye girme hızı | 1.5 m/s |

## Normal frenden farkı

* **Normal fren**: gaz bırakılır, robot sıfıra iner, dönüşe dokunulmaz.
* **El freni**: hızın bir kısmı korunur (3.30 m/s), bedel üst hızdan ödenir,
  dönüş belirgin biçimde keskinleşir.

Test ikisini aynı senaryoda yan yana ölçer.

## Hız üretmez

El freni yalnızca üst hız **sınırını aşağı** çeker; hiçbir yerde hız eklemez.
Bu yüzden duvara sıkışmış robotta hız üretmesi yapısal olarak mümkün değildir.

## Boost ile birleşim: öncelik el freninde

İkisi de aynı çarpan kanalına yazdığı için öncelik tek yerde kuruldu:

* `DriveHandbrake` boost'tan sonra çalışır (execution order 106 / 105) ve
  devredeyse boost'un yazdığı çarpanı ezer.
* `DriveBoost.HandbrakeHasPriority` el freni devreye girecekse boost'u hiç
  başlatmaz — **enerji de harcanmaz**.
* El freni bırakılınca, boost tuşu hâlâ basılıysa boost geri gelir.

Gerekçe: "keskin manevra" ile "hızlan" çelişkili isteklerdir; birini seçmek
gerekir ve manevra oyuncunun o anki acil ihtiyacıdır.

## Reset ve uç durumlar

Bırakma, odak kaybı, kontrolün kapatılması, bileşenin devre dışı kalması ve maç
sıfırlaması geçici dönüş/hız çarpanını bırakır. Pause (`Time.timeScale = 0`)
fizik adımını durdurduğu için durum dondurulur.

## Yapı

| Dosya | Sorumluluk |
| --- | --- |
| `Scripts/Robots/RobotMotor.cs` | `SetDriveModifier(..., turnScale)` — dönüş çarpanı |
| `Scripts/Driving/DriveHandbrake.cs` | Manevra koşulu, hız bedeli, devreye girme eşiği |
| `Scripts/Driving/DriveBoost.cs` | Öncelik kuralı (`HandbrakeHasPriority`) |
| `Scripts/Driving/PlayerKeyboardDriver.cs` | El freni tuşu ve isteğin iletilmesi |
| `Scripts/Match/DrivingFeatureInstaller.cs` | MainArena bağlantısı ve sıfırlama |

Yeni bir metot açılmadı: mevcut `SetDriveModifier`'a isteğe bağlı `turnScale`
parametresi eklendi.

## Testler

`RobotArena/Assets/Tests/Driving/PlayMode/DriveHandbrakeTests.cs` — 9 test:
aynı hızda dönüş kazancı ve hız bedeli, dönüş sınırı ve tek adım sınırı, sıfır
hızda devreye girmeme, hız üretmeme, normal frenden farkı, boost ile öncelik,
bırakınca temizlenme, sıfırlama ve kapatma, tuşun sürücü üzerinden isteğe
dönüşmesi.

Son koşu: **55/55 PlayMode**, **32/32 EditMode**.

İlgili: [sürüş boost'u ve enerji](surus-boost.md)
