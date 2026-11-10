# Meshy ve Blender modüler robot ilk seti

Kullanıcının `robots.docx` belgesindeki modüler robot hedefi için yeni ve bağımsız
bir görsel montaj seti. Mevcut proje **URP**; belgede geçen HDRP'ye geçilmedi.
Ortak Arena ve mevcut iki robot modeli değiştirilmedi.

## İçerik

- Meshy Smart Topology ile ayrı üretilen şasi, dikey disk, drum, yatay bar ve
  hafif/ağır zırh yüzeyleri; Meshy PBR dokuları.
- Blender'da üretilen tekrar kullanılabilir tekerlek, mil/göbekler, yerel
  rotor pivotları, tekerlek/yan zırh/silah montaj noktaları.
- Yedi bağımsız modül için üç GLB seviyesi; Unity LODGroup prefab'ları.
- Silah ve zırh takma/sökme, zırh tipi değiştirme ve rotor hareketi deneme sahnesi.

Unity: `Scenes/ModularRobotPreview.unity` sahnesini aç ve Play'e bas.
Düğmelerle üç silahı, iki zırhı ve sökme işlemini dene. “Pivot hareketini göster”
rotoru çevirir; robotu döndürme düğmesi modelin çevresini incelemeyi sağlar.
Bu bir görsel montaj prototipidir; dövüş/hasar/yükseltme istatistikleri ve ağ
senkronizasyonu bağlı değildir. Silah setleri bu aşamada mekanik sınıfı gösterir;
üretim öncesinde mil bağlantısı ve dönme hacmi ayrıca gözden geçirilmelidir.

## Ölçülen bütçe

`validated-budget.json`, gerçek GLB indekslerinden hesaplanan değerleri içerir.
Takılı silah ve zırha göre tam robot:

| Seviye | Üçgen sayısı |
| --- | ---: |
| LOD0 | 27.947–32.093 |
| LOD1 | 13.973–16.045 |
| LOD2 | 3.342–3.839 |

Tek LOD0 kombinasyonunun benzersiz modül dosyaları yaklaşık 8,0–8,1 MB'dır.
Tekerlek ve zırh örnekleri aynı asset'i yeniden kullanır. Bu rakam tüm setin
ve üç LOD'un toplamı, FBX boyutu veya Unity bellek kullanımı değildir.
Şasi dokuları en çok 2048; küçük modüller en çok 1024 piksel.
Küçük civata/aşınma detayları Meshy PBR yüzeylerinde korunur.
LOD dosyalarında dokular gömülü olduğundan LOD'lar arasında doku kopyaları vardır;
yayın öncesinde ortak materyal/doku atlası ve 8 robot performansı ölçülmelidir.

## Koordinat ve montaj standardı

Blender'da metre, Z yukarı, robot önü -Y. GLB dışa aktarımında glTF Y-up dönüşümü
uygulanır. Modül kökü sıfır poz/dönüş ve birim ölçektedir. Unity'de prefab'ı
socket altına `Instantiate(asset, socket, false)` ile yerleştir; görselin iç
dönüşü korunur. Dosyayı yeniden boyutlandırmak yerine Blender kaynağını güncelle.

| Socket | Blender yerel konumu | Amaç |
| --- | --- | --- |
| WeaponMount_A | (0, -.88, .38) | Üç silah için aynı yerel kök |
| WheelMount_FL / FR | X=-.66 / +.66, Y=-.4, Z=.22 | Ön tekerlekler |
| WheelMount_RL / RR | X=-.66 / +.66, Y=+.4, Z=.22 | Arka tekerlekler |
| ArmorMount_L / R | X=-.5874 / +.5874, Y=0, Z=.36 | İki yan zırh seti |

`Rotor_vertical` ve `Rotor_drum` Unity yerel X ekseninde;
`Rotor_horizontal` Unity yerel Y ekseninde döner. Sabit mil rotorun dışında kalır.
Her modülün LOD renderer'ları aynı LODGroup içinde; tüm rotor LOD'ları birlikte döner.

İlk set iki yan zırh alternatifi içerir; ön zırh, iç şasi parçalarının sökülmesi,
bağlantı toleranslı CAD modeli veya tüm robot sınıfları henüz bu sette yoktur.
Yeni hasar sistemi eski `RobotDamageVisuals` alan-kesme kodunu doğrudan kullanmamalı;
ayrı mesh'leri doğrudan dayanıklılık/bağlantı bileşenleriyle eşleştirmelidir.

## Yeniden üretim ve kaynaklar

Meshy API görevleri ve orijinal PBR GLB'ler:
`RobotArena/ArtSource/ModularKit/` (Git tarafından bilinçli olarak yok sayılır).
`ModularKit_Master.blend` düzenlenebilir modülleri içerir;
`ModularKit_Preview.blend` üç montajın sunumudur. Bu yerel kaynaklar ortak art
deposuna ayrıca yedeklenmeden ekip için erişilebilir sayılmaz.

Üretim kodu: `Tools/RobotArt/meshy_kit.py` ve `build_modular_kit.py`.
Meshy anahtarı kodda/sahnede/kayıtta tutulmaz; yerel ortam veya mevcut Codex
Meshy yapılandırmasından yalnız süreç içinde okunur. POST istekleri otomatik
tekrarlanmaz; belirsiz üretim sonucu yeniden ücretlendirmeden önce uzlaştırılmalıdır.

Blender: `blender.exe --background --python-exit-code 1 --python Tools/RobotArt/build_modular_kit.py`
Dosya kontrolü: `python Tools/RobotArt/check_modular_kit.py`
Unity menüsü: `RobotSavaslari > Burak > Build Modular Robot Art Preview`.
Ham kaynaklardan yeniden export edilir; eski optimize çıktılar tekrar tekrar
sadeleştirilmez. GLB dosyaları mevcut Git LFS kuralıyla izlenir.

Meshy MCP yerel yapılandırmada kayıtlıydı, bu sohbetin araçlarına sunulmamıştı;
aynı hesabın resmi API'si üzerinden geometri ve PBR üretimi yapıldı. MCP sunucusu
veya kullanıcı ayarları değiştirilmedi. Bu nedenle MCP'nin çalıştığı iddia edilmez.
Teknik referans: https://docs.meshy.ai/en/api/text-to-3d

## Bu oturumdaki doğrulama

9 Kasım 2026: altı geometri ve altı PBR görevi tamamlandı. Bakiye farkı
90 Meshy kredisi (2370 → 2280). Ek ücretli tekrar üretim yapılmadı.
GLB/üçgen/doku/socket/LOD kontrolleri geçti. Unity Play Mode'da üç silahın
takılıp sökülmesi, iki zırh seti, zırh sökme ve her modülün üç LOD'u doğrulandı.
Sekiz etkin modül grubu vardı (şasi, dört tekerlek, silah, iki zırh).
Son oturumda derleme/Console hatası yoktu. Kare sayısı 5353 → 11794 ilerledi.
Rotor adı eşleşmesindeki Blender sayısal son ek hatası giderildi.

Görsel değerlendirme ilk tasarım iterasyonudur: şasi, silah yüzeyleri ve zırhlar
AI kaynaklıdır; elle mekanik toleranslı yeniden modelleme yapılmadı. Özellikle
dikey disk ve yatay bar siluetleri, kaynak görüntüleriyle karşılaştırmalı olarak
sonraki görsel revizyonda iyileştirilebilir. Kaynaklar .blend olarak korunmuştur.
