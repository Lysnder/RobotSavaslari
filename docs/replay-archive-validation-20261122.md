# REPLAY-02 doğrulama — 22 Kasım 2026

Issue [#45](https://github.com/Lysnder/RobotSavaslari/issues/45) · dal `feature/match-replay-archive` · hedef `test`.

Başlangıç `test` ucu `199f6ec`. Son test edilen kaynak `4376d596ec45efd5dc6e7a0c796b950d10ae3604`. Windows build'i, normal oyun kabulü ve test-player ölçümü `91a8daf2abf4599988ae35fa52af3dbc9840968a` üzerinde yapıldı; `4376d59` ondan yalnız bir test log satırının metniyle ayrılır (`git diff --stat 91a8daf 4376d59`: 1 test dosyası, 1 satır; `Scripts/` farkı yok). Bu belge ayrı commit'tir. Saatler UTC. Unity `6000.4.4f1 (360f97ecca93)`, Windows 10.0.26200, AMD Ryzen 5 5600X, 16 GB RAM, RTX 3070 Ti, sistem diski Samsung 960 EVO NVMe. Graphics açık; `-nographics` kullanılmadı.

## Eklenen davranış

| Madde | Gözlenebilir çıktı |
|---|---|
| 02.1 | Tek maç başlangıçtan sonuca, ilk-iki serisi ilk raunddan `EncounterComplete`'e kadar 20 Hz kaydedilir. Fizik/hasar yeniden simüle edilmez. Son 5 sn tekrarı (`MatchReplayRecorder/Player`) değişmedi. 128 iz, dosya başına 128 MiB (4 MiB dizin payı ayrılır), en çok 64 raund ve 4096 darbe olayı. Sınıra varınca kayıt kesilmez; arşiv "Eksik: iz/dosya/olay sınırı" olarak işaretlenir, maç sürer. |
| 02.2 | İki robotun bütün mesh renderer pozları ve görünürlüğü, aşınma (`_Wear` her karede, darbe izleri her parçanın başında; darbeden sonra yeni parça açılır), kopan parçanın debris uçuşu (parça başına bir debris izi), mevcut bariyerin hareketi/uyarısı, konveyör ve engel görselleri. Arena hücre snapshot'ı, layout adı ve malzeme adı saklanır. Anahtarlar `robot/<robot adı>/<hiyerarşi yolu>`, `debris/...`, `layout/<sıra>/<bilinen nesne adı>`; instanceID veya dosya yolu yok. Oynatmada proxy'ler MainArena'daki canlı robotun aynı yoldaki mesh'inden ve arenanın kendi malzemesinden üretilir. |
| 02.3 | `RSRA` başlığı, şema 1, içerik revizyonu 1, GUID, nullable encounterId, raund kimlikleri, süre, örnekleme hızı, iz tablosu, parça dizini (offset/uzunluk/CRC-32) ve tamlık bayrakları. Kayıt `<guid>.tmp`'ye akar; dizin yazılıp `Flush(true)` sonrası tek `File.Move` ile `<guid>.rpl` olur. NaN/Infinity, negatif süre, aşırı sayı, 128 MiB üstü dosya, bilinmeyen/gelecek sürüm, bozuk CRC ve beyaz liste dışı anahtar yüklenmeden reddedilir. |
| 02.4 | Ready menüsünde sağ üst ikinci satırda "Tekrar arşivi" (720p referansta y=60–96). Modal liste: başlık, tarih, süre, raund, arena, olay sayısı, boyut, tamlık, uyumluluk, maç geçmişi bağlantısı. Aç / Sil (ikinci tıkla onay). Kota 10 kayıt / 512 MiB; doluysa yeni kayıt `pending/` içinde bekler, oyuncu kayıt silince arşive alınır. Eski kayıt hiçbir zaman kendiliğinden silinmez. |
| 02.5 | Olaylar: raund başlangıcı, darbe (`RobotHealth.Damaged`), parça kopuşu (`RobotDamageVisuals.Parts[i].Detached` geçişi), raund sonu, karşılaşma sonu. Zaman, sonra geliş sırasıyla sıralanır. Zaman çizelgesinde renkli işaretler, sağda tıklanınca o ana giden olay listesi, önceki/sonraki olay. |
| 02.6 | Oyuncu / Rakip / Düello hedefi, mevcut kameranın sağ tık orbit ve tekerlek zoom'u, 1x/0.5x/0.25x, duraklat, "incele" (duraklatıp robotu yakından gösterir, o andaki parça durumunu listeler). Arama en yakın parçadan okur; bellekte en çok iki çözülmüş parça durur. |
| 02.7 | Arşiv yalnız normal Ready menüsü görünürken açılır: maç kurulu, etkin, duraklatılmamış; ayar, tuş atama, eğitim, geçmiş paneli ve başka sunum kapalı. Açıkken `MainMenuScreen`, `DrivingFeatureInstaller`, `TimeTrialHud`, `DriveBindingsScreen`, `PlayerKeyboardDriver`, `DriveBoostGauge` kapatılır, kapanışta önceki enabled değerleri geri verilir. Canlı robotlar, debris ve layout `MatchPresentationScope` ile gizlenir/geri verilir; kamera modu, yaw/pitch/mesafe ve GUI odağı eski haline döner. `MatchReplayPlayer.Begin` ve Result varsayımı değişmedi. |
| 02.8 | Bulunamayan robot/yol, değişen mesh, farklı içerik revizyonu, desteklenmeyen arena içeriği "Uyumsuz", bozuk parça "Bozuk" görünür ve oynatılmaz; sahnede hiçbir şey değişmez. Bilinmeyen layout içeriği (ör. Pulse/Sweep) kaydı başlatmaz ve sonuç ekranında "Tam kayıt yok: arena içeriği desteklenmiyor" yazar. Tehlike paketi import edilmedi. |

## Kullanılan mevcut API'ler

`MainArenaPresentationInstaller.Install`, `MatchPresentationScope.Enter/Exit`, `RobotVisualProxyFactory.CreateProxy/Hide/Restore/CollectMeshRenderers/IsVisible/CreateRoot`, `CombatCameraController.SetPresentationTargets/ClearPresentationTargets/SetMode/SetOrbit`, `MatchReplayClip.Pose`, `MatchController.ResultDetermined/LastRound/LastMatch/EncounterComplete/MatchCount/RoundNumber/PlayerWins/OpponentWins/DrawnRounds/Rules/HistoryStore`, `RobotHealth.Damaged`, `RobotDamageVisuals.Parts`, `ArenaEnvironment.Layout/Contents`, `DrivingFeatureInstaller.TimeTrial`, `TimeTrialSession.State`, `DriveBindingsScreen.IsOpen`, `SettingsPanelController.IsOpen`.

`MainMenuScreen.IsShowingReadyMenu` başlangıç kaynağında yoktu (ARENA-02 ekledi). Paket ona derleme bağımlılığı kurmaz: varsa yansıma ile okur, yoksa aynı sorgunun geçmiş paneli koşulunu okur. Emin'e öneri: bu sorgu ortak API olarak kalıcılaşırsa yansıma kaldırılabilir. Paylaşılan dosyalarda değişiklik gerekmedi.

## Değişen dosyalar

- Yeni: `Scripts/Presentation/MatchReplayArchive.cs` (biçim, anahtar beyaz listesi, CRC), `MatchReplayArchiveStore.cs` (yazıcı, akışlı okuyucu, klasör/kota), `MatchReplayArchiveRecorder.cs`, `MatchReplayArchivePlayer.cs`, `MatchReplayArchiveScreen.cs` ve Unity'nin ürettiği `.meta` dosyaları.
- Değişen: `MainArenaPresentationInstaller.cs` (idempotent arşiv bileşenleri, `UseArchiveStore`), `MatchPresentationScope.cs` (isteğe bağlı ek gizlenecek renderer listesi, `BehaviourEnableScope`).
- Testler: `Tests/Presentation/EditMode/MatchReplayArchiveTests.cs`, `Tests/Presentation/PlayMode/MatchReplayArchivePlayModeTests.cs`.
- Kabul araçları: `docs/windows-acceptance/Preserve-ReplayArchiveUserData.ps1` (ayar/tuş/rekor/geçmiş + `replays/` yedek/geri yükleme/doğrulama), `Drive-ReplayArchiveAcceptance.ps1` (Windows SendInput ile oyun penceresine tıklama/tuş, ekran görüntüsü).
- Mevcut `.meta`/GUID, MainArena YAML, prefab, GameSettings, RobotHealth, asmdef ve ortak test yardımcıları değişmedi.

Editor oturumları ve test koşuları oyuncunun arşivini doldurmasın diye Editor'da varsayılan klasör `RobotArena/Library/RobotSavaslariReplays`'tir. Oyuncu build'i `persistentDataPath/replays` kullanır. Teknik testler her seferinde ayrı geçici klasör kullanır.

## Disk biçimi ve bütçe gerekçesi

Ölçüm (`BinaryFramesAreSmallerThanJson`): 128 izlik bir kare ikili biçimde 2.820 B, JsonUtility ile 29.988 B. 15 dakika 20 Hz'de ikili 48,4 MiB, JSON 514,8 MiB. JSON dosya bütçesine sığmadığı için ikili seçildi. Yeni paket eklenmedi.

Bir poz 22 bayttır: konum 3×float, dönüş 4×int16, görünürlük, aşınma. Gerçek MainArena kaydı (Open layout) **115/128 iz** kullanır: 104 robot/debris, 11 arena; karede 106 dinamik iz, 40 aşınma izi.

## Otomatik sonuçlar

| Kaynak | Kontrol | Sonuç | Yerel kanıt |
|---|---|---|---|
| `4376d59` | Unity EditMode, bütün proje | 224/224 geçti, 0 atlandı. 19 yeni arşiv testi dahil | `final-editmode.xml/.log` |
| `4376d59` | Unity PlayMode, bütün proje (graphics açık) | 242/242 geçti, 0 atlandı. 13 arşiv, 9 `MatchRulesPlayModeTests`, mevcut replay/kamera/inceleme testleri dahil | `final-playmode.xml/.log` |
| `91a8daf` | Windows x64 oyuncu build'i (`BuildWindowsPlayer.Build`) | Başarılı. Kimlik `sha256:7A78B38A87CE4D3079B295D7ABB1FFDDCDF6214EA4FEC4CF6FD062A8ACDFF9D0`, 193 dosya | [build-hashes.json](evidence/replay-archive-20261122/build-hashes.json), `windows-build-final.log` |
| `91a8daf` | Windows x64 test player: iz bütçesi + 0 B örnekleme, 5/15 dk kayıt, açık/kapalı yığın karşılaştırması | 4/4 geçti | `standalone-perf.xml/.log` |
| `91a8daf` | Windows build'inde normal oyun kabulü (iki süreç) | Geçti, aşağıda | [ekran görüntüleri ve adım logu](evidence/replay-archive-20261122/) |

Yeni testler: biçim gidiş-dönüşü, parça ve raund sınırı, rastgele arama (en çok iki parça okuma, sabit bellek), darbe izi tazeleme, aynı zamanlı olay sırası, olay sınırı, 128 MiB dosya sınırı, gelecek/bilinmeyen sürüm, NaN/negatif/aşırı sayı, beyaz liste dışı anahtar, bozuk dizin/parça, 128 MiB üstü dosya, yarım dosya, GUID dışı dosya ve yol aşımı reddi, kota ve bekleyen kayıt, yeni store ile yeniden okuma, JSON/ikili ölçümü. MainArena'da: tek maç, seri, yarıda bırakılan seri, maç geçmişi bağlantısı ve geçmiş silinince açılma, izin kapıları (ayar, tuş atama, geçmiş paneli, eğitim, başka sunum, Playing/Result, kapalı maç), 10 aç/kapat döngüsü (menü/eğitim/tuş/sürüş/gösterge/renderer/kamera/odak/nesne sayısı), ayar duraklatması, disable ve Ready dışına çıkış, uyumsuz/bozuk kayıt, mevcut bariyer/konveyör izleri, IMGUI ile giriş düğmesi ve onaylı silme, gerçek sahne iz sayısı ve 0 B örnekleme, 5/15 dk kayıt, kayıt açık/kapalı yığın karşılaştırması.

İlk tam PlayMode koşusunda (`b8cdd91`) üç test kaldı. İkisi bu paketin hatasıydı ve düzeltildi: float saat 15 dakikada örnek kaçırıyordu (double saate geçildi); ham Editor yığın artışı kaydediciye yanlış atfediliyordu (açık/kapalı karşılaştırmasıyla değiştirildi). Üçüncüsü `TestHarnessPlayModeTests.MeleeTestSceneDrivesEveryActionFromTheKeyboard` boş test sahnesinde sanal klavyeyle çalışır, MainArena'yı ve arşiv bileşenlerini yüklemez. Tek başına hem `199f6ec` tabanında hem dalda 6/6 geçti, son tam koşuda da geçti; mevcut kararsız test olarak not edildi.

Kabulde bulunan hata: onaylı silme satır döngüsü içinde listeyi yeniden kurunca `ArgumentOutOfRangeException` oluşuyordu. Satırlar kopyadan çizilerek düzeltildi (`91a8daf`). `PanelButtonsOpenTheArchiveAndDeleteAfterConfirmation` düzeltme geri alınınca aynı istisnayla kalır, düzeltmeyle geçer.

### Çalıştırma yolu

`$project` = `RobotArena`, `$evidence` = proje dışı kayıt klasörü.

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe'
& $unity -batchmode -projectPath $project -runTests -testPlatform EditMode -testResults "$evidence/final-editmode.xml" -logFile "$evidence/final-editmode.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform PlayMode -testResults "$evidence/final-playmode.xml" -logFile "$evidence/final-playmode.log"
& $unity -batchmode -quit -projectPath $project -executeMethod RobotSavaslari.Editor.BuildWindowsPlayer.Build -logFile "$evidence/windows-build-final.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter "RealSceneFitsTheTrackBudgetAndSamplingDoesNotAllocate|FiveAndFifteenMinuteRecordingsStayWithinBudget|RecordingAddsNoManagedHeapGrowth" -testResults "$evidence/standalone-perf.xml" -logFile "$evidence/standalone-perf.log"
```

## Performans

Bütçeler tasarım önerisiydi; aşağıdakiler ölçümdür. Ryzen 5 5600X, 16 GB, Samsung 960 EVO NVMe, Windows 10.0.26200, Unity 6000.4.4f1. Gerçek MainArena kaydı: 2 robot, 115 iz (karede 106), 20 Hz. 5/15 dakikalık koşular gerçek kaydediciyi `Time.captureDeltaTime = 0,05` ile çalıştırır (her kare bir örnek); robotlar kinematik ve ayrı tutulur. Örnekleme ölçümü son 512 örnek, ısınma 40 örnek; arama 200 rastgele nokta.

| Ölçüm | Editor | Windows x64 test player | Bütçe |
|---|---:|---:|---:|
| Örnekleme p50 / p95 / maks (gerçek sahne) | 0,032 / 0,054 / 0,285 ms | 0,037 / 0,062 / 0,291 ms | p95 ≤ 2 ms |
| Isınma sonrası örnek başına GC.Alloc | 0 B (`Is.Not.AllocatingGCMemory`) | 0 B | 0 B |
| 15 dk: kare / dosya / parça | 18.001 / 42,4 MiB / 901 | 18.001 / 42,4 MiB / 901 | ≤ 128 MiB |
| 15 dk örnekleme p95 | 0,140 ms | 0,150 ms | ≤ 2 ms |
| 15 dk arama p50 / p95 / maks | 0,369 / 0,712 / 0,949 ms | 0,385 / 0,744 / 0,913 ms | p95 ≤ 200 ms |
| 15 dk açma + bütün CRC doğrulaması | 201 ms | 219 ms | — |
| 5 dk: kare / dosya / arama p95 | 6.001 / 14,1 MiB / 0,511 ms | 6.001 / 14,1 MiB / 0,524 ms | — |
| Kaydedicinin tuttuğu bellek / okuyucu belleği | 229 KiB / 187 KiB | 229 KiB / 187 KiB | toplam ≤ 64 MiB |
| 2000 karede yönetilen yığın: kaydedici kapalı / açık / kapalı | 35,5 / 20,8 / 19,8 MiB | 19,8 / 20,3 / 20,5 MiB | açık − kapalı ort. < 4 MiB |

Yığın kaydedici kapalıyken de büyür (Editor ve oyuncu); bu yüzden ham artış kaydediciye atfedilmez, aynı sayıda kare açık/kapalı karşılaştırılır. Sentetik 128 izlik dosya 124 MiB'da 46.080 karede (38,4 dk) sınırı işaretleyip durdu. 84 izlik 15 dakikalık sentetik arşivde yazma ortalaması 0,022 ms/kare, arama p95 0,570 ms. Kayıt sırasında arayüz askıya alınmaz: her parça (≤ 47 KiB) ana iş parçacığında tek `FileStream.Write` ile yazılır, dizin yalnız karşılaşma bitince yazılır.

## Normal oyun kabulü

Windows x64 build'i (`91a8daf`), 1280×720 pencere. `-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile` dışında argüman yok; smoke bayrağı veya test API'si kullanılmadı. Girdi [Drive-ReplayArchiveAcceptance.ps1](windows-acceptance/Drive-ReplayArchiveAcceptance.ps1) ile Windows `SendInput` üzerinden gerçek işletim sistemi fare olayları ve tarama kodlu Esc tuşu olarak oyun penceresine gönderildi; her girdiden önce pencerenin önde olduğu doğrulandı. Bu bir otomasyondur, fiziksel klavye/fare ile insan kabulü değildir. Ekran görüntüleri yalnız oyun penceresinin kendi çizimidir. [Adım logu](evidence/replay-archive-20261122/acceptance-steps.log) · [özet sayfa](evidence/replay-archive-20261122/contact-sheet.jpg).

1. Ready menüsü: "Tekrar arşivi" sağ üst ikinci satırda, "Maç geçmişi"nin altında (01).
2. İlk 2 galibiyet + 60 sn, Oyna. Raund 1 sonunda "Tekrar kaydı sürüyor: seri bitince arşive eklenecek (1 raund)" (02). Sonraki raund; seri 0–2 bitince "Tam tekrar arşive kaydedildi." (03). Önceki oturumda "Son vuruşu izle" mevcut 5 sn tekrarının çalıştığı da görüldü.
3. Ana menü → Tekrar arşivi: "Seri 0-2 · Mağlubiyet · 0:14,4 · 2 raund · Open · 15 olay · 0,7 MiB · maç geçmişinde · Tam · Uyumlu" (04).
4. Aç: zaman çizelgesi, olay listesi, raund göstergesi (05); Rakip görünümü (06); duraklat + Oyuncuyu incele ile parça listesi (07). Esc ile ayarlar arşivin üstünde açıldı ve arşiv arayüzü çizilmedi; 3 sn sonra Esc ile kapandı, oynatma beklemişti (08–09). Kapat: menü, kurallar, yerel mücadele, arena seçimi, rakip davranışı, eğitim ve tuş düğmeleri geri geldi (10).
5. Tek maç oynandı ve arşive eklendi (11). Oyunun "Çıkış" düğmesiyle kapatıldı; diskte iki `.rpl`, hiç `.tmp` yok.
6. Yeni süreç: arşivde iki kayıt (12); seri kaydı açıldı, darbe işareti görünür (13). Listeye dön → Sil... → "Evet, sil" (14) → "Kayıt silindi.", 1/10 (15) → Kapat (16) → Çıkış. Diskte yalnız seri kaydı kaldı.
7. İki oyuncu logunda 0 istisna/hata satırı. Süreçler "Çıkış" düğmesiyle sonlandı (sürücü çıkış kodunu süreç kapandıktan sonra okumaya çalıştığı için kod kaydedilemedi).

Bu paket diğer yeni paketlerin sınıflarına bağlanmadan derlenir ve çalışır; kabul, `test` dalındaki mevcut MainArena üzerinde normal Ready akışıyla yapıldı.

## Kayıt uyumluluğu ve kullanıcı verisi

- Biçim sürüm 1 ilk sürümdür; daha önce diskte tekrar biçimi yoktu. `match-history.json` v1, ayar/tuş/rekor/ghost/mücadele dosyalarının biçimi ve yolları değişmedi. Arşiv yalnız `replays/` (ve `replays/pending/`) altında kendi GUID adlı dosyalarını açar, taşır veya siler.
- Arşiv `encounterId` ile maç geçmişine bağlanır (nullable); geçmiş silinse de açılır (PlayMode testi).
- Kullanıcı verisi: ilk build koşusundan hemen önce [Preserve-ReplayArchiveUserData.ps1](windows-acceptance/Preserve-ReplayArchiveUserData.ps1) ile yeni snapshot alındı: 42 registry değeri, 4 kişisel dosya konumu (hiçbiri yoktu), `replays/` yoktu. Kabul ve test player koşuları `replays/<guid>.rpl`, `match-history.json` ve pencere/ayar registry değerleri üretti. Son Restore + Verify: 42 registry değeri, 4 dosya konumu ve replay klasörü içerik/tür/hash olarak eşleşti. Not: son tam Editor koşusundan sonra da gerçek `match-history.json` (17.896 B) ve Editor PlayerPrefs değişmişti; bunları bu paketin testleri değil, depoyu ayrı klasöre bağlamayan mevcut testler yazıyor (arşiv testleri geçici klasör kullanır). Bu da geri yüklenip doğrulandı; mevcut test yalıtımı açığı olarak Emin'e bildirildi. Snapshot Git dışında, yerelde tutuldu. Unity'nin kendi `Unity/ShaderVariantAnalytics` analitik dosyası snapshot kapsamı dışındadır.
- İlk denemede oyun penceresi önde değilken alınan bir ekran görüntüsü başka bir uygulamayı gösterdi; hemen silindi ve o denemede hiçbir girdi gönderilmedi. Sürücü artık pencere önde değilse girdi göndermez ve yalnız oyun penceresinin çizimini kaydeder.

## Açık kalanlar

- Fiziksel klavye/fare ile insan kabulü yapılmadı; kabul Windows SendInput otomasyonudur. Emin'in insan kabulü için aynı adımlar ve veri koruma betiği kullanılabilir.
- Bağımsız review, son push onayı, gereken code-owner onayı ve GitHub kontrolleri bekliyor. PR taslak; merge edilmedi.
- Kayıt yalnız beyaz listedeki arena içeriklerinde (Floor/Obstacle/Hazard/Conveyor) alınır. Pulse/Sweep içeren layout'larda kayıt başlamaz ve sonuç ekranında nedeni yazar (tehlike paketi import edilmedi). Bu içerik ileride yeni içerik revizyonuyla eklenebilir; öneri Emin'e.
- Robot başına `CollectMeshRenderers` 32 mesh renderer verir; 10'u model içinde hep gizli olan mesh'lerdir ve sabit görünmez iz olarak yer kaplar. Open layout 115/128 iz kullanır; Tactical +2 statik iz (hesap, ölçülmedi). Çok sayıda engel içeren bir layout 128'i aşarsa önce debris izleri düşer ve kayıt "Eksik: iz sınırı" olur.
- Darbe izleri her parça başında saklanır ve her darbeden sonra yeni parça açılır; aşınma miktarı her karede. Partikül, ses ve bariyer uyarısının yanıp sönme rengi kaydedilmez (kapsam dışı).
- `MainMenuScreen.IsShowingReadyMenu` yansıma ile okunur (başlangıç kaynağında yoktu). Ortak API olarak kalıcılaşırsa doğrudan çağrıya geçilebilir.
- `Tests/Gameplay` altındaki bazı test dosyaları depoda `.meta` olmadan izleniyor; Unity her açılışta yeni GUID'le `.meta` üretiyor. Bu paketin kapsamı dışında, commit edilmedi; Emin'e bildirildi.
- `TestHarnessPlayModeTests.MeleeTestSceneDrivesEveryActionFromTheKeyboard` bir kez tam koşuda kaldı (yukarıda); mevcut kararsız test.
