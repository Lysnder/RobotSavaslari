# ARENA-02 Windows Profiler doğrulaması

19 Kasım 2026. Kaynak `7629f745cfb3d95520885c642fc4f9915163a664`. [Issue #44](https://github.com/Lysnder/RobotSavaslari/issues/44) · [PR #49](https://github.com/Lysnder/RobotSavaslari/pull/49)

Unity 6000.4.4f1, AMD Ryzen 5 5600X ve 32691 MB RAM ile ölçüldü. MainArena'da sekiz aktif Pulse/Sweep tehlikesi ve ikişer collider taşıyan iki robot kullanıldı. Temas koşulunu sabit tutmak için robotların hareketi kısıtlandı. Isınmadan sonra iki robotta da hasar gözlendi.

Unity'nin fizik döngüsü 20 ms'lik oyun zamanı adımıyla her karede sekiz tehlikeyi çalıştırdı. 250 ısınma karesinden sonra arka arkaya 300 kare örneklendi. Test her örnekte sekiz fizik çağrısı olduğunu doğruladı. `ArenaHazard.FixedUpdate` Profiler işareti süreyi topladı. Tehlike çağrılarındaki yönetilen tahsis ayrı ölçüldü. Ana iş parçacığındaki tüm `GC.Alloc` olayları da kaydedildi. p95 ≤1 ms ve 0 B/kare hedefleri tehlike koduna uygulanır.

| Ortam | Çözünürlük | p50 ms | p95 ms | Maks. ms | Tehlike maks. B/kare | Ana iş parçacığı maks. B/kare |
|---|---:|---:|---:|---:|---:|---:|
| Windows Editor | 640×480 | 0,0388 | 0,0833 | 0,4873 | 0 | 24.400 |
| Windows test build'i | 1024×768 | 0,0350 | 0,0717 | 0,4846 | 0 | 27.200 |

Windows x64 test build'i derlendi ve performans testi 1/1 geçti. 300 karenin tamamında tehlike tahsisi 0 bayttı. [NUnit sonuçları, loglar ve kare başı CSV kayıtları](evidence/arena-hazards-20261119/frame-profiler-evidence.zip)

26/26 PlayMode testi geçti. Buna iki arenada 16 fiziksel bot karşılaşması ve `EnterStartsAndRStartsTheNextMatch` dahil. Enter ve R, Unity Input System'e gönderilen tuş olaylarıyla sınandı. Windows'taki önceki normal kabul Oyna düğmesiyle tamamlanmıştı. Fiziksel Enter denemesi bu issue'da ayrı bir kabul maddesi olarak yer almıyor. [Önceki Windows kabul raporu](arena-hazards-validation-20261119.md)

Testler geçici proje kopyasında, grafikler açıkken çalıştırıldı. `$verify` yolu `%TEMP%/RobotArena-44-verification`.

```powershell
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe'
$verify = Join-Path $env:TEMP 'RobotArena-44-verification'
$playFilter = 'ArenaHazardTests;MatchFlowPlayModeTests;ArenaMatchControllerPlayModeTests;RobotDrivingComponentsPlayModeTests;EnterStartsAndRStartsTheNextMatch'
& $editor -batchmode -projectPath $verify -runTests -testPlatform PlayMode -testFilter $playFilter -testResults "$verify/final-PlayMode.xml" -logFile "$verify/final-PlayMode.log"
& $editor -batchmode -projectPath $verify -runTests -testPlatform StandaloneWindows64 -testFilter MeasureEightActiveHazards -testResults "$verify/final-StandaloneWindows64.xml" -logFile "$verify/final-StandaloneWindows64.log"
```
