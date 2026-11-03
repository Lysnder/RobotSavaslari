# Robot hareketi — prefab ve komut bağlantısı

Bu belge ilk görevin çıktısını anlatır: robot prefab'ı, fizik gövdesi, çarpışma
hacmi, hareket bileşeni ve komutların nasıl bağlandığı.

## Dosya yolları

| Ne | Yol |
| --- | --- |
| Robot prefab'ı | `RobotArena/Assets/Prefabs/Robot.prefab` |
| Test arenası sahnesi | `RobotArena/Assets/Scenes/RobotArena.unity` |
| Hareket bileşeni | `RobotArena/Assets/Scripts/Robots/RobotMotor.cs` |
| Komut yapısı | `RobotArena/Assets/Scripts/Robots/RobotMoveCommand.cs` |
| Klavye bileşeni | `RobotArena/Assets/Scripts/Robots/KeyboardRobotInput.cs` |
| Klavyesiz örnek sürücü | `RobotArena/Assets/Scripts/Robots/RobotCommandSequence.cs` |
| Malzemeler | `RobotArena/Assets/Materials/` |
| Prefab + sahne üreten editör betiği | `RobotArena/Assets/Editor/RobotArenaSetup.cs` |

Sahne `Build Settings` listesinde ilk sırada; `RobotArena.unity` açıp Play'e
basmak yeterli.

## Prefab yapısı

```
Robot                      (pivot yerde, y = 0'a bırakılır)
├── Rigidbody              kütle 25, yer çekimi açık, Interpolate,
│                          Continuous Dynamic, X/Z dönüşü kilitli,
│                          ağırlık merkezi (0, 0.2, 0)
├── BoxCollider            boyut (1, 0.6, 1.4) — merkez (0, 0.3, 0)
├── RobotMotor             hareket bileşeni (komut alır)
├── KeyboardRobotInput     klavye komut kaynağı
├── RobotCommandSequence   klavyesiz komut kaynağı (varsayılan kapalı)
├── Gövde                  görsel küp, çarpışma hacmi yok
└── Burun                  ileri yönü gösteren görsel küp (+Z)
```

Çarpışma hacmi tek yerde, kökte durur; görsel çocuk nesnelerde collider yoktur.
Fizik gövdesi X ve Z dönüşlerine kilitli olduğu için robot köşeye sürüldüğünde
devrilip duvarın üzerinden çıkmaz; `Continuous Dynamic` çarpışma algılama ince
duvarlardan geçmeyi engeller. Bu üç ayar `RobotMotor.Awake` içinde de garanti
altına alınır, yani prefab dışında kurulan bir robot da aynı şekilde davranır.

## Komut bağlantısı

Hareket bileşeninin tüm arayüzü üç üyedir:

```csharp
motor.SetMoveCommand(forward, turn);          // -1..1 arası iki eksen
motor.SetMoveCommand(new RobotMoveCommand(1f, 0f));
motor.Stop();                                 // komutu bırakır
```

* `forward`: `1` ileri, `-1` geri, `0` gaz yok.
* `turn`: `1` sağa, `-1` sola, `0` dönüş yok.
* Komut **verildiği gibi kalır (latch)**. Sürücü bileşen komutu her karede
  yenilemeli; girdi bırakıldığında `0` göndermeli ya da `Stop()` çağırmalı.
  Robot o anda frenleyerek durur, kayarak gitmeye devam etmez.
* Okuma için: `MoveCommand`, `TurnCommand`, `ForwardSpeed`, `TurnSpeed`.

Hız, ivme ve dönüş değerleri Inspector'da ayarlanır: `maxForwardSpeed` 6 m/s,
`maxReverseSpeed` 3 m/s, `maxTurnSpeed` 180 °/s, ivme ve fren ayrı alanlarda.

### Klavye ile

`KeyboardRobotInput` aynı nesnedeki (ya da üst nesnedeki) motoru kendiliğinden
bulur; `motor` alanı boş bırakılabilir. Tuşlar Inspector'dan değiştirilebilir.

| Tuş | Komut |
| --- | --- |
| `W` / `↑` | ileri |
| `S` / `↓` | geri |
| `A` / `←` | sola dön |
| `D` / `→` | sağa dön |

Tuş bırakıldığında eksen `0` olur, yani hareket girdisi kesilir. Bileşen
kapatıldığında da (`enabled = false`) motora `Stop()` gönderilir.

Girdi Unity Input System paketiyle okunur (Player ayarlarında *Active Input
Handling* = `Input System Package`).

### Klavye olmadan

Aynı motora başka bir kaynaktan komut göndermek için klavye bileşenine hiç
gerek yok — tek şart `SetMoveCommand` çağırmak:

```csharp
// Yapay zekâ, ağ katmanı ya da bir test:
var motor = robot.GetComponent<RobotMotor>();
motor.SetMoveCommand(1f, 0.5f);   // ileri giderken sağa dön
// ...
motor.Stop();                      // komutu bırak
```

Hazır örnek `RobotCommandSequence`: süreli komutlardan oluşan bir dizi çalar.
`Play()` çağrıldığında, `overrideKeyboardInput` açıksa klavye bileşenini kapatır
ve dizi bittiğinde geri açar. `RobotArena` sahnesindeki **Robot (Senaryo)**
nesnesi bu bileşenle, klavyesiz olarak hareket eder; **Robot (Klavye)** ise
klavyeyle sürülür. İkisi de aynı prefab ve aynı `RobotMotor`.

```csharp
sequence.SetCommands(new[]
{
    new TimedRobotCommand { duration = 1.5f, forward = 1f, turn = 0f },
    new TimedRobotCommand { duration = 0.75f, forward = 0f, turn = 1f },
});
sequence.Play();
```

## Arena

`RobotArena.unity` sahnesi 20 × 20 birimlik bir zemin ve dört duvardan oluşur
(kalınlık 0.5, yükseklik 1.5). Duvarlar köşelerde üst üste biner, yani köşede
boşluk kalmaz. Zemin ve duvarlar statik; robot duvara ya da köşeye sürüldüğünde
arenanın dışına geçmez.

## Testler

Unity Test Runner (`Window > General > Test Runner`):

* **PlayMode** — `RobotArena/Assets/Tests/PlayMode/RobotMotorPlayModeTests.cs`:
  ileri/geri gitme, iki yöne dönme, komut bırakılınca durma, duvardan ve
  köşeden geçmeme, klavyesiz komutla sürme, komutların -1..1 aralığına
  kırpılması.
* **EditMode** — `RobotArena/Assets/Tests/EditMode/RobotPrefabTests.cs`:
  prefab'da fizik gövdesi, tek çarpışma hacmi ve bileşenlerin varlığı, sahnenin
  derleme listesinde olması.

Komut satırından:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe" -batchmode -projectPath RobotArena -runTests -testPlatform PlayMode -testResults playmode-results.xml -logFile -
```

## Prefab ve sahneyi yeniden üretmek

Prefab, malzemeler ve sahne elle değil `Assets/Editor/RobotArenaSetup.cs` ile
üretildi. Unity menüsünden **Robot Savaslari > Kurulum > Robot Prefab + Arena
Sahnesi Olustur** komutu aynı dosyaların üzerine yazar; kurulum değişirse
betiği güncelleyip komutu yeniden çalıştırmak yeterli.
