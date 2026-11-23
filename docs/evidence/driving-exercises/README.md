# DRIVING-02 doğrulaması — 23 Kasım 2026

[Issue #46](https://github.com/Lysnder/RobotSavaslari/issues/46) · [PR #55](https://github.com/Lysnder/RobotSavaslari/pull/55) · hedef `test`.

Test edilen oyun kaynağı `b298aa348076e8172f788db0124ee94b6eaebc05`. Başlangıç `test` ucu `0d8a6743e8d80065a8afeaf41ed0964d7a14d852`, `main` ucu `8452fa4a9f568966c98f5a2468ba57436ee05624`. #43, `6b2d41a` ile birleşti. PR başı `12d7477` güncel test'in atası ve merge farkında `RobotArena` ağacı aynı. Bağımsız kontrol için yalnız DRIVING-02 değişiklikleri `12d7477` üzerine uygulandı. Bağımsız kaynak `064a430fcc620979dc53dcc2745db73751519d44`.

MainArena hazır ekranında **Sürüş alıştırmaları** düğmesini aç. Dört seçenek aynı robotu, sürüş eşlemelerini, boost ve el frenini kullanır. Uygun boşluk varsa rota bütünü korunarak yan koridora kaydırılır. Robotun ayak iziyle güvenli koridor bulunamazsa giriş reddedilir. **Sürüş eğitimi** düğmesi eski zaman turunu açar.

| Alıştırma | Bitiş ve ölçüm |
|---|---|
| Slalom | Numaralı kapıların düzlemini ilerleyen yönde geç. Düzlem geçişinde kapının açıklığı dışındaysan bir kapı hatası yazılır. Son kapıdan sonra süre, kapı ve temas sayısı görünür. |
| Kontrollü duruş | Hedef bölgede kesintisiz 1 sn boyunca yatay hız en fazla 0,15 m/sn. İlerleme ekseninde bölge kenarını en fazla kaç metre aştığın ölçülür. |
| Hassas dönüş | Aynı fizik örneğinde konum hatası en fazla 0,5 m ve açı sapması en fazla 5°. Bitiş anındaki iki değer gösterilir. |
| Geri park | Bölgeye geri hareketle gir. Açı sapması en fazla 8°, hız en fazla 0,15 m/sn ve kesintisiz 1 sn bekleme gerekir. Hatalı girişten sonra yeniden girebilirsin; o deneme geçersiz kalır. |

`CollisionReporter` tarafından raporlanan sert yan temaslar denemeyi geçersiz kılar. Bir fizik adımının son rapor edilen teması sayılır. Zemin sürtünmesi sayılmaz. Yanlış sıra/kaçırılan kapı ve yanlış yön hatası olan deneme kişisel rekor olmaz. Geçerli sonuçlar hata sayısı, duruş aşımı, sonra süreyle karşılaştırılır. Yarım **Yeniden dene** aynı deneme numarasında ölçümleri ve hayaleti sıfırlar. Sonuç yalnız bir kez kaydedilir. Süre sınırı 600 sn.

`driving-exercises.json` sürüm 1 kullanır. Anahtar alıştırma kimliği, kural revizyonu ve veri özeti, `arena-v2` düzen/rota parmak izi ve sürüş profil özeti içerir. En çok 20 anahtar, anahtar başına 20 sonuç, ayrı rekor ve en çok 6.001 hayalet pozu tutulur. Bozuk, gelecek sürüm ve kapasiteye ulaşmış kayıtlar korunur. Eski `driving-personal-best.json` çalışır ve yalnız tam eşleşen parkur eski zaman turu alanına bir kez kopyalanabilir. Yeni alıştırmalar bu kaydı kullanmaz.

Üretim bağlantısı `DrivingFeatureInstaller`, `TimeTrialSession`, `TimeTrialHud`, `GhostRunner` ve `GhostLapRecording` üzerindedir. Yeni eğitim sınıfları, Resources kataloğu ve testleri kendi `.meta` dosyalarıyla eklendi. MainArena sahnesi, prefab'lar ve mevcut GUID'ler korundu.

Unity 6000.4.4f1, Windows 11 Pro 10.0.26300, Ryzen 9 9950X3D2, Windows'un gördüğü 61,7 GiB RAM, GeForce RTX 5090. Birinci Windows denemesinde 2560 × 1440 kullanıldı. İlk çalışma kopyası içe aktarımında uzun Windows yollarında Unity paketlerinin bazı kaynakları açılamadı. Ardından çalışma kopyasının önbelleği ayrılarak kısa bir sürücü yolundan temiz içe aktarıldı. Gameplay ve render ayarları commit kapsamında korunuyor.

Windows x64 build kimliği `sha256:1A87B780EAE8240553315093BA1CED5F81904885B33A99FD5B67212468B0B0EB` (194 dosya). Dosya listesi `driving-camera-build-manifest.json`. Kimlik, sıralı göreli yol, tab, büyük harf SHA256 ve LF satırlarının SHA256 özetidir.

Kanıtlar çalışma kopyasında `RobotArena/Logs/DrivingExerciseEvidence` altında. Oyuncu anlık görüntüleri, ham günlükler ve Windows paketleri Git'e eklenmedi.

## Çalıştırılan kontroller

| Kaynak | Çalıştırma | Sonuç | Dosya |
|---|---|---|---|
| `b298aa3` | Unity EditMode, bütün proje | 224/224, atlanan yok | `driving-camera-edit.xml`, `driving-camera-edit.log` |
| `b298aa3` | Unity PlayMode, grafik açık, Training ve Driving | 97/97, atlanan yok | `driving-camera-play.xml`, `driving-camera-play.log` |
| `b298aa3` | Normal Windows x64 derlemesi | Başarılı | `driving-camera-build.log`, `driving-camera-build-manifest.json` |
| `b298aa3` | Windows x64 Test Framework oyuncusunda çekirdek ölçüm | 1/1, atlanan yok | `driving-camera-player-perf.xml`, `driving-camera-player-perf.log` |
| `064a430` | #43 tabanı ve yalnız DRIVING-02, Windows x64 | Başarılı | Bağımsız kopyada `RobotArena/Logs/driving-camera-independent-build.log` |
| `ac2343a` | #43 tabanındaki önceki bağımsız Windows oyunu, normal menü | Dört seçeneğin listesi, kontrollü duruşa giriş, fareyle yeniden deneme ve ana menüye dönüş görüldü. Uçtan uca sürüş tamamlanmadı. | `driving-clean-normal-2.log` |
| `b298aa3` | Son Windows oyunu, normal menü | Arena materyalleri, dört seçenek, Slalom ve Geri park, numaralı kapılar, birimli ölçümler ve çıkışta menü/kamera geri dönüşü görüldü. Fiziksel klavyeyle tamamlanmış dört deneme ve yeni süreçte gerçek oyuncu rekorunun yeniden açılması doğrulanmadı. | `driving-camera-normal-1.log` |

PlayMode kapsamı dört başarı eşiği, kaçırılan kapı, gerçek çarpışma bileşeninin sürüş regresyonları, aşım, yanlış yön, dwell kesilmesi, tek sonuç, dört ayrı rekor anahtarı, 20 sonuç sınırı, bozuk/gelecek sürüm dosyası, tam eşleşen eski dosya geçişi, 10 giriş/çıkış, yarım Retry, kamera kadrajı ve geri dönüşü ile eski girdi/parkur/marker kontrolleridir. Birim ölçüm testleri kontrollü senaryo girdileri kullanır. Normal Windows oturumunda test bayrağı ve Unity test API'si kullanılmadı.

Komutlar, `R:\RobotArena` çalışma kopyasına geçici olarak eşlenen proje köküyle yürütüldü. Aşağıdaki göreli kanıt adları `R:\RobotArena\Logs\DrivingExerciseEvidence` içindedir. `$unity` editör yolu `C:\Program Files\Unity\Hub\Editor\6000.4.4f1\Editor\Unity.exe`.

```powershell
Set-Location R:\RobotArena\Logs\DrivingExerciseEvidence
& $unity -batchmode -projectPath R:\RobotArena -runTests -testPlatform EditMode -testResults "$PWD/driving-camera-edit.xml" -logFile "$PWD/driving-camera-edit.log"
& $unity -batchmode -projectPath R:\RobotArena -runTests -testPlatform PlayMode -testFilter 'RobotSavaslari.Training.Tests|RobotSavaslari.Driving.Tests' -testResults "$PWD/driving-camera-play.xml" -logFile "$PWD/driving-camera-play.log"
& $unity -batchmode -projectPath R:\RobotArena -quit -executeMethod RobotSavaslari.Editor.BuildWindowsPlayer.Build -logFile "$PWD/driving-camera-build.log"
& $unity -batchmode -projectPath R:\RobotArena -runTests -testPlatform StandaloneWindows64 -testFilter MeasureWarmedEvaluationAndGhostInterpolation -testResults "$PWD/driving-camera-player-perf.xml" -logFile "$PWD/driving-camera-player-perf.log"
& $unity -batchmode -projectPath S:\RobotArena -quit -executeMethod RobotSavaslari.Editor.BuildWindowsPlayer.Build -logFile S:\RobotArena\Logs\driving-camera-independent-build.log
```

Normal kullanım yolu `Builds/Windows/RobotSavaslari.exe` → `Sürüş alıştırmaları` → alıştırma → `Yeniden dene` / `Eğitimden çık`. Oyun `-screen-fullscreen 0 -screen-width 1100 -screen-height 740 -logFile <oyun-günlüğü>` ile başlatıldı. Oyuncunun mevcut tam ekran tercihi nedeniyle görülen masaüstü yakalaması 2560 × 1440 oldu.

## Ölçüm ve açık kabul

Çekirdek ölçüm tek robotun değerlendiricisini ve 6.001 pozlu kayıttan hayalet interpolasyonunu içerir. 120 ısınma adımı ve 1.000 örnek. Editör p50/p95/maksimum sırasıyla 0,00020 / 0,00020 / 0,00120 ms, toplam yönetilen tahsis 0 B. Windows x64 test oyuncusunda 0,00020 / 0,00020 / 0,00110 ms, toplam 0 B. Bu ölçümün dışında kalan normal oyun HUD'u, çizim, disk yazımı ve tam kare profili ölçülmedi.

Windows kabulünde kısa otomatik klavye girdileri robota doğrulanabilir hareket vermedi. Menü tıklamaları ve kamera gözlemleri gerçekleşti. Son oyun normal çıkıştan sonra `0xC000041D` koduyla kapandı. Native çağrı yığınında `UnityPlayer`, `USER32` ve `ctxapclient64` var. Benzer iz bağımsız kopyada da görüldü. Kök nedenin hangi bileşende olduğu ayrıştırılmadı. Ham günlükler yerel kanıt klasöründe.

Normal oyuncu açılışlarından hemen önce ayar/tuş ve kayıtlar için yeni anlık görüntüler alındı. Kapanışlarda içerik SHA256, kayıt değerleri ve türleri karşılaştırılarak geri yükleme doğrulandı. Son rapor `preferences-driving-camera-normal/user-data-Verify.json`, 45 kayıt değeri ve 6 kişisel dosya konumu eşleşti, tekrar arşivi durumu eşleşti.

PR hedefi `test`. Fiziksel klavyeyle uçtan uca sürüş ve kapanış kabulü tamamlanmadığı için taslak. Birleşme için güncel test tabanı, gerekli kontroller, son gönderime bağımsız inceleyici onayı, uygulanıyorsa code-owner onayı ve kapatılmış inceleme konuşmaları gerekir.
