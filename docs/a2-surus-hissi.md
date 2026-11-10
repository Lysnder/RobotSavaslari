# A2 · Sürüş hissi ve tekrarlanabilir ayarlar

## Deneme sahnesi

**`RobotArena/Assets/Scenes/Atalay/A2_SurusHissi.unity`**

Dört işaretli rota, hepsi kendi başlangıç noktasından aynı yönle başlar:

| Rota | Ne ölçülür |
| --- | --- |
| 1 · Düz hızlanma | Hızlanma ve fren mesafesi |
| 2 · Kısa zikzak | Yön değiştirirken dönüşün gecikmesi |
| 3 · Dar dönüş | Dönüş yarıçapı |
| 4 · Geri çıkış | Çıkmazdan geri sürüşle kurtulma |

## Çalıştırma

`A2_SurusHissi` sahnesini aç, Play'e bas, Game görünümüne tıkla.

| Tuş | İş |
| --- | --- |
| `W` `S` `A` `D` | sür |
| `R` | rotayı baştan al (robot başlangıç noktasında, duruşta doğar) |
| `Tab` | sonraki rota |
| `1` / `2` | ayar setini değiştir (robot aynı noktada yeniden doğar) |
| `C` | kontrolü kapat / aç |
| `P` | deneme kontrolü (klavyesiz sürüş) |

Sağ üstteki düğmeler de aynı işi yapar. Sol üstteki telemetri hangi rotada,
hangi ayarla ve kaç saniyedir sürdüğünü gösterir.

## Ayar setleri

Ayarlar ortak prefab'ın **varyantlarında** durur, ortak prefab değişmez:

* `RobotArena/Assets/Prefabs/Atalay/Robot_AyarA_Sabit.prefab`
* `RobotArena/Assets/Prefabs/Atalay/Robot_AyarB_Cevik.prefab`

| Ayar | A · Sabit | B · Çevik |
| --- | --- | --- |
| Üst hız | 5 m/s | 7.5 m/s |
| Geri hız | 2.5 m/s | 4 m/s |
| İvme | 20 | 26 |
| Fren | 30 | 20 |
| Dönüş hızı | 140 °/s | 230 °/s |
| Dönüş ivmesi | 520 | 900 |
| Dönüş freni | 900 | 1200 |

## Ölçümler

`DriveFeelMeasurementTests` koşusundan, her iki set için aynı sürüşle:

| Ölçüm | A · Sabit | B · Çevik |
| --- | --- | --- |
| Üst hıza çıkma | 1.0 s | 0.5 s |
| Fren mesafesi (üst hızdan) | 0.23 m | 0.76 m |
| 180° dönüş çapı | 5.38 m | 4.35 m |
| İleriden geriye geçiş | 0.120 s | 0.240 s |

### Gözlemler

* **A · Sabit** kontrolü kolaylaştırıyor: çeyrek metrede duruyor ve ters komutu
  yarı sürede alıyor. Dar alanda, duvar dibinde ve köşede hata payı bırakıyor.
* **B · Çevik** daha hızlı dönüyor (çapı 1 m dar) ama 0.76 m fren mesafesiyle
  duvara giriyor. Açık alanda ve zikzakta avantajlı, köşede affetmiyor.
* Dönüş çapının B'de daha küçük çıkması hızın değil dönüş hızının sonucu:
  B %50 daha hızlı gidiyor ama %64 daha hızlı dönüyor.

**Seçim: A · Sabit.** Arena duvarlı ve dar; fren mesafesi ile ters komut
gecikmesi burada dönüş çapından daha belirleyici. B, açık alan denemeleri için
varyant olarak duruyor.

## Kare hızı karşılaştırması

Aynı 1.5 saniyelik tam gaz sürüş, üç farklı fizik adımında:

| Fizik adımı | Alınan yol |
| --- | --- |
| 0.0100 s (100 Hz) | 6.008 m |
| 0.0200 s (50 Hz) | 5.970 m |
| 0.0333 s (30 Hz) | 5.911 m |

En büyük sapma **%1.6**. Sürüş kare hızına bağlı değil; test %8 sapmada düşecek
şekilde bağlanmış.

## Ortak motorda bulgu — Emin'e

**Belirti:** İvmesi ~12 m/s²'nin altında olan hiçbir ayar seti robotu üst
hızına çıkaramıyor. İvme 12 ile robot 3 saniyede ancak 0.95 m/s'e ulaşıyor,
ayarlanan üst hız 5 m/s.

**Ölçüm:** Ayarlanan ivme 12.0 m/s², ölçülen net ivme 0.23 m/s², yani örtülen
sürtünme tabanı **11.8 m/s²**.

**Sebep:** `RobotMotor.FixedUpdate` her adımda gövdenin gerçek hızını geri
okuyup ondan hızlanıyor (`forwardSpeed = Vector3.Dot(body.linearVelocity, …)`).
Bu, duvara dayanınca hızın kendiliğinden sıfırlanmasını sağlıyor — istenen
davranış — ama aynı zamanda zemin sürtünmesini her adımda ivmeden düşüyor.
Kullanılabilir ivme = ayarlanan ivme − 11.8.

**Tekrar adımları:** `RobotSavaslari.Atalay.PlayModeTests` >
`DusukIvmeliAyarSurtunmeTabaninaTakilir`. Test ayarlanan ivmeyi, ölçülen net
ivmeyi ve örtülen sürtünmeyi yazdırır.

**Etkisi:** Ayar setlerinin alt sınırı yapay olarak 20'ye çekildi. Düşük ivmeli,
ağır bir robot hissi bugün kurulamıyor. Düzeltme ortak motorda olacağı için
karta alınmadı.

## Kabul maddeleri

| Madde | Durum |
| --- | --- |
| Dört rota seçilen ayarla art arda tamamlanabiliyor | Sahnede elle (rotalar `Tab` ile sırayla) |
| Başka bir geliştirici kayıtlı varyantı açınca aynı ayarları yükleyebiliyor | ✅ EditMode testi değerleri doğruluyor |
| İleri–geri yön değişiminde ters komut gecikmeden algılanıyor | ✅ 0.120 s / 0.240 s ölçüldü |
| Kare hızı karşılaştırması kaydedilmiş | ✅ %1.6 sapma, üç fizik adımı |
