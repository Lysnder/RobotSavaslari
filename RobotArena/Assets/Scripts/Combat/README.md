# Yakın dövüş: maç bağlantıları ve doğrulama

Ana arenadaki oyuncu ve bot aynı yakın dövüş bileşenini kullanır. Bağımsız deneme sahnesi profilleri ve sınır durumlarını maç oynamadan incelemek içindir.

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
| `Assets/Editor/Combat/CombatChecks.cs` | Senaryoları editörden / komut satırından çalıştırır |

Deneme sahnesi: `Assets/Scenes/Tests/Combat/MeleeCombatValidation.unity`
Profiller: `Assets/Settings/Combat/StandardMelee.asset`, `HeavyMelee.asset`

## Kurallar

- İstek şu durumlarda reddedilir: saldıran robot yenik (`AttackerDefeated`), maç kapalı (`MatchClosed`), bekleme sürüyor (`Cooldown`).
- Kabul edilen istek bekleme süresini **o anda** başlatır ve `activeDuration` kadar süren bir saldırı penceresi açar.
- Pencere boyunca, menzil (`range`, saldırı merkezinden collider yüzeyine) ve yay (`arcDegrees`) içindeki collider'lar aranır. Hedef, collider'ın üst nesnelerindeki `RobotHealth` bileşenidir. Bu yüzden gövde ve tekerlek gibi birden çok collider aynı cana bağlanır.
- Kendi robotu hedef olamaz. Bir saldırı her hedefe en fazla **bir** kez hasar verir.
- Pencere sürerken maç kapanırsa saldırı kesilir ve artık hasar yazılmaz.
- Can ve yenilgi ortak `RobotHealth` bileşenindedir. Can 0'ın altına inmez ve `Defeated` olayı yalnız bir kez gelir.

## Ana arenadaki bağlantı

Her iki robotun `RobotPrototypeController` bileşeninde `weaponProfile` alanı `StandardMelee` profiline bağlıdır. Oyuncunun Space komutu ve botun saldırı kararı aynı `MeleeAttack.RequestAttack()` yolundan geçer. Silah menzil, açı, etkin vuruş süresi ve bekleme süresine tabidir. Hıza ve kütleye dayanan fiziksel çarpma hasarı ayrı hesaplanır. Yeni maçta etkin saldırı penceresi ve bekleme süresi sıfırlanır.

Deneme sahnesinde `CombatTestRig` gövde ve tekerlek collider'ları bulunan geçici robotlar kurar. İkinci profil (`HeavyMelee`) bu sahnede karşılaştırma içindir; oynanabilir iki robotun varsayılan profili aynıdır.

## Varsayılan dövüş ayarları

| Ayar | StandardMelee | HeavyMelee |
|---|---|---|
| Hasar | 25 | 40 |
| Menzil | 1.5 m | 1.5 m |
| Bekleme | 1 s | 1 s |
| Saldırı penceresi | 0.15 s | 0.15 s |
| Yay | 120° | 120° |
| 100 cana gereken vuruş | 4 | 3 |

## Çalıştırma

**Deneme sahnesi:** `MeleeCombatValidation.unity`'yi açın ve Play'e basın.

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
- Editörde: `RobotSavaslari > Combat > Run Melee Checks` menüsü.
- Komut satırında (editör kapalıyken):

```
Unity.exe -batchmode -nographics -projectPath RobotArena -executeMethod RobotSavaslari.Combat.EditorChecks.CombatChecks.RunBatch -logFile combat.log
```

Komut, tüm senaryolar geçerse 0, biri bile kalırsa 1 ile çıkar. Sonuç satırları logda `COMBATCHECK` önekiyle yazılır.

## Senaryo kapsamı

- **E1:** 25 hasar yalnız hedefi 75'e indirir · aşırı hasar 0'da durur · ek hasar ikinci yenilgi bildirimi üretmez · yeni denemede her robot kendi başlangıç canına döner
- **E2:** iki collider teması tek hasar (önden ve yandan) · süren temas tek hasar · menzil dışı ıska · kendi robotuna hasar yok · bekleme sürerken tekrarlı istek reddedilir · bekleme sonrası 50 · klavyesiz tetikleyici
- **E3:** maç kapalıyken hasar yok · saldırı sürerken maç kapanırsa hasar yok · yenik robot saldıramaz · beklemeler birbirinden bağımsız · 25 hasarla 4, 40 hasarla 3 vuruş · yeni maç temiz başlar
- **E4:** menzil sınırının 0.02 m içi isabet, dışı ıska · yaklaşma açıları 0/30/±45 isabet, 90/135/180 ıska · art arda 3 maç
