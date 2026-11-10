# B4: Maç regresyonu ve P05B bağlantı tarifi

Burak deneme sahnesi maç başlangıcını, iki galibiyeti, tekrar/geçersiz bildirimleri,
aynı değerlendirmedeki iki yenilgiyi ve temiz yeniden oynamayı tek yerde gösterir.
Tek sonuç kilidi ve yeni maç temizliği önceki B1–B3 uygulamasını kullanır.
Bu metin PR açıklaması olarak kullanılmak üzere hazırlanmıştır; uzak PR açılmadı.

## Sahne ve çalıştırma

`Assets/Scenes/Tests/MatchRegression.unity`, Unity 6000.4.4f1.

1. Unity'yi kapatıp projeyi yeniden aç. Sahneyi aç ve Play'e bas.
2. “Yalnız deneme bildirimleri” varsayılan olarak açıktır. Mevcut ortak hareket
   bileşenleri devre dışıdır; bot/dövüş tick'leri gerekmez. İzin göstergeleri ve
   deneme girdileri maç kapısını sınar. Hareketi denemek için kutuyu kaldır.
3. Hazır'da yenilgi ve geçersiz bildirim düğmelerini dene: sonuç üretilmez.
4. Başlat'a ve tekrar Başlat'a bas: tek maç, iki robot. İzinler AÇIK.
5. Geçersiz (null/yabancı robot/eski maç) düğmesine bas: maç devam eder.
6. Rakibi yen: oyuncu kazanır. Tekrar bildirim düğmesi sonucu değiştirmez;
   sonuç sayısı 1, iki izin KAPALI kalır.
7. Yeniden başlat'a hızlı çift bas: yalnız bir yeni Oynanıyor maçı açılır.
   Sonuç metni ve sayacı temiz, canlar tam, robotlar spawn'larında olur.
8. Oyuncuyu yen: rakip kazanır. Yeniden başlat aynı davranır.
9. Yeni maçlarda iki “Aynı an” düğmesini ayrı ayrı dene: iki çağrı tek metot
   içinde aynı değerlendirmede yapılır. Mevcut geçici davranış ilk bildirimdir;
   iki geliş sırası farklı galip üretir. Bu, onaylanmış beraberlik kuralı değildir.
10. Üç tur yeniden başlat; eski maçın sonucu kendiliğinden görünmemelidir.

## Tek menüyle otomatik regresyon

Play kapalıyken `RobotSavaslari > Burak > Run B1-B4 Regression`.
Menü Burak sahnesini yeniden açar, yeni Play oturumu başlatır, Start sonrası
B1/B2 kontrollerini, iki aynı-an sırasını ve B3'ün üç ardışık yeniden başlatmasını
çalıştırır. Console'da `PASS: B1-B3...` çıktısı beklenir. Testler Editor klasöründedir;
yayın paketine girmez. Sonuç `MatchRegression.Status` üzerinden de okunabilir.

Kontrol edilenler: spawn/yön/çakışma; Hazır/Oynanıyor/Sonuç izinleri;
iki galibiyet; maç öncesi/sırası/sonrası geçersiz kimlik ve maç numarası;
tekrar ve iç içe bildirimlerde tek sonuç olayı; yeniden başlatmada 25 ek çağrının
reddi; üç maçta can/konum/sonuç/yerel bekleme/hedef temizliği. Testler aynı
sahnedeki gerçek düğmelerin çağırdığı metotları kullanır. Mevcut RobotHealth
yenilgi olayının aynı girişe ulaştığı da kontrol edilir.

## P05B uygulayıcısına bağlantı tarifi

### Gerçekleştirilen doğrulama — 9 Kasım 2026

Unity Editor kapatılıp yeniden açıldı: önceki süreç 24708, yeni süreç 31028.
Yeni süreçte `RunFresh()` üzerinden B1–B4 regresyonu PASS verdi. B3'ün üç
ardışık yeniden başlatma kontrolü de PASS. Gerçek kare sayısı 7567'den 18380'e
ilerledi. Yeni Console oturumunda hata 0, uyarı 0, derleme hatası yok.
`B4-regression.png` sahne ve senaryo düğmelerinin görsel kanıtıdır.
Eşzamanlı yenilgi yalnız mevcut davranış olarak test edildi; ekip kuralının
kabulü bekliyor. Bu nedenle bütün ekip kabul maddelerinin kapandığı iddia edilmez.

Bu kart ana Arena'yı değiştirmez. Aşağıdaki bağlantılar birleşim işinde yapılmalıdır.

| Bağlantı | Burak denemesinde | P05B'de yapılacak |
| --- | --- | --- |
| Oyuncu spawn | `Burak Player Spawn`: (-3.15, .82, 0), Y=90 | Emin'in onayladığı oyuncu spawn Transform'unu `playerSpawn` alanına bağla |
| Rakip spawn | `Burak Opponent Spawn`: (3.15, .82, 0), Y=-90 | Onaylı rakip spawn Transform'unu `opponentSpawn` alanına bağla |
| Oyuncu kimliği | `Mavi Robot` kök GameObject; `player` hareket bileşeni | Ortak prefab'ın oyuncu örneğinin kökünü kullan |
| Rakip kimliği | `Kirmizi Robot` kök GameObject; `opponent` hareket bileşeni | Ortak prefab'ın rakip örneğinin kökünü kullan |
| Gerçek yenilgi | `RobotHealth.Defeated` → `OnDefeated` → `NotifyDefeat(root, matchId)` | Gerçek can bileşenini aynı girişe bağla; child collider kimliği gönderme |
| Maç izni | `PlayerAllowed` / `OpponentAllowed` | Atalay girdisi, Eray saldırısı ve Ahmet botu kendi robotunun iznini komut uygulamadan önce sorgulasın |
| Sonuç ekranı | `ResultDetermined`, `Outcome`, `ResultText` | Sonuç olayına bir kez abone ol; kapanışta aboneliği kaldır |
| Yeni maç düğmesi | `RestartMatch()` | Aynı metodu çağır; `IsRestarting` sırasında düğmeyi kapat |

1. Burak denemesindeki controller'ı referans alarak birleşik maç yöneticisini kur.
   Aynı robotları yöneten iki maç yöneticisini birlikte çalıştırma. Ana Arena'daki
   mevcut `ArenaMatchController` değişikliği P05B/Emin kapsamındadır.
2. İki farklı robotun Rigidbody, RobotHealth ve mevcut hareket API'sini bağla.
   `Configure(player, opponent, playerSpawn, opponentSpawn)` Start'tan önce
   çağrılabilir veya Inspector'daki dört alan atanabilir. Bu bağlantıları maç
   sırasında değiştirme. Onaylı spawn'lardaki collider ayrımını tekrar doğrula.
3. Can bileşeni değişirse mevcut iki `RobotHealth.Defeated` aboneliğini yeni
   adaptöre taşı; aynı olayı iki kez bağlama. Senkron can olayı geçerli maçın
   `MatchCount` değerini kullanır. Asenkron saldırı/bot işi kimliği iş başında
   yakalamalı: `int id = match.MatchCount;` ve tamamlanınca
   `match.NotifyDefeat(defeatedRoot, id);` çağırmalıdır. Önceki maç işi yeni
   maç numarasını sonradan okumamalıdır.
4. `NotifyDefeat` yalnız Oynanıyor'da, mevcut maç numarası ve atanmış iki kökten
   biriyle kabul edilir. İlk kabulde durum kilitlenir, iki robot `StopCombat()`
   ile durdurulur; sonra tek sonuç olayı yayınlanır. İzin kapısını sadece görsel
   düğmede değil giriş, saldırı, bot ve gecikmeli iş yürütme noktasında da uygula.
5. Deneme modu birleşimde kapalı olmalı (`manualNotificationsOnly=false` veya
   `SetManualMode(false)`). Gerçek hareket/dövüş/bot bileşenleri çalışırken de
   izin kapısını koru. Yerel `DemoAttackCooldown`/`DemoBotTarget` gerçek sistem
   yerine kullanılmaz; B3 notundaki başlangıç noktalarına gerçek resetleri bağla.
6. Mevcut yeniden başlatma aynı sahnede Reset/Restore/ConfigureCombat kullanır;
   abonelikleri çoğaltmaz. Yeni can, saldırı kuyruğu/beklemesi ve bot hedefini
   komutlar açılmadan önce sıfırla. Kalıcı/asenkron yöneticiler varsa eski işleri
   iptal et; sahne yükleme yöntemi değiştirilirse maç kimliklerinin yeniden
   kullanılması riskini ayrıca ele al. Bu tarif sahneler arası kalıcı yönetici kurmaz.
7. Birleşimden sonra bu dosyadaki iki galibiyet, tekrar/geçersiz bildirim ve
   üç yeniden başlatma senaryosunu gerçek can ve botla tekrar çalıştır.

## Açık ekip kararları

- Emin eşzamanlı yenilgi kuralını henüz seçmedi. Aynı fizik adımı/kare tanımı
  ve beraberlik kararı olmadan bu senaryo ekip kabulü olarak kapatılamaz.
  Onay gelirse ilk çağrıda kesinleştirme yerine bildirimleri seçilen değerlendirme
  sınırında toplayıp iki geliş sırasının aynı beraberliği üretmesini test et.
- Ortak prefab, mevcut spawn nesneleri ve resmi başlangıç commit'i B1'de
  belgelenen eksiklerdir. Burak kopyası mevcut robot/pozlarla çalışır; P05B
  öncesinde Emin'in sağladığı ortak tabanla eşleştirilmelidir.

Windows yayın paketi ve Steam işlemleri yapılmadı; bu kartın kapsamı dışındadır.
