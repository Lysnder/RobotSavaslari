# Dövüş modülü (E1–E4) — birleşim notu

Bu belge, Eray'ın dövüş modülünü maç akışına bağlayacak kişi içindir. Modül ortak koda (RobotPrototypeController, ArenaMatchController, ortak prefab, proje ayarları) dokunmaz.

## Dosyalar

| Dosya | Görev |
|---|---|
| `MeleeAttack.cs` | Tek silahlı kısa menzilli saldırı. Tek giriş: `RequestAttack()` |
| `AttackCooldown.cs`, `AttackHitLog.cs` | Saf kurallar: bekleme süresi, saldırı başına vurulan hedef kaydı |
| `AttackOutcome.cs` | `AttackOutcome` sonuçları ve `AttackReport` |
| `LocalMatchGate.cs` | `IMatchGate` arayüzü ve sahne içi maç açık/kapalı anahtarı |
| `CombatProfile.cs` | Hasar / menzil / bekleme / pencere / yay ayar profili |
| `AttackTestTrigger.cs` | Klavyesiz saldırı isteği gönderen deneme bileşeni |
| `Testing/` | Deneme düzeneği, kabul senaryoları, deneme sahnesi kontrolü |
| `Editor/CombatChecks.cs` | Senaryoları editörden / komut satırından çalıştırır |

Deneme sahnesi: `Assets/Scenes/Dev/Eray/CombatTest.unity`
Profiller: `Assets/Scenes/Dev/Eray/Profiles/Profil_Hasar25.asset`, `Profil_Hasar40.asset`

## Kurallar

- İstek şu durumlarda reddedilir: saldıran robot yenik (`AttackerDefeated`), maç kapalı (`MatchClosed`), bekleme sürüyor (`Cooldown`).
- Kabul edilen istek bekleme süresini **o anda** başlatır ve `activeDuration` kadar süren bir saldırı penceresi açar.
- Pencere boyunca, menzil (`range`, saldırı merkezinden collider yüzeyine) ve yay (`arcDegrees`) içindeki collider'lar aranır. Hedef, collider'ın üst nesnelerindeki `RobotHealth` bileşenidir. Bu yüzden gövde ve tekerlek gibi birden çok collider aynı cana bağlanır.
- Kendi robotu hedef olamaz. Bir saldırı her hedefe en fazla **bir** kez hasar verir.
- Pencere sürerken maç kapanırsa saldırı kesilir ve artık hasar yazılmaz.
- Can ve yenilgi ortak `RobotHealth` bileşenindedir. Can 0'ın altına inmez ve `Defeated` olayı yalnız bir kez gelir.

## Oyuncu ve botun ortak saldırı girişi

```csharp
// Oyuncu girdisi (Atalay'ın giriş sistemi):
if (attackPressed) robot.GetComponent<MeleeAttack>().RequestAttack();

// Bot (rakip yapay zekâ):
var attack = GetComponent<MeleeAttack>();
if (attack.CooldownRemaining <= 0f && HedefOnumde()) attack.RequestAttack();

// Sonucu dinlemek isteyen (ses, efekt, arayüz):
attack.Resolved += (a, report) => Debug.Log(report.Describe());
attack.TargetHit += (a, target, damage) => { /* isabet efekti */ };
```

`RequestAttack()` hiçbir tuşa bağlı değildir. `AttackTestTrigger.Fire()` de aynı çağrıyı yapar.

## Maç akışına bağlantı

1. **Robot prefab'ı:** Her robota `MeleeAttack` eklenir (`RobotHealth` zaten var). İstenen profil `profile` alanına atanır.
2. **Maç izni:** Maç akışı `IMatchGate` uygular. Örneğin `ArenaMatchController : IMatchGate`, `IsOpen => !_matchFinished && !_countingDown`. Robotlar kurulurken `attack.Gate = matchController;` atanır. Sahnede `LocalMatchGate` kullanılacaksa `MeleeAttack.matchGate` alanına sürüklenmesi yeterlidir.
3. **Yenilgi bildirimi:** Maç akışı zaten `RobotHealth.Defeated` olayını dinliyor. Bu olay robot başına bir kez gelir, ek bir bağlantı gerekmez.
4. **Yeni maç:** `RobotHealth.Restore()` ile birlikte `MeleeAttack.ResetForNewMatch()` çağrılır. Bu çağrı beklemeyi, süren saldırıyı ve hedef kayıtlarını sıfırlar.

Not: Ortak `RobotPrototypeController` hâlâ kendi çarpma hasarını uyguluyor. İki sistemin birlikte mi çalışacağı, yoksa çarpma hasarının kapatılıp kapatılmayacağı birleşimde karar verilmeli.

## Geçici çözüm

Repoda ortak robot prefab'ı olmadığı için deneme sahnesindeki iki robot `CombatTestRig.CreateRobot` ile kodla kurulur. Her robot gövde ve iki tekerlek collider'ı, `RobotHealth`, `MeleeAttack` ve `AttackTestTrigger` içerir. Ortak prefab eklendiğinde bu metot prefab örneklemeye çevrilmelidir.

## Varsayılan dövüş ayarları

| Ayar | Profil_Hasar25 | Profil_Hasar40 |
|---|---|---|
| Hasar | 25 | 40 |
| Menzil | 1.5 m | 1.5 m |
| Bekleme | 1 s | 1 s |
| Saldırı penceresi | 0.15 s | 0.15 s |
| Yay | 120° | 120° |
| 100 cana gereken vuruş | 4 | 3 |

## Çalıştırma

**Deneme sahnesi:** `CombatTest.unity`'yi açın ve Play'e basın.

| Tuş | İşlev |
|---|---|
| Tab | Robot seç |
| WASD / oklar | Hareket |
| Space | Saldırı (oyuncu tuşu) |
| H | Test hasarı |
| M | Maç aç/kapat |
| P | Profil değiştir |
| R | Yeni maç |
| T | Senaryoları çalıştır |

Aynı işlemler ekrandaki düğmelerle de yapılabilir. "Oto saldırı" düğmesi klavyesiz saldırı isteğini gösterir.

**Kabul senaryoları:**
- Editörde: `RobotSavaslari > Eray > Dovus Kontrollerini Calistir` menüsü.
- Komut satırında (editör kapalıyken):

```
Unity.exe -batchmode -nographics -projectPath RobotArena -executeMethod RobotSavaslari.Combat.EditorChecks.CombatChecks.RunBatch -logFile combat.log
```

Komut, tüm senaryolar geçerse 0, biri bile kalırsa 1 ile çıkar. Sonuç satırları logda `COMBATCHECK` önekiyle yazılır.

## Senaryolar (22 adet)

- **E1:** 25 hasar yalnız hedefi 75'e indirir · aşırı hasar 0'da durur · ek hasar ikinci yenilgi bildirimi üretmez · yeni denemede her robot kendi başlangıç canına döner
- **E2:** iki collider teması tek hasar (önden ve yandan) · süren temas tek hasar · menzil dışı ıska · kendi robotuna hasar yok · bekleme sürerken tekrarlı istek reddedilir · bekleme sonrası 50 · klavyesiz tetikleyici
- **E3:** maç kapalıyken hasar yok · saldırı sürerken maç kapanırsa hasar yok · yenik robot saldıramaz · beklemeler birbirinden bağımsız · 25 hasarla 4, 40 hasarla 3 vuruş · yeni maç temiz başlar
- **E4:** menzil sınırının 0.02 m içi isabet, dışı ıska · yaklaşma açıları 0/30/±45 isabet, 90/135/180 ıska · art arda 3 maç
