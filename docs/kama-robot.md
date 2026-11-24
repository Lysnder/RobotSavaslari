# Kama yakın menzil robotu

ROBOT-02, GitHub #60. Unity 6000.4.4f1. Ortak taban `6200ddcaff7bda984a88d343db1446c1a6ce57c1`.

## Oynama

`Assets/Scenes/Gameplay/MainArena.unity` açılır. Ortadaki **Robot** düğmesi Döner ile Kama arasında geçiş yapar. Sağdan Open arenası seçilir ve **Maça başla** ile mevcut bota karşı oynanır. Kama dar yayıyla önden, yakın mesafede vurur. İsabet, hedefin ileri yöndeki hızını 2,4 m/s’ye getirir ve mevcut motorun kısa kavrama kaybını kullanır. Temas sesi, kıvılcımlar, hareketli sarı çatal ve rakibin geri kayması isabeti gösterir. Silah kopunca saldırı kapanır. Hazır ekranına dönünce Döner tekrar seçilebilir.

| MainArena oyuncu değeri | Döner | Kama |
| --- | ---: | ---: |
| Kütle / azami can | 12 kg / 100 | 12 kg / 100 |
| Hız / geri hız | 5,2 / 4 m/s | 5,2 / 4 m/s |
| İvme / dönüş | 26 m/s² / 300°/s | 26 m/s² / 300°/s |
| Saldırı merkezi → hedef collider yüzeyi | 1,50 m | 1,10 m |
| Yay / aktif pencere | 120° / 0,15 s | 78° / 0,18 s |
| Vuruş / bekleme | 25 / 1,00 s | 18 / 1,25 s |
| 100 candan yenilgiye silah isabeti | 4 | 6 |
| Silahın uyguladığı ileri hız | 0 | 2,4 m/s |

## İçerik bütçesi ve kaynak

Gövde, zırh ve dört teker sahnedeki `BlueCombatRobot.glb` modelinden gelir. Mevcut oyuncu kökü aynı Rigidbody, BoxCollider, motor, klavye sürücüsü, kamera hedefi ve hasar parçalarını kullanır. Özgün çatal geometrisi bu görevde Unity küp geometrisinden oluşturuldu. Turuncu metal projenin mevcut `Assets/Materials/RobotNose.mat` malzemesidir. Yeni dış model, doku veya lisans gerektiren indirme yoktur.

| Kaynak ölçümü | Değer |
| --- | ---: |
| Mavi GLB / temel görünüm | 29.451.680 bayt, 120.000 üçgen, 3 doku |
| Çıkarılan döner silah | 3.040 üçgen |
| Kama çatalı / yeni doku | 6 parça, 72 üçgen, 0 doku |
| Kama görünür ana geometri | 117.032 üçgen |
| Yeni fizik collider / oyuncu gövdesi | 0 / 1 BoxCollider |
| Kutu genişlik × yükseklik × uzunluk | 1,3372 × 0,8178 × 1,6500 m |
| Kutudan hesaplanan yatay dönüş yarıçapı | 1,0619 m |
| Çatalın dinlenme sınırları, X / Y / Z | ±0,545 / 0,144…0,707 / −0,168…0,819 m |
| Çatal menteşesi, robot yerel XYZ | 0 / 0,630 / −0,090 m |

Windows çalışma kopyasının oluşturulması (00.37.56) ile oynanabilir içerik ve ilk iki regresyonu içeren `b383a3f` kaydı (00.43.23) arasında 5 dakika 27 saniye geçti. Bu duvar saati ölçümü ilk üretimi kapsar. Sonraki hizalama, doğrulama ve Windows derlemesi ayrıca yürütüldü. Prefab 16.348 bayt, profil 496 bayttır.

Üçgen sayımı gövde ve hasar parçalarının temel yüzeylerini içerir. Aşınma kaplaması gerektiğinde aynı mesh’i ikinci geçişte çizer. Kaynak GLB’deki indeks sayısı ve üretim bölümleyicisinin üst silah kesiti (üçgen merkezinde Y > 0,295 m) kullanıldı. Çatalın altı kutusu tek mesh ve tek malzemeye birleşir. Bu 72 üçgenlik parça tek ayrıntı seviyesinde kalır. Teker milleri mevcut oyuncu sağ ekseninde, çatal mafsalı aynı eksende çalışır. Seçim sırasında yeni collider veya sahne robotu üretilmez. Dinlenme geometrisinin tüm köşeleri oyuncunun kutusu içinde kontrol edilir. Mevcut arenanın dönüş boşluğu seçimin önkoşuludur.

## Tekrar üretim

1. `Assets/Resources/Robots/Kama.prefab` dosyasını aç. Yerel +Z ileri, +Y yukarı olacak şekilde iki kol, iki uç, menteşe kirişi ve köprüyü düzenle. Root ölçeği (1,1,1), başlangıç konumu (0,0,0) kalır. Her parçaya okunabilir MeshFilter ekle. İçe aktarılan mesh kullanılacaksa Read/Write açık olmalıdır.
2. Prefab kökündeki `ForkLoadout` alanlarına `Assets/Settings/Combat/KamaFork.asset` ve malzemeyi bağla. Yeni dosyalarda Unity’nin ürettiği meta/GUID’leri işle. Seçici bu kaynak prefabının geometrisini oyuncudaki mevcut silah parçasına ve onun hasar kaplamasına takar. Geçerli bir profil, mesh veya arena yoksa Döner seçili kalır ve durum menüde görünür.
3. Hazır ekranda iki görünüm arasında geç. Başla → hasar → sonuç → tekrar/Ready döngüsünü çalıştır. Kullanılan model oyuncu kök isminde ve silah yolunda kayda girer. Eşleşmeyen model açık uyumsuzluk nedeni üretir. Doğru modele geri dönülünce aynı arşiv tekrar açılır.
4. Unity Test Runner, PlayMode, `KamaPlayModeTests`. Grafik aygıtı bulunan Windows oturumunda çalıştır. Test geçici bir dizinde iki maç geçmişi ve tekrarları üretir. Tarihsel bir Döner kaydını da açmak için editörü başlatmadan önce `ROBOT_ARENA_LEGACY_REPLAY` ortam değişkenine korunan .rpl kopyasının tam yolunu ver. Kaynak bütçesi değişirse ilgili testteki ölçümü yeniden al.
5. Temiz bir Git çalışma kopyasında LFS varlıklarını indir, `RobotArena` kökünü Unity 6000.4.4f1 ile aç ve importun tamamlanmasını bekle. Editör menüsünde **RobotSavaslari > Build Windows Player** çalıştır. Çıktı `RobotArena/Builds/Windows/RobotSavaslari.exe` altında oluşur. Komut satırı giriş noktası `RobotSavaslari.Editor.BuildWindowsPlayer.Build` şeklindedir.

## Windows doğrulaması · 24 Kasım 2026

Oynanan kaynak `e5c7a10908cab20e1c0f2ef3d82df26c0383c889`. Unity 6000.4.4f1, Windows, gerçek MainArena ve Open düzeni. `KamaPlayModeTests;ArenaRobotPlayModeTests` filtresinde **10/10 geçti**, 0 başarısız, 0 atlanan, 32,946 saniye.

| Kapsam | Doğrulanan sonuç |
| --- | --- |
| Menüden iki düello | IMGUI ile Maça başla → geri sayım → gerçek bot → sonuç → Ready. Döner 4, Kama 6 gerçek silah isabetiyle kazandı. Test hedefin pozisyonunu ve saldırı saatini belirledi. İlk isabette hedefin fizik gövdesinde Döner için 0, Kama için 2,4 m/s ileri hız ölçüldü. |
| Arşiv | Görevden önce kaydedilmiş v1 Mavi/Kırmızı Open düellosu Döner ile salt okunur açıldı. İki yeni kayıt kendi görünümüyle oynatıldı. Diğer görünüm seçilince uyumsuzluk bildirildi. |
| Parça, sahiplik ve maliyet | Aynı oyuncu, klavye sürücüsü, gövde collider’ı ve hasar parçası korundu. Çatalın 72 üçgeni, robotun 117.032 üçgeni ve çatalın kutu içindeki bütün köşeleri çalışma anında doğrulandı. |
| Olumsuz durumlar | Kaçan saldırı, cooldown, kopan çatal, mağlubiyet, retry ve eksik prefab durumları geçti. Varsayılanın özgün silah mesh ve profili geri geldi. |
| Mevcut arena | Klavye/silah girdisi, parça kopma ve onarım, çarpışma/itiş, arena duvarları, oyuncu seçimi, durdurulan saldırı ve teker animasyonu için 7 mevcut regresyon geçti. |

Yerel NUnit çıktısı `RobotArena/Logs/DrivingExerciseEvidence/robot-regression.xml`, SHA-256 `63869C5A0D23C3E0E7F5BBEC1F7D93DA8669EEDE5D936347463E9819B107B593`. Ham Unity günlüğü aynı klasördeki `robot-regression.log` dosyasıdır. Teste ait yeni geçmiş ve arşivler geçici klasörde tutuldu. Kullanıcı kaydı Git’e eklenmedi.

### Windows x64 build

Temiz görev kopyasından ilk Windows x64 build 01.01.52–01.03.04 arasında tamamlandı. Unity süreci 0 ile kapandı ve `RobotArena/Logs/DrivingExerciseEvidence/robot-windows.log` içinde `Build Finished, Result: Success.` kaydedildi. Derleme çıkışı `RobotArena/Builds/Windows` altındadır.

| Boyut ölçümü | Ortak test tabanı | Kama dalı |
| --- | ---: | ---: |
| Dağıtım dosyaları | 193 dosya / 500.579.242 bayt | 193 dosya / 500.590.501 bayt |
| Build klasörünün tamamı | 194 dosya / 500.919.127 bayt | 194 dosya / 500.930.386 bayt |

Ölçülen dağıtım farkı **+11.259 bayt**. Dağıtım sayımı `RobotArena_BurstDebugInformation_DoNotShip` klasörünü hariç tutar. Karşılaştırılan taban, kontrol kopyasındaki `checkpoint-6200ddc-build.log` ile belgelenen başarılı build’dir. Windows çalıştırıcısının SHA-256 değeri `413A04009D5FC13E03FE4E5C6941A8606C63C103EDD08853EFC6C9BD57E6151B`, yeni oyunu içeren `RobotSavaslari.Gameplay.dll` değeri `AAA401E9D55260054C772B986A72835F2AB3EBEE5585C3DEDB0F45514B4D348A`.

Paketlenmiş EXE `-arena-smoke` ile MainArena’yı açtı ve varsayılan Döner ile sanal klavye girdisinden iki maç tamamladı. İki sonuç da oyuncu galibiyeti, kalan canlar 66,08 ve 75; kopan parça sayıları 1 ve 2. Bot karşı saldırısı, temas hasarı, aşınma, tek sonuç ve temiz yeniden başlatma geçti. Süreç çıkışı 0. `RobotArena/Logs/DrivingExerciseEvidence/robot-player-smoke.log` SHA-256 `632B6173883CE1B13B3D502C9D7A9F79E025CC4A5701DFB9FE6FA008274AAC65`. Unity’nin çevrimiçi telemetri sunucuları için DNS uyarıları oluştu. Korumalı kayıtlar ve 45 kayıt defteri girdisi çalıştırmadan sonra doğrulandı.
