# Kalıcı sürüş tuş eşlemeleri

Altı sürüş eylemi değiştirilebilir ve değişiklik oyun kapanıp açıldığında
korunur. Varsayılanlar WASD + ok takımı.

## Eylemler ve varsayılanlar

| Eylem | Varsayılan | Ok yardımcısı |
| --- | --- | --- |
| İleri | `W` | `↑` |
| Geri | `S` | `↓` |
| Sola dönüş | `A` | `←` |
| Sağa dönüş | `D` | `→` |
| Boost | `LeftShift` | — |
| El freni | `LeftCtrl` | — |

## Kullanım

MainArena **Ready** durumundayken alt ortadaki **Sürüş tuşları** düğmesi. Panel
açılınca sürüş kontrolü geçici olarak kapanır; bir eylem satırına tıklayıp tuşa
basılır. `Esc` yakalamayı iptal eder.

Erişim yalnızca maç kurulmuş, durum Ready ve ayarlar paneli kapalıyken açıktır.
Maç başlarsa panel kendiliğinden kapanır ve kontrol eski durumuna döner.

## Çakışma: reddet ve sebebini göster

Üç durumda atama **yapılmaz** ve sebep ekranda yazar:

1. **Sürüşe ait olmayan tuş** — `Esc`, `Enter`, `NumpadEnter`, `R`, `Space`.
   Bunlar menü, yeniden başlatma ve silah tarafına ait.
2. **Ok tuşları** — dört yönün sabit yardımcısı oldukları için birincil eşleme
   olarak atanamaz. Bu sayede eşleme değişse bile ok davranışı korunur.
3. **Başka bir eylemde kullanılıyor** — mesaj hangi eylem olduğunu söyler:
   *"D zaten saga donus icin kullaniliyor."*

Kullanıcıya çözdürmek yerine reddetmek seçildi: tek adımda biten, kesin test
edilebilen ve oyuncunun yanlışlıkla sürüşü bozamayacağı davranış budur.

## Kalıcılık

`PlayerPrefs`, `GameSettings` ile aynı kalıpta ama **kendi anahtarlarında**:

```
Settings.Driving.Forward      Settings.Driving.TurnRight
Settings.Driving.Backward     Settings.Driving.Boost
Settings.Driving.TurnLeft     Settings.Driving.Handbrake
```

* **Maç sıfırlaması** eşlemeleri korur; yalnızca boost enerjisi ve el freni
  durumu sıfırlanır.
* **Varsayılana dön** yalnızca bu altı anahtarı sıfırlar. Bir test özellikle
  `Settings.MasterVolume` yazıp sürüş sıfırlamasının ona dokunmadığını
  doğrular.
* İkinci bir genel ayar yöneticisi kurulmadı; depo yalnızca sürüş girdisine
  aittir.

## Bozuk kayıt

| Durum | Davranış |
| --- | --- |
| Tanınmayan metin | O eylem varsayılanına döner |
| Sürüşe ait olmayan tuş kayıtlı | O eylem varsayılanına döner |
| Aynı tuş iki eylemde | **Tamamı** varsayılana döner |

Son durumda kısmi kurtarma yapılmaz: tek tek düzeltmek yeni çakışmalar
üretebilir.

## Uç durumlar

* **Yakalama sırasında oyuna komut gitmez**: panel açıkken sürücü kapalıdır,
  kapanınca eski durumuna döner. Yeni bir bastırma mekanizması icat edilmedi,
  el freni kartında test edilmiş "kontrol kapalı" yolu yeniden kullanıldı.
* **Klavye çıkarılırsa** yakalama güvenle biter.
* **İptal** eşlemeyi değiştirmez.

## Yapı

| Dosya | Sorumluluk |
| --- | --- |
| `Scripts/Driving/DriveAction.cs` | Altı eylemin listesi |
| `Scripts/Driving/DriveBindings.cs` | Saklama, çakışma kuralı, varsayılanlar |
| `Scripts/Driving/DriveBindingsScreen.cs` | Yakalama ekranı ve Ready erişimi |
| `Scripts/Driving/PlayerKeyboardDriver.cs` | Tuşların durduğu yer (`ApplyBindings`) |
| `Scripts/Match/DrivingFeatureInstaller.cs` | Erişim koşulu ve eşlemelerin uygulanması |

Eşleme ekranı maç durumunu bilmez — Driving assembly'si Gameplay'e referans
veremez. Erişimin ne zaman açık olduğunu installer hesaplayıp `AccessAllowed`
ile bildirir; bu da `MainMenuScreen`'in kullandığı gating kalıbının aynısıdır.

## Testler

* `Tests/Driving/EditMode/DriveBindingsTests.cs` — 17 test: varsayılanlar, altı
  eylemin eşlenmesi, çakışma reddi ve mesajı, sahiplenilmeyen tuşlar, ok
  tuşları, kaydın yeniden yüklenmesi, bozuk kayıt, aynı tuşun iki eylemde
  olması, sıfırlamanın yalnızca sürüş anahtarlarını etkilemesi.
* `Tests/Driving/PlayMode/DriveBindingsScreenTests.cs` — 8 test: kontrolün
  kapanıp geri gelmesi, yakalama sırasında komut gitmemesi, yeni eşlemenin
  sürücüye uygulanıp gerçekten sürmesi, çakışma reddi, `Esc` iptali, erişim
  kapanınca ekranın kapanması, varsayılana dönüş, eşleme değişse de ok
  tuşlarının çalışması.

Son koşu: **63/63 PlayMode**, **49/49 EditMode**.

İlgili: [boost](surus-boost.md) · [el freni](surus-el-freni.md)
