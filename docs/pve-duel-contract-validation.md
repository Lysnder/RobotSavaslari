# Hazır robotlarla maç sözleşmesi · doğrulama

## Kapsam ve yöntem

Unity 6000.4.4f1, Windows x64 ve üretim MainArena sahnesi kullanıldı. Her deneme menüden seçilen robotu, mevcut maç yöneticisini ve sahnedeki botu kullandı. Test fixture'ı geçici tarihçe/tekrar dizini açar, Ready düello tercihini sonunda geri yükler. Kaynak kod ve arka arkaya uygulanan kabul kontrolleri [DuelContractFlowTests.cs](../RobotArena/Assets/Tests/Gameplay/PlayMode/DuelContractFlowTests.cs) içinde.

| Yerel senaryo | Ölçülen davranış |
| --- | --- |
| Döner oyuncu / Kama bot | Hızlı Open tek maçında her iki gövde yüz yüze 1,80 m arayla yerleştirilir. Gerçek saldırı başına oyuncu/bot 25/18 HP verir. Saldırı sonrasında iki robot ayrılır ve üretim hakeminin 60 saniyelik süre sonu çalışır. Sonuç menüsü aynı rollerle yeni karşılaşma açar. |
| Kama oyuncu / Döner bot | Open İlk 2 serisinde 18/25 HP karşılıklı vuruş ve bottaki Spinner görseli doğrulanır. İki turdaki devam eylemi, aynı karşılaşma ve kurulum nesnesini korur. İkinci galibiyette tek seri kaydı oluşur. |
| Bozuk ve yinelenen başlangıç | Bilinmeyen iki robot kimliği, yanlış rol, farklı katalog revizyonu, gelecek sürüm, bilinmeyen arena, sahneye alınmayan geçerli arena ve başka karşılaşma kimliği reddedilir. Aynı karede ikinci istek yeni maç oluşturmaz. Kabulden sonra çağıranın DTO'suna yazılan yeni kimlik, süre ve robot kilitli değeri değiştirmez. |
| Eski tarihçe | Robot ve arena kimliği içermeyen v1 tarihçe girdisi okunur. Rövanş mevcut Kama seçimiyle, dosyadaki kurallarla başlar. Eski girdinin önceki alanları korunur. |
| Sonraki ağ provası | İki Windows istemcisi, gerçek paket akışı, RTT/jitter/kayıp, kopma ve yeniden bağlanma M2 sonrasının ayrı kabul ölçümleridir. Bu koşunun bütün komutları yereldir. [SP04 sınırı](sp04-duel-contract.md) |

## Windows ve Editor sonuçları

24 Kasım 2026'da `bee96facbfcaa720f0fb58e83defe91e5c7c4f0f` üzerindeki testler Direct3D 12 Windows x64 oyununda **4/4**, Editor PlayMode'da **4/4** geçti. Sözleşme dalının EditMode koşusu **236/236** geçti.

| Kontrol | Gerçekleşen |
| --- | --- |
| Süreli tek maç | Döner oyuncu 82 HP, Kama bot 75 HP. Üretim saati 60,00 oyun saniyesinde süre dolumu verdi. Bir sonuç ve bir tarihçe kaydı oluştu. Sonuç menüsünden aynı robot ve kurallarla yeni kimlikte maç başladı. İkinci maçın bitişinde iki karşılaşma kaydı vardı. |
| İlk 2 galibiyet | Kama oyuncu / Döner bot. Skor 2-0, iki tur sonucu ve tek karşılaşma kaydı. Oyuncu ve bot aynı fizik nesneleri olarak iki turda da kaldı. |
| Girdi koruması | Sekiz bozuk veya sahneyle uyuşmayan başlangıç isteğinden sonra maç ve sonuç sayısı sıfırdı. Ardından iki başlangıç girişinden biri kabul edildi. Bir maç, bir sonuç alındı. |
| Geçmiş | Kimlik alanları içermeyen v1 girdi yüklendi. Güncel rollerle rövanş sonuçlandı. Tekrar okunan eski girdi 10 saniyelik özgün süresini korudu. |

Kontrollü isabetlerde test hedefleri yerleştirdi ve seri bitişindeki tekrarlı saldırılar arasında silahın saatini ilerletti. Süreli maçta fizik ve üretim saati dört kat oyun hızıyla çalıştı. Süreli bitiş ve serideki 2-0 skor, hakem ve kayıt akışının kabul ölçümüdür.

Gerçek 1024 × 768 Windows kareleri [Döner oyuncu](evidence/pve-contract-20261124/01-spinner-playing.jpg), [süreli maç sonucu](evidence/pve-contract-20261124/02-spinner-time-result.jpg), [Kama oyuncu](evidence/pve-contract-20261124/03-fork-playing.jpg) ve [seri sonucu](evidence/pve-contract-20261124/04-fork-series-result.jpg).

## Çalıştırma ve kanıt

Çalışma alanı kökünde aşağıdaki iki komut aynı fixture'ı çalıştırır. Ortak Windows çalıştırıcısı her koşuda Save, Restore ve Verify uygular. PlayMode ve Windows oyuncusu sonundaki denetimler altı kişisel kayıt dosyası, 46 kayıt defteri girdisi ve bir tekrar dosyasında eşleşti.

```powershell
$dir = (Resolve-Path './PvE/contract/RobotArena/Logs/PveEvidence').Path
$env:DUEL_CONTRACT_FRAMES = Join-Path $dir 'contract-frames'
& './PvE/Run-PveUnity.ps1' -Task contract -Mode PlayMode -Label contract-flow-01 -Filter 'DuelContractFlowTests'
& './PvE/Run-PveUnity.ps1' -Task contract -Mode StandaloneWindows64 -Label contract-win-01 -Filter 'DuelContractFlowTests'
```

| `RobotArena/Logs/PveEvidence` içindeki XML | SHA-256 |
| --- | --- |
| `contract-flow-01.xml` | `6e0df186f8f5bc687a163ffd0e0f7b196491a87c416efee8197740e42fa3b4ee` |
| `contract-win-01.xml` | `31a5f96e960cbe804cd5477902b463d2475859aa494ad52bc2c1a69f463d7dec` |
| `contract-edit-01.xml` | `367367f1f138148ae86087a242577e54eeabd364322b5ea1eaeec22b85a9aacb` |

Aynı dizindeki eş adlı `.log` dosyaları çalıştırıcı günlükleridir. Paketleme günlüğü `contract-winbuild-01.log`, normal sürüm `RobotArena/Builds/Windows/RobotSavaslari.exe`.
