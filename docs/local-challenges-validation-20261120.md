# SESSION-02 doğrulama kaydı

20 Kasım 2026. [Görev #48](https://github.com/Lysnder/RobotSavaslari/issues/48) · [Taslak PR #50](https://github.com/Lysnder/RobotSavaslari/pull/50)

Üretim kaynağı `48f0793445ebc8da302f35e4cef43ae39dc89e02`, başlangıç `test` ucu `321537fddf9e2645dc3683d4465fb11cfedf0432`. `cec7d024f00dd15dd270a01cad60d386457f1996` yalnız aşama geçişi ölçüm testini ekledi. Proje Unity 6000.4.4f1 ile Windows x64 üzerinde doğrulandı. MainArena ve mevcut asset GUID'leri korundu.

## Oyuncu akışı

- **Open / Aggressive Easy** tek maç, **Tactical / Counter Hard** ilk iki galibiyet, **Open / Aggressive Hard** tek maç. Raund süresi 60 saniye. Galibiyet, mağlubiyet veya tek maç beraberliği bir aşamayı tamamlar. Berabere seri raundu aynı aşamada kalır.
- İlerleme `challenge-session.json` sürüm 1 kaydında tutularak her karşılaşma için bir kez yazılır. Baştan başlatma yeni oturum kimliği üretir, önceki özet arşivde kalır. Arşiv 20 özetle ve kayıt dosyası 256 KiB ile sınırlıdır. Okunamayan kayıt, oyuncu saklamayı seçtiğinde ayrı dosyaya taşınır.
- Windows x64 normal oyuncu derlemesinde **Yerel mücadele → Mücadeleyi başlat → ilk sonuç → Son vuruşu izle → oyunu kapat/aç → Kaldığın aşamadan başlat → Sonraki raund → Devam → Özet → Tamamla → Maç geçmişi → Rövanş** yolu uygulandı. Girdi Windows pencere otomasyonuyla gönderildi. Başlatma, aşama geçişi ve rövanş düğmeleri fareyle kullanıldı.
- İlk aşama kaydı yeni süreçte aynı SHA-256 ile okundu. İkinci aşamanın ilk raundu `0–1` skorunda aşama kaydı üretmedi. İkinci raunddan sonra seri `0–2` ile tek aşama olarak kaydedildi. Finalin ardından üç aşamalı özet 40,4 saniye gösterdi. Tamamla düğmesi tek arşiv kaydı bıraktı ve aktif oturumu kapattı. Rövanş normal tek maç olarak oynandı.
- Normal oturumdan önce alınan yedeğe karşı **45 registry değeri ve dört dosya konumu** Restore ve Verify kontrollerinde eşleşti. Geçici performans koşularından sonra aynı kontroller geçti. Kişisel yedekler yerelde saklandı.

## Çalıştırılan kontroller

| Kaynak | Kontrol | Sonuç | Yerel kanıt |
|---|---|---|---|
| `48f0793` | Unity EditMode | 205/205 | `final-editmode.xml`, `final-editmode.log` |
| `48f0793` | Unity PlayMode | 228/228. 11 mücadele testi dahil | `final-playmode.xml`, `final-playmode.log` |
| `48f0793` | Windows x64 oyuncu derlemesi | Başarılı. 194 dosyanın SHA-256 içeriği doğrulandı | `final-windows-build.log` |
| `48f0793` | Windows normal menü, yeniden açılış, arşiv ve rövanş | Fareyle geçti. Her iki oyuncu süreci kod 0 ile kapandı | `acceptance-1-interactive.log`, `acceptance-2-interactive.log` |
| `48f0793` | Windows x64 test oyuncusunda checkpoint yazımı | 1/1 | `final-standalone.xml`, `final-standalone.log` |
| `cec7d02` | Mücadele PlayMode grubu | 12/12 | `transition-PlayMode.xml`, `transition-PlayMode.log` |
| `cec7d02` | Windows x64 test oyuncusunda aşama geçişi | 1/1 | `transition-StandaloneWindows64.xml`, `transition-StandaloneWindows64.log` |

PlayMode kapsamı 10 yinelenen sonuç bildirimi ve değişmeyen checkpoint baytları, kısmi seri sonrası yeniden yükleme, turnuva devamı, beraberlik, pause, tuş yakalama ve eğitim girişi, robot inceleme engeli, kayıt kilidi, eşzamanlı değişiklik, bozuk/gelecek sürüm, baştan başlatma, geçmiş hatası uyarısı ve normal rövanşı içerir. `match-history.json` v1 modeli korunur.

Build kimliği `sha256:FEE3E45D6085230D8B13C014619B4F53785983EC1080F7EB53DC1B0B41C7F0C6`. Kimlik göreli yolları ordinal sıralayıp her satırı yol, tab, büyük harf SHA-256 ve LF olarak UTF-8 kodlayarak hesaplandı. [Dosya manifesti](evidence/local-challenges-20261120/final-build-hashes.json)

## Performans

Windows NT 10.0.26300, Ryzen 9 9950X3D2, 63.152 MB RAM. Normal menü doğrulaması 2560×1440. Aşama geçişi ölçümü iki robotla, Open karşılaşması sonlandıktan sonra mevcut arena içeriğinin Tactical olarak kurulmasını ve geri sayımın hazırlanmasını kapsadı. Sahne yeniden yüklenmedi. Her ortamda 4 ısınma ve 20 ölçüm alındı.

| Ortam | Çözünürlük | p50 | p95 | Maks. | Ana iş parçacığı tepe GC.Alloc |
|---|---:|---:|---:|---:|---:|
| Windows Editor | 640×480 | 2,640 ms | 3,386 ms | 3,471 ms | 35.200 B/kare |
| Windows x64 test oyuncusu | 1024×768 | 2,630 ms | 3,879 ms | 4,800 ms | 35.400 B/kare |

Checkpoint yazımı ayrı ölçüldü. 5 ısınma ve 50 örnek alındı. Arşiv kapasitesi 20'ye ulaştı ve arşiv oturumları sonuç içermeyen sentetik kayıtlardı. Windows test oyuncusunda p50 6,306 ms, p95 7,106 ms, maksimum 7,236 ms. Normal oturumdaki üç sonuçlu checkpoint ve arşiv dosyası 1.869 bayttı.

## Tekrar çalıştırma

`$project` ile `RobotArena` klasörünü, `$evidence` ile proje dışındaki kayıt klasörünü belirt.

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe'
& $unity -batchmode -projectPath $project -runTests -testPlatform EditMode -testResults "$evidence/final-editmode.xml" -logFile "$evidence/final-editmode.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform PlayMode -testResults "$evidence/final-playmode.xml" -logFile "$evidence/final-playmode.log"
& $unity -batchmode -projectPath $project -quit -executeMethod RobotSavaslari.Editor.BuildWindowsPlayer.Build -logFile "$evidence/final-windows-build.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter MeasureCheckpointWrites -testResults "$evidence/final-standalone.xml" -logFile "$evidence/final-standalone.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform PlayMode -testFilter RobotSavaslari.Match.Tests.ChallengeSessionTests -testResults "$evidence/transition-PlayMode.xml" -logFile "$evidence/transition-PlayMode.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform StandaloneWindows64 -testFilter MeasureOpenToTacticalTransition -testResults "$evidence/transition-StandaloneWindows64.xml" -logFile "$evidence/transition-StandaloneWindows64.log"
```

## Açık doğrulama

Windows otomasyonuyla gönderilen `R` ve `Esc` oyun penceresinde beklenen etkiyi üretmedi. Menüde eşdeğer eylemler fareyle çalıştı. Klavye davranışı için Unity Input System testleri yeşil ve fiziksel klavyeyle Windows kabulü açık. Bağımsız review, son push onayı ve gerekli GitHub kontrolleri bekliyor. PR taslak ve hedefi `test`.
