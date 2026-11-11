# A1 · Klavyeyle güvenilir sürüş

Atalay'ın kartı. Ortak robot prefab'ı ve ortak hareket bileşeni değişmedi;
girdi kendi bileşeninde okunur ve ortak motora komut olarak gönderilir.

## Deneme sahnesi

**`RobotArena/Assets/Scenes/Tests/Driving/KeyboardDriving.unity`**

Düz bir alan, 5 metre aralıklı ölçü ızgarası, başlangıç işareti ve üzerinde
Atalay'ın girdi bileşenleri bulunan ortak robot. Duvar yok — çarpışma
senaryoları A3'ün konusu.

Sahne **derleme listesine (Build Settings) eklenmedi**: proje ayarları bu
kartın teslim alanı dışında.

## Çalıştırma adımları

1. Project panelinde `Assets/Scenes/Tests/Driving/KeyboardDriving.unity` sahnesine çift tıkla.
2. Play'e bas, Game görünümüne bir kez tıkla.
3. Sol üstteki telemetri metnini açık tut; kabul maddeleri oradan okunur.

| Tuş | Komut |
| --- | --- |
| `W` veya `↑` | ileri |
| `S` veya `↓` | geri |
| `A` veya `←` | sola dön |
| `D` veya `→` | sağa dön |
| `P` | deneme kontrolünü aç/kapat (klavye bileşenini kapatır) |

## Telemetri metni neyi ayırır

Üç satır üst üste durur:

```
Kaynak : klavye
Girdi  : ileri/geri  1.00   donus  0.00     <- bileşenin okuduğu
Komut  : ileri/geri  1.00   donus  0.00     <- motorun aldığı
Fizik  : hiz  5.84 m/s   donus    0.0 derece/s   <- gövdenin hızı
```

* **Girdi 1, Komut 0** → kontrol arızası, bağlantı kopuk.
* **Girdi 0, Komut 0, Fizik hâlâ hareketli** → komut kesilmiş, görünen şey
  fiziksel kayma. Kabul maddesindeki ayrım tam olarak budur.

## Bileşenler

Hepsi `RobotArena/Assets/Scripts/Driving/` altında, kendi assembly'sinde
(`RobotSavaslari.Driving`).

| Bileşen | İşi |
| --- | --- |
| `PlayerKeyboardDriver` | Klavyeyi okur, `motor.SetMoveCommand(forward, turn)` çağırır. |
| `DriveProbe` | Klavye bileşenini kapatıp aynı motora doğrudan komut gönderir. |
| `DriveTelemetryHud` | Girdi / komut / fizik satırlarını çizer. |
| `TestCameraFollow` | Deneme kamerası; hedefin konumunu izler, dönüşünü izlemez. |

### Komut bağlantısı

`PlayerKeyboardDriver` motoru kendisi bulur (`GetComponentInParent<RobotMotor>`),
yani robot prefab'ının köküne eklemek yeterli. Motoru koddan değiştirmek için
`driver.Motor = baskaMotor;` — eski motora önce `Stop()` gider.

Maç akışı kontrolü kapatmak istediğinde:

```csharp
driver.enabled = false;   // OnDisable motora Stop() gönderir
```

Kapalıyken okunan girdi de gönderilen komut da sıfırdır; tekrar açıldığında eski
değer kullanılmaz, o karede klavye yeniden okunur.

Bot ya da başka bir sürücü için klavye bileşenine hiç gerek yok:

```csharp
motor.SetMoveCommand(1f, 0.35f);   // ileri giderken sağa
motor.Stop();                       // komutu bırak
```

### Karşıt tuş kuralı

İleri ve geri aynı anda basılıysa ileri/geri ekseni **0**; sola ve sağa aynı
anda basılıysa dönüş ekseni **0**. İki eksen birbirinden bağımsız: `W+A+D`
basılıyken ileri komutu 1 kalır, yalnızca dönüş sıfırlanır. WASD ile oklar
birlikte kullanılabilir: `W+↑` tek ileri komutudur, `W+↓` ise sıfırdır.

## Kabul maddeleri

| Madde | Nasıl doğrulandı |
| --- | --- |
| İleri, geri, sağ ve sol ayrı ayrı çalışır | Otomatik test + sahnede elle |
| İleriyle birlikte dönüş yapılabilir, karşıt tuşlar ilgili ekseni sıfırlar | Otomatik test |
| Tuş bırakıldığında gönderilen komut sıfırdır; fiziksel kayma telemetriden ayırt edilir | Otomatik test + telemetri metni |
| Klavye kapalıyken deneme kontrolü ortak motoru sürebilir | Otomatik test + sahnede `P` tuşu |

### Otomatik testler

`RobotArena/Assets/Tests/Driving/PlayMode/PlayerKeyboardDriverTests.cs`

Sanal klavye (`InputTestFixture`) ile sürülür, gerçek tuşa basmaya gerek yok.
Test Runner > PlayMode > `RobotSavaslari.Driving.PlayModeTests`.

Testler düz ve dönüşlü sürüşü, aynı anda basılan karşıt tuşları ve iki eksenin
bağımsızlığını sınar. Tuş bırakıldığında veya kontrol kapatıldığında komutun
kesilmesi, kontrol yeniden açıldığında eski değerin kullanılmaması ve deneme
kontrolünün klavyesiz sürmesi de kapsamdadır.
Dört ek senaryo oklarla çapraz hareketi, WASD ve okların karışık kullanımını,
klavye çıkarılınca komutların sıfırlanmasını ve ortak robot prefab'ındaki
alternatif okuyucunun aynı çelişkili tuşları nasıl çözdüğünü sınar.

İlk A1 tesliminde **19/19 PlayMode** (11 A1 + 8 ortak) ve **4/4 EditMode**
geçti. Bu tarihsel sonuç, sonradan eklenen ok tuşu testlerini içermez.
Karşıt tuş kuralı ancak iki tuş gerçekten aynı anda basılıyken sınanabildiği
için testler klavye durumunu tek olayla yazar (`KeyboardState`); tuşlara sırayla
basmak ilk tuşu bırakılmış sayar.

Fizik rotaları (düz hat, dar dönüş, hızlı yön değişimi) bu testlerin kapsamında
değil; onlar A2'nin konusu ve Unity'de elle oynanır.

## Kapsam dışı bırakılanlar

* Ortak `RobotMotor`, ortak prefab ve ana Arena sahnesi değiştirilmedi.
* Proje ayarlarına dokunulmadı, sahne derleme listesine eklenmedi.
* Duvar/çarpışma senaryoları A3'e, sürüş hissi ayarları ve prefab varyantı
  A2'ye bırakıldı.
