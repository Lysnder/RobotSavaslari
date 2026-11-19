# ARENA-02 doğrulama — 19 Kasım 2026

Sonraki doğrulamada Windows test build'inde 300 oyun karesi ölçüldü ve Enter regresyonu geçti. [Güncel Profiler sonuçları](arena-hazards-profiler-20261119.md)

Issue [#44](https://github.com/Lysnder/RobotSavaslari/issues/44), taslak [PR #49](https://github.com/Lysnder/RobotSavaslari/pull/49), hedef `test`, dal `feature/arena-hazards`.

Test edilen kaynak: `4f8fa7c212c58d0d0750fa2e77a542f25bccf494`. Doğrulama belgeleri bundan sonra ayrı commit ile eklenir. 19 Kasım son fetch sonucunda `origin/test=6b2d41ac2ff4944d29a2ede5c58facf4c6fd4be7`; başlangıç tabanı ilerlememiştir. PR #43 birleşmesi önceki oturumda doğrulandı.

## Uygulama ve commit sırası

| Görev | Commit | Gözlenebilir çıktı |
|---|---|---|
| 02.1 | `ced6354` | Enum sonuna Pulse/Sweep; sabit alan ve dar hareketli şerit |
| 02.2 | `c1b190f` | `RobotHealth.TakeDamage`, `source=null`, robot başına cooldown/tek vuruş |
| 02.3 | `d5d05c0` | Idle/Warning/Active/Recovery, hareket/boyut/yükseklik uyarısı, Playing/pause gate |
| 02.4 | `9ee0daa` | Mevcut hücre editöründe yön, süre, hasar ve tekrar aralığı |
| 02.5 | `d35c737` | Tehlikenin hareket alanıyla spawn tamponu ve güvenli rota doğrulaması |
| 02.6 | `9b0bf79` | Resources kataloğuyla MainArena'ya PulseArena/SweepArena kaydı |
| 02.7 | `b987f19` | Normal Ready menüsündeki kaydırılabilir seçim; eski collider'ların emekliye ayrılması |
| 02.8 | `4f8fa7c` | Çevrim/hasar testleri, 16 bot maçı, gizmo ve maliyet ölçümü |

Mevcut ArenaEnvironment, ArenaLayoutValidator, MatchController ve RobotHealth API'leri kullanıldı. MainArena YAML, ortak prefab, RobotHealth, GameSettings, ortak builder ve ortak asmdef değişmedi. Open/Tactical asset'leri ve mevcut enum değerleri korundu. Katalog yalnız MainArena yüklenince kayıt yapar; diğer yeni geliştirme paketlerine bağımlılık yoktur. Yeni Unity varlıkları `.meta` dosyalarıyla gönderildi.

Paylaşılan UI ihtiyacının somut patch'i: `MainMenuScreen.cs` içine yalnız üç satırlık public `IsShowingReadyMenu` sorgusu eklendi (`b987f19`). Mevcut menü/maç/pause/geçmiş/tuş ayarı gate'ini arena seçimiyle paylaşır; mevcut çağıranların imzası değişmez. Emin'in kapsam incelemesine açıktır.

## Otomatik sonuçlar

Windows, Unity `6000.4.4f1 (360f97ecca93)`, AMD Ryzen 5 5600X, 32691 MB RAM. Graphics açık; `-nographics` kullanılmadı. NUnit sonuçları ve loglar [evidence.zip](evidence/arena-hazards-20261119/evidence.zip) içindedir.

| Grup | Geçti | Kaldı | Atlandı |
|---|---:|---:|---:|
| EditMode seçili arena/geometri/authoring/bariyer/maç kuralı | 63 | 0 | 0 |
| PlayMode arena tehlikesi ve seçili maç/reset/sürüş regresyonları | 25 | 0 | 0 |
| Windows x64 test player performans testi | 1 | 0 | 0 |

Bu sayılar bütün projenin test toplamı değildir. Eski 410 test ve 48 maç bu sürümün sonucu olarak sayılmadı. PlayMode içindeki tek BotMatchMatrix testi 16 gerçek fiziksel bot karşılaşması çalıştırdı: iki arena × Aggressive/Counter × Easy/Hard × iki tekrar. Tamamı süre sınırından önce tek geçerli sonuç verdi (5,42–15,77 saniye). Oyuncu bu matriste sabit; insan sürüş kalitesi veya kazanma dengesi ölçülmez.

Hasar testleri compound collider deduplikasyonu, Pulse tekrar aralığı, Sweep tek vuruşu, Warning'de hasar olmaması, Ready/Countdown/Result/pause gate'i, çevresel hasarın rakibin dealtDamage'ına yazılmaması, tek yenilgi sonucu ve temiz reset'i kapsar. 10 seçimde eski collider'lar anında kapanır ve yalnız yeni iki tehlike kalır. Authoring save/reopen testi üretim sahnesinin geçici kopyasında çalışır.

### Çalıştırma yolu

Editor: `C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe`. Test kopyası `%TEMP%/RobotArena-44-verification`; kaynak Assets/Packages/ProjectSettings buraya kopyalandı. Aynı kopyadaki çalıştırmalar sırayla yapıldı.

```powershell
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe'
$verify = Join-Path $env:TEMP 'RobotArena-44-verification'
& $editor -batchmode -projectPath $verify -runTests -testPlatform EditMode -testFilter 'ArenaGeometryTests;ArenaLayoutTests;ArenaAuthoringTests;WarningBarrierTests;CombatRulesTests' -testResults "$verify/EditMode.xml" -logFile "$verify/EditMode.log"
& $editor -batchmode -projectPath $verify -runTests -testPlatform PlayMode -testFilter 'ArenaHazardTests;MatchFlowPlayModeTests;ArenaMatchControllerPlayModeTests;RobotDrivingComponentsPlayModeTests' -testResults "$verify/PlayMode.xml" -logFile "$verify/PlayMode.log"
& $editor -batchmode -projectPath $verify -runTests -testPlatform StandaloneWindows64 -testFilter MeasureEightActiveHazards -testResults "$verify/StandaloneWindows64.xml" -logFile "$verify/StandaloneWindows64.log"
```

Kaynakta görevle değişen 22 dosyanın test kopyasındaki hash'leri aynı. Normal Windows build kopyasındaki bütün değişen runtime/editor/asset dosyaları da aynı; build kopyasında daha eski test kodu bulunduğu için o kopyanın testleri son NUnit sonuçları olarak kullanılmaz. Son NUnit sonuçları verification kopyasındandır.

## Windows build ve normal oyun

Normal build yolu `%TEMP%/RobotArena-44-player/Builds/Windows/RobotSavaslari.exe`; `BuildWindowsPlayer.Build` ile Windows x64 build sonucu Success. EXE SHA256: `413A04009D5FC13E03FE4E5C6941A8606C63C103EDD08853EFC6C9BD57E6151B`. Bütün build dosyalarının hash listesi kanıt arşivinde `build-sha256.csv` içindedir.

Build komutu:

```powershell
$playerProject = Join-Path $env:TEMP 'RobotArena-44-player'
& $editor -batchmode -projectPath $playerProject -executeMethod RobotSavaslari.Editor.BuildWindowsPlayer.Build -quit -logFile "$playerProject/windows.log"
```

Normal exe smoke/test bayrağı olmadan açıldı. Bilgisayar Kullanımı ile oyun penceresine OS fare girdileri gönderildi. Önceki oturumdaki görüntüler: Pulse seçimi, pause paneli, ilk raund, 0–2 seri sonucu, Sweep seçimi/oynanış/sonuç; uygulama kapatılıp yeni süreçte maç geçmişi açıldı. Bunlar otomatik NUnit input'u olarak sunulmaz.

Devam oturumunda normal menüden PulseArena seçilip Oyna düğmesiyle maç tamamlandı (7,2 sn, oyuncu 0/rakip 90). Ana menü → Maç geçmişi → Rövanş ile ikinci karşılaşma açıldı, temiz spawn/geri sayım görüldü ve ikinci sonuç oluştu (Maç 2, 7,2 sn, oyuncu 0/rakip 90). Üç normal player logunda exception/assertion kaydı yok. Görüntüler arşivde `acceptance/` altındadır.

Not: OS Return gönderiminin ardından menü kaldı; fareyle Oyna çalıştı. Kısa tuş enjeksiyonunun oyun frame'inde gözlenip gözlenmediği ayrıştırılmadığı için klavyeyle başlatma bu oturumda geçti sayılmaz. Oyunun tüm klavye kontrollerinin OS kabulü yapıldığı iddia edilmez.

## Kullanıcı verileri ve uyumluluk

Önceki oturumda normal kabulden önce alınan `%TEMP%/RobotArena-44-player/acceptance/state.clixml` aynı kabulün devamında korundu. Son normal player kapatıldıktan sonra `Preserve-UserData.ps1 -Action Restore`, ardından `-Action Verify` çalıştı: 40 registry değeri, iki kişisel kayıt dosyası; registry içerik/tür ve dosya hash eşitliği true. Son doğrulama 19 Kasım 2026 21:28 Türkiye saati. Kişisel snapshot ve kayıt içerikleri Git'e eklenmedi; yalnız içeriksiz sonuç JSON'u arşivdedir. Açık geliştirme Editor'ü korunmuştur.

Match history veri formatı değişmedi. Yeni süreçte eski ve yeni maçlar aynı geçmiş panelinde okunabildi. Normal kabul sırasında eklenen maçlar geri yüklemeyle test öncesi durumuna döndürüldü.

## Performans ve sınırlar

Windows test player: 1024×768; 8 tehlike, iki robot, her robotta iki collider; 250 warm-up, 1500 örnek. Her örnek sekiz tehlikenin FixedUpdate gövdesini delegate ile sırayla çağırır; Stopwatch süreyi, `GC.GetAllocatedBytesForCurrentThread` aynı thread tahsisini ölçer. Bu bir gövde mikro ölçümüdür; Unity Profiler'da gerçek render frame başına bütün oyun maliyeti değildir.

| Ortam | p50 ms | p95 ms | max ms | Ölçülen yönetilen tahsis |
|---|---:|---:|---:|---:|
| Windows Editor, 640×480 | 0,0336 | 0,1694 | 8,0899 | 0 B |
| Windows Player, 1024×768 | 0,0119 | 0,0514 | 3,3277 | 0 B |

p95 ≤1 ms gövde bütçesi geçti. Max değer gizlenmedi. Sürekli gerçek frame başına 0 B GC.Alloc ve gerçek player-loop Profiler track ölçümü bu testten çıkarılamaz; bunlar ayrıca doğrulanmalıdır.

## İnceleme durumu

PR taslak tutulur: gerçek frame Profiler ölçümü ve klavye kabulünün yukarıdaki sınırı açıkça kayıtlıdır. Tam proje regresyonu çalıştırıldığı iddia edilmez. `MainMenuScreen` public sorgusu için ortak dosya kapsamı ayrıca incelenmelidir. Birleştirme yapılmadı. Hedef `test`; bağımsız review, son push onayı ve gereken code-owner/konuşma koşulları sağlanmadan merge edilmez. Main yayını ayrı `test → main` akışıdır.
