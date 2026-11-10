# A4 · Sürüş kabul seti ve bağlantı teslimi

## Kabul sahnesi

**`RobotArena/Assets/Scenes/Atalay/A4_SurusKabul.unity`**

A2 ve A3'ün sekiz rotası tek sahnede, `Tab` ile sırayla:

| # | Rota | Geldiği kart |
| --- | --- | --- |
| 1 | Düz hızlanma | A2 |
| 2 | Kısa zikzak | A2 |
| 3 | Dar dönüş | A2 |
| 4 | Geri çıkış | A2 |
| 5 | Önden çarpışma | A3 |
| 6 | Açılı çarpışma | A3 |
| 7 | Köşeye sürme | A3 |
| 8 | Duvar boyunca | A3 |

Her rotanın ne beklediği sahnede yazıyor: seçili rotanın açıklaması sol üstteki
telemetrinin altında görünür. Başlangıç noktaları sarı, kapılar mavi işaretli.

### Çalıştırma

1. `A4_SurusKabul` sahnesini aç, Play'e bas, Game görünümüne tıkla.
2. `Tab` ile rotayı seç, `R` ile baştan al.
3. `1` / `2` ile ayar setini değiştir, `C` ile kontrolü kapat/aç, `P` ile
   klavyesiz deneme kontrolünü çalıştır.

Sahne **derleme listesinde değil**; Project panelinden açılır.

## Otomatik kabul seti

Girdi kuralları, ölçümler ve çarpışma davranışı otomatik doğrulanır; fizik
rotaları Unity'de ayrıca oynanır.

| Takım | Dosya | Test |
| --- | --- | --- |
| Girdi kuralları | `PlayerKeyboardDriverTests.cs` | 11 |
| Sürüş hissi ölçümleri | `DriveFeelMeasurementTests.cs` | 5 |
| Çarpışma ve girdi güvenliği | `CollisionRecoveryTests.cs` | 7 |
| Teslim sözleşmesi (EditMode) | `AtalayDeliveryTests.cs` | 7 |

Son koşu: **32/32 PlayMode** (24 Atalay + 8 ortak) ve **11/11 EditMode**
(7 Atalay + 4 ortak).

Komut satırından:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe" -batchmode -nographics -projectPath RobotArena -runTests -testPlatform PlayMode -testResults playmode.xml -logFile -
```

## Bağlantı: oyuncu girdisi nereye eklenir

`PlayerKeyboardDriver` robot prefab'ının köküne eklenir ve motoru kendisi bulur:

```csharp
// Robot prefab'inin kokunde, RobotMotor'un yaninda:
var driver = robot.AddComponent<PlayerKeyboardDriver>();
// motor alani bos birakilabilir; Awake GetComponentInParent<RobotMotor>() ile bulur.
```

## Bağlantı: maç akışı kontrolü nasıl kapatır

```csharp
// Mac bitti, tur arasi ya da robot devre disi:
driver.enabled = false;   // OnDisable motora Stop() gonderir, komut birakilir

// Tur basladiginda:
driver.enabled = true;    // eski deger kullanilmaz, o karede klavye yeniden okunur
```

Bot ya da ağ sürücüsü için klavye bileşenine hiç gerek yok, motorun arayüzü
yeterli:

```csharp
motor.SetMoveCommand(1f, 0.35f);   // ileri giderken saga
motor.Stop();                       // komutu birak
```

## Varsayılan ayarlar

Teslim edilen varsayılan `Robot_AyarA_Sabit`:

| Alan | Değer |
| --- | --- |
| Üst hız / geri hız | 5 / 2.5 m/s |
| İvme / fren | 20 / 30 |
| Dönüş hızı | 140 °/s |
| Dönüş ivmesi / freni | 520 / 900 |

Tuşlar: `W` `S` ileri-geri, `A` `D` dönüş, yön tuşları da aynı işi yapar.

## Bilinen kusurlar

1. **Sürtünme tabanı (ortak motor).** İvmesi ~11.8 m/s²'nin altındaki ayarlar
   robotu üst hızına çıkaramaz; kullanılabilir ivme = ayarlanan ivme − 11.8.
   Bu yüzden ayar setlerinin alt sınırı 20'de duruyor ve ağır robot hissi
   kurulamıyor. Ölçüm ve tekrar adımları
   [A2 belgesinde](a2-surus-hissi.md#ortak-motorda-bulgu--emine), düzeltme
   ortak motorda olacağı için Emin'e bırakıldı.
2. **Dönüş sürtünmeden etkilenmez.** Dönüş hızı bileşenin kendi durumunda
   tutulduğu için robot duvara dayalıyken bile komuta göre dönmeye devam eder.
   Çarpışma gövdeyi fiziken durdurur ama dönüş komutu kesilmez.
3. **Kapılar yalnızca görsel.** Rota kapılarının çarpışma hacmi yok; "rota
   tamamlandı" kararı gözle verilir, otomatik sayılmaz.
4. **Odak geri çağrısı yalnızca oyun penceresi için.** Editörde Game görünümü
   odağı kaybetmeden Scene görünümüne geçerseniz `OnApplicationFocus`
   tetiklenmez; bu durumda girdi zaten Game görünümüne gitmediği için robot
   komut almaz, ama telemetride kaynak hâlâ `klavye` görünür.

## Kartların teslim alanı

Dört kartın hiçbiri ortak `RobotMotor`'a, ortak robot prefab'ına, ana Arena
sahnesine ya da proje ayarlarına dokunmadı. Bütün kod
`RobotArena/Assets/Scripts/Atalay/`, bütün sahneler
`RobotArena/Assets/Scenes/Atalay/`, ayar setleri ortak prefab'ın varyantları
olarak `RobotArena/Assets/Prefabs/Atalay/` altında. EditMode testi deneme
sahnelerinin derleme listesine girmediğini her koşuda doğruluyor.
