# Maç kuralları ve karşılaşma sonuçları

Sorumlu: Emin (Lysnder). Issue #36. Taban: `test@3a7c500db7a9495bda036bac74ea2719a2c09856`.

## Oyuncu akışı

MainArena ana menüsünde Tek maç veya İlk 2 galibiyet seçilir. Raund süresi Süresiz, 60, 120 veya 180 saniye olabilir. Oyna/Enter mevcut 3–2–1 geri sayımını kullanır. Sonuçtan R, seri sürüyorsa sonraki raundu veya beraberlik tekrarını; karşılaşma bittiyse yeni maç/seriyi başlatır. Ana menü, devam eden seriyi bırakır ve skorları temizler.

Tek otorite mevcut MatchController'dır. Yeni ana sahne veya ikinci sonuç yöneticisi yoktur. Üretim sahnesi ve diğer geliştiricilerin sistemleri korunur.

## Emin tarafından onaylanan kurallar

- İlk iki galibiyete ulaşan seri kazanır: 2–0 veya 2–1. Tamamlanan seriye devam edilemez; Yeni seri ayrı ve temiz bir karşılaşmadır.
- Süre yalnız aktif raund sırasında ilerler. Geri sayım, duraklatma ve sonuç ekranı süreye dahil değildir.
- Süre sonunda `CurrentHealth / MaxHealth` oranı yüksek olan kazanır. Hesap mevcut iki sağlık verisine dayanır.
- Tam eşitlikte raund berabere biter. Uzatma yoktur.
- Seride beraberlik galibiyet sayılmaz; aynı skor raundu tekrar edilir. Bu tekrarlar, en fazla üç sonuç veren raund sınırının dışındadır. Örneğin beraberlik, galibiyet, mağlubiyet, galibiyet: dört fiziksel deneme, 2–1 seri sonucu.
- Tek maçtaki beraberlik tamamlanmış karşılaşmadır ve geçmişe yazılır.
- Ölüm ve süre bitimi aynı değerlendirme aralığında yarışırsa ilk kesinleşen geçerli sonuç otoriteyi kilitler. Zaman dolumu MatchController'ın fizik/güncelleme adımında işlenir; kilitten sonra hiçbir bildirim ikinci sonuç üretemez. Mevcut eşzamanlı yenilgi kuralı (ilk geçerli bildirim) korunmuştur.
- Time Over, Result durumunun `RoundFinishReason.TimeOver` nedenidir; mevcut replay ve inspection Result akışını kullanmaya devam eder.

## Pause

Esc veya Duraklat düğmesi mevcut SettingsPanelController'ı kullanır. Time.timeScale'ın sahibi yine bu paneldir. Bot, motor ve fizik aynı zaman kaynağında donar. MatchController kendi başlangıç/sonuç girişlerini ve ortak melee gate'lerini pause sırasında kapatır.

Oyuncu sürücüsünün önceki etkinliği saklanır. Devamda pause boyunca basılı kalan tuşlar bırakılana kadar sürüş/silah girdisi yeniden açılmaz. Botu veya Prototype'ı pause için disable etmek yoktur; hafıza ve karar durumu korunur. Cooldown sıfırlanmaz. Eğitimde MatchController devre dışıyken maçın yan akışları çalışmaz.

## Ölçülen sonuçlar

Sonuç ekranı raund süresi/numarası, galibiyet skoru, beraberlik sayısı, iki robotun kalan canı, verilen/alınan hasarı ve kopan parçalarını gösterir. Seri tamamlanınca toplam süre ve verilen hasar da görünür.

Hasar toplamı `RobotHealth.Damaged` olayındaki gerçekten uygulanan, canla kırpılmış Amount değeridir. Kaynak bilgisi RobotHit ve geriye uyumlu TakeDamage çağrısında isteğe bağlıdır. Melee ve ram, kendi saldırgan sağlık bileşenini iletir. Kaynağı bilinmeyen veya çevresel hasar alınan hasara dahildir; oyuncuya/rakibe verilmiş hasar olarak atanmaz. Ölüm vuruşu sonuç snapshot'ından önce sayılır. MeleeAttack.TargetHit'in istenen hasar sözleşmesi değiştirilmemiştir.

Raund reseti mevcut can/parça/spawn/motor/weapon temizliğini kullanır. Seri skoru raundlar arasında korunur; yeni karşılaşmada temizlenir. Sonuç olayı, skor ve istatistik snapshot'ı kesinleştikten sonra yayınlanır.

## Yerel geçmiş ve rövanş

Ana menüde Maç geçmişi açılır. En son 20 tamamlanmış karşılaşma sürümlü `match-history.json` dosyasında `Application.persistentDataPath` altında saklanır. Yarım bırakılmış seri, eğitim ve replay kayıt üretmez. Aynı sonuç kimliği iki kez eklenmez. Yazma geçici dosya ve atomik replace ile yapılır; okuma/yazma hatası oyunu durdurmaz ve UI'ya bildirilir.

Rövanş kayıttaki temel maç modu ve süre kuralını yükler; eski can, parçalar, skor ve süre geri yüklenmez. Mevcut arena/rakip seçimi kullanılır. Yeni AI profillerine bağımlılık yoktur; varsayılan bot da çalışır.

## Doğrulama

Commit sonrası 9/9 maç testi tekrar çalıştırılan uygulama SHA: `8208cc1a62e445d17fdc3dfd535eb02afc53509c`. Sonraki kanıt commit’i üretim kodunu değiştirmez.

- Unity 6000.4.4f1: tüm EditMode 195/195.
- Yeni MainArena maç PlayMode: 9/9. 2–0, 2–1, beraberlik tekrarı, oran hesabı, pause/input temizliği, ölüm/overkill kaynak atfı, disable ve kalıcı rövanş.
- Mevcut MatchRegression kontrolü geçti; mevcut CombatChecks 23/23 geçti.
- Windows x64 derlendi. Kontrollü sağlık olaylarıyla seri/time-over/pause kuralları doğrulandı. Ayrı Windows süreci aynı geçmiş dosyasını okuyup geri sayımla temiz rövanş açtı.
- Ayrıca mevcut arena-smoke Windows executable'da iki ardışık gerçek klavye/fizik/hasar maçını, tek sonuç ve reseti geçti.
- Ek eski `MenuButtonsStartAndOpenSettings` testi, sonuç düğmelerine ulaşmadan değişmeyen Ayarlar düğmesinde başarısız oldu. Yalnız MainMenuScreen taban `test@3a7c500` sürümüne alınınca aynı hata tekrarlandı; ortak GUI test düzeneği bu teslimde değiştirilmedi. İki XML kanıt dosyası eklidir. Sonuç düğmelerinin otomatik tıklama yolu bu testle doğrulanamadı.
- Diğer dört merge paketinin fiziksel olarak kaldırıldığı ayrı bir kabul çalıştırılmadı; mevcut üretim sahnesi ve ortak API’ler üzerinde doğrulandı.
- Görünür Windows oturumunda sonuç panelleri ve mevcut replay/inspection düğmeleri birlikte incelendi. Ekran görüntüleri Evidence/MatchRules içindedir.

Kanıt: `Evidence/MatchRules/validation.json`, test XML'leri, regresyon/Windows logları ve ekran görüntüleri. Kontrollü kural denemeleri, gerçek dövüş kabulünden ayrı işaretlenmiştir. Windows dağıtım dosyaları Git'e eklenmez.

Komutlar (proje yolu RobotArena):

```text
Unity.exe -batchmode -nographics -projectPath <RobotArena> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
Unity.exe -batchmode -nographics -projectPath <RobotArena> -runTests -testPlatform PlayMode -assemblyNames RobotSavaslari.Match.PlayModeTests -testResults <xml> -logFile <log>
Unity.exe -batchmode -projectPath <RobotArena> -executeMethod RobotSavaslari.Editor.BuildWindowsPlayer.Build -quit -logFile <log>
RobotSavaslari.exe -match-rules-smoke write -match-history-path <ayrı-denetim-history.json> -logFile <log>
RobotSavaslari.exe -match-rules-smoke read -match-history-path <aynı-denetim-history.json> -logFile <log>
RobotSavaslari.exe -arena-smoke -logFile <log>
```

`-match-rules-smoke` yalnız açık opt-in ile çalışır ve ayrı dosya yolu ister. Normal oyuncu akışında denetim nesnesi yaratılmaz.

## Kapsam sınırı

Diğer dört paketin raporlanan beş input/eğitim/ghost eksiği ilgili geliştiricilere bırakılmıştır. Genel kamera/sürüş ayarları, motor/AI kararları, arena geometrisi değiştirilmemiştir. RobotPrototypeController değişikliği yalnız ram hasar çağrısına kaynak ekler. Tam eski Gameplay test grubundaki önceden görülen takılma bu paket kapsamında düzeltilmemiştir.
