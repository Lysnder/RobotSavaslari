# H1–H4: Rakip robot takibi, saldırı niyeti ve takılmadan çıkış

Arena'daki eski bot yalnızca hedefe dönüp ileri sürüyordu. Hedef kaybında komutu
temizleme, menzil kararlılığı ve duvardan çıkma davranışı yoktu. Yeni
`RobotBotController`, ortak `RobotDriveMotor` üzerinden takip eder, öndeki ve
menzildeki hedef için saldırı isteği üretir ve ilerleme yoksa geri çıkıp yön
değiştirerek takibe döner. `RobotPrototypeController` oyuncu girişi ve mevcut
temas dövüşü için adaptör olarak kalır; iki robot da aynı fizik motorunu kullanır.

Bu metin PR açıklaması ve birleşim teslimidir. GitHub CLI oturumu bulunmadığından
uzak PR açılmadı. Çalışma dalı: `feature/rakip-robot-davranis`.

Arena için eklenen saldırı ritmi ve spin döngüsünden çıkış revizyonu:
[Tactics-Validation.md](Tactics-Validation.md). H1–H4 motor laboratuvarında bu
taktik katmanı kapalıdır; Arena'da yeni oluşturulan botta açıktır.

## Deneme sahnesi

`Assets/Scenes/Tests/AI/BotScenarios.unity`, Unity **6000.4.4f1**.

1. Sahneyi tek başına aç ve Play'e bas. Laboratuvar botunda oyuncu kontrolü,
   RobotHealth veya gerçek dövüş bileşeni bulunmaz.
2. Ekrandan **Önde / Yanda / Arkada** senaryolarını seç. Bot önce döner,
   sonra yaklaşır. Sarı işaret robotun önüdür.
3. **Hareketli** hedefi seç. Varsayılan koşu 540 oyun saniyesidir; sonunda maç
   kapısı kapanır. Süre `Scenario selector > Soak Seconds` alanından değişir.
4. Otomatik hedefi kapat; ok tuşlarıyla veya Scene görünümünde `Target - move me`
   nesnesini taşı. `Bot > Target` alanına başka bir Transform atayabilirsin.
5. **Menzil sınırı** hedefi giriş mesafesinin çevresinde ±0,12m oynatır.
   İki mesafe sürgüsünü değiştir; ileri/dur komutu her sınır geçişinde değişmemelidir.
6. Hedefi yana/arkaya taşı: saldırı sayacı artmamalıdır. Öne getir: artmalıdır.
   Hedefi takibe dönüş mesafesinin ötesine taşı: bot tekrar yaklaşmalıdır.
7. **Hedef kaybı** 2. saniyede hedef atamasını kaldırır, 4. saniyede geri verir.
   Düğme ile de kaldırıp geri verebilirsin. Hierarchy'den hedefi devre dışı bırakmak
   veya silmek de komutu keser; silinen nesne yerine Inspector'dan yeni hedef ata.
8. **Açılı duvar / Köşe** aynı başlangıç noktalarını yeniden kurar. İleri sürüşte
   ilerleyemeyen bot geri çıkar, döner, kısa kaçış sürüşü yapar ve hedefe yönelir.
   Köşeyi aynı düğmeyle en az üç kez yeniden başlat.
9. Manevra sırasında **Maçı durdur** düğmesine bas. Komutlar sıfırlanır.
   Devam ettir veya yeniden başlat: eski manevra sürdürülmez.
   **Maç sonu** senaryosu 1,5. saniyede otomatik durdurur.

Ekran karar durumunu, son sürüş/dönüş komutunu, düzlemde hedef mesafesini,
açıyı, saldırı istek sayısını, son istek zamanını ve toplam toparlanma sayısını
gösterir. Toparlanma sayacı oturum toplamıdır; senaryo değişiminde sıfırlanmaz.
`BotAttackProbe` sayacı senaryo değişiminde sıfırlanır. İstek sayısı vuruş sayısı değildir.

## Varsayılan ayarlar

Mesafeler robot merkezleri arasında, XZ düzleminde metredir.

| Ayar | Laboratuvar | Arena adaptörü |
| --- | ---: | ---: |
| Saldırı menzili | 1,65 | 1,85 |
| Takibe dönüş mesafesi | 2,10 | 2,30 |
| Menzilin içine durma payı | 0,15 | 0,30 |
| Saldırı ön açısı | 22° | 22° |
| İleri sürebilmek için açı | 55° | 55° |
| Tam dönüş komutu açısı | 40° | 40° |
| Takılma gözlem süresi | 0,90s | 0,90s |
| Anlamlı ilerleme | 0,12m | 0,12m |
| Geri çıkma | 0,65s | 0,65s |
| Kaçış dönüşü | 0,40s | 0,40s |
| Kaçış sürüşü | 0,85s | 0,85s |

Giriş mesafesi durma payı kadar içeriden yakalanınca duruş kilitlenir;
takibe dönüş mesafesi aşılınca kilit açılır. Bu bantta bot bekleyebilir;
saldırı isteği ise **yalnız gerçek saldırı menzili ve ön açı koşulu sağlanınca**
üretilir. Durma payı, temasın hedefi birkaç santimetre itmesiyle isteklerin
tamamen kesilmesini azaltır. Arena'ya özgü değerler ilk bot oluşturulurken
atanır; Inspector'da önceden eklenmiş botun değerleri korunur.

Motorun mevcut varsayılanları: ivme 26, ileri hız 5,2m/s, geri hız 4m/s,
dönüş 300°/s, fren 34, yan tutuş 28. Hasarlı tekerleklerin hareket, dönüş ve
direksiyon çekmesi katsayıları aynı motor tarafından uygulanır.

## Birleşim sözleşmesi

Botun gerektirdikleri: `Rigidbody`, collider ve `RobotDriveMotor`. `RobotHealth`
varsa bot kendi yenilgisini ve hedefin yenilgisini de gözetir. Yoksa laboratuvar
tek başına çalışır. Atalay'ın yeni oyuncu kontrol bileşenine bağımlılık yoktur.
Oyuncu bileşeni de `motor.Submit(ileri, donus)` çağırabilir; değerler [-1, 1].
Bir robot üzerinde aynı anda tek sürüş üreticisi açık tutulmalıdır.

Fizik sırası: senaryo hedefi -50, bot 0, eski oyuncu/dövüş adaptörü 20,
ortak motor 100. `Submit` komutu sadece bir fizik adımı için geçerlidir;
yeni komut gelmezse motor frenler. `Clear(true)` komutu ve hızı hemen temizler.

Gerçek dövüşe bağlantı örneği (aşağıdaki `combat` ekibin dövüş bileşenidir):

```csharp
// OnEnable / OnDisable içinde simetrik bağla/çöz:
bot.AttackRequest += HandleAttackRequest;
bot.AttackRequest -= HandleAttackRequest;

void HandleAttackRequest(RobotBotController source, Transform target)
{
    // Gerçek API'ye uyarla: hasar, cooldown ve silah uygunluğu combat'a aittir.
    combat.RequestAttack(target);
}

// Maç başlatılırken, spawn ve sağlık sıfırlandıktan sonra:
bot.Target = player.transform;
bot.SetMatchRunning(true);

// Hazır, sonuç, iptal veya duraklatma durumuna geçerken:
bot.SetMatchRunning(false);
```

Olay uygun her **FixedUpdate** adımında üretilir; bot saldırı cooldown'u
uygulamaz. `BotAttackProbe` bunun hasarsız örnek alıcısıdır. Eray'ın dövüş
bileşenine geçilirken probe kaldırılabilir. Mevcut Arena adaptörü olayın aynı
adım için ürettiği `AttackRequested` bayrağını silah isteği olarak tüketir;
`DealContactDamage` içindeki gerçek temas ve cooldown kuralları çalışmaya devam eder.

`ArenaMatchController` zaten iki robota `ConfigureCombat` / `StopCombat` çağırır.
Bu çağrılar artık botu da başlatır/durdurur. `RestartMatch` manevra durumunu temizler.
`RobotPrototypeController` devre dışı bırakılırsa ona ait bot da kapanır;
Burak'ın yalnız bildirim kullanan denemeleri sürüş üretmez. Ortak prototip tamamen
kaldırılınca yeni maç yöneticisi yukarıdaki `SetMatchRunning` kapısını doğrudan
çağırmalıdır. `Time.timeScale = 0` tek başına bu sözleşmenin yerine geçmez.

## Tekrarlanabilir doğrulama

`BotScenarios` Play modundayken **Robot Savaslari > Bot > Run H1-H4 Checks
(Play Mode)** menüsünü çalıştır. Alternatif C# girişi:

```csharp
RobotSavaslari.Editor.BotScenarioChecks.RunInPlayMode();
```

Test, gerçek ortak motoru ve Unity Rigidbody/collider fiziğini 0,02s sabit
adımla sürer. Çalışırken otomatik bileşen güncellemelerini kapatır, fizik
simülasyon modunu ve interpolation ayarını sonunda geri yükler. Sahneyi ön
hedef senaryosuna döndürür. Hedef hareketi, köşe ve menzil değerleri aynıdır;
540 saniyelik koşu **hızlandırılmış simülasyondur**, gerçek zamanlı 9 dakikalık
gözlem olarak sunulmaz. Test yalnız laboratuvar sahnesinde çalıştırılmalıdır.

Son doğrulama sonuçları ve görsel kanıt `H1-H4-Validation.md` dosyasındadır.

## Kapsam ve sınırlar

Bu bot genel yol bulma kullanmaz. Sınanan L köşesinden ve açılı duvardan çıkar;
keyfi labirentler veya tamamen kapalı hedefler için NavMesh/engel algısı gerekir.
Maç süresi ve hasar kararı botun sorumluluğu değildir. Histerezis bandında
beklemek normaldir; menzil dışına saldırı gönderilmez. Yeni robotların fiziksel
ölçüleri değişince merkez mesafeleri ve durma payı yeniden ayarlanmalıdır.
