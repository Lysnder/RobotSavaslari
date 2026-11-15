# Sürüş özelliklerinde el ile doğrulama

Otomatik testlerin kapsamadığı dört şey kaldı. Üçü **gerçek bir süreç yeniden
başlatması** gerektiriyor (batch mode testinde süreç hiç kapanmıyor), biri de
görsel/his değerlendirmesi.

Beklenen sayılar bileşenlerin varsayılan değerlerinden gelir; farklı çıkarsa
bileşende ayar değişmiş demektir.

## Hazırlık

MainArena'da **Play**. Ekranın altında `Sürüş tuşları` ve onun üstünde
`Surus egitimi` düğmeleri görünür. İkisi de yalnızca maç **Ready** durumundayken
ve ayarlar kapalıyken çıkar.

| Tuş | İş |
| --- | --- |
| `W` `S` `A` `D` | Sürüş |
| `LeftShift` | Boost |
| `LeftCtrl` | El freni |

## 1 · Boost enerjisi ve duvar teması

| Adım | Beklenen |
| --- | --- |
| `W` basılı, düz hızlan | Göstergede enerji 100, hız ~17 m/s'e oturur |
| `W` + `LeftShift` basılı tut | Hız ~26 m/s'e çıkar (×1.5), enerji saniyede 40 düşer |
| Basılı tutmaya devam et | ~2.5 s sonra enerji biter, **boost kendiliğinden kesilir**, hız normale döner |
| `LeftShift`'i bırak, bekle | 1 s gecikmeden sonra enerji saniyede 25 dolar; tam dolum ~4 s |
| Enerji 5'in altındayken `LeftShift` | Boost **hiç** devreye girmez (eşik 5) |
| `S` + `LeftShift` (geri) | Boost uygulanmaz, geri hız değişmez |
| Boost'la kafes duvarına dayan | Robot duvarı geçmez, takılıp titremez; bırakınca geri sürülebilir |
| Maç bitir, `R` ile yeni maç | Enerji 100'e dönmüş olur |

Enerji bitmesini ve dolumunu otomatik test de ölçüyor
(`EnerjiBitinceBasiliBoostDurur`, `BoostEnerjiHarcarVeSonraYenilenir`); buradaki
asıl konu **duvar teması** ve göstergenin okunurluğu.

## 2 · El freni hissi

Aynı hızda iki tur at, birini normal birini el frenli:

| Adım | Beklenen |
| --- | --- |
| ~5 m/s'te `A` ile dön | Bir dönüş yarıçapı, hız korunur |
| ~5 m/s'te `A` + `LeftCtrl` | Dönüş belirgin biçimde keskinleşir (×1.8), hız düşer (×0.55) |
| Dururken `LeftCtrl` + `A` | El freni devreye **girmez** (eşik 1.5 m/s), normal dönüş olur |
| `LeftCtrl` + `LeftShift` birlikte | El freni kazanır; boost uygulanmaz |
| `LeftCtrl`'ü bırak | Hız ve dönüş sınırı anında normale döner |

Sayısal karşılaştırmayı `ElFreniAyniHizdaDahaKeskinDondururVeHizKaybettirir`
zaten doğruluyor; burada bakılan şey bunun **sürülebilir** hissedilmesi.

## 3 · Tuş eşlemeleri gerçek yeniden başlatmada kalıyor mu

Bu maddenin otomatik testi yok, çünkü PlayerPrefs'in diske yazılması ve yeni bir
süreçte okunması gerekiyor.

1. Windows sürümünü çalıştır (`RobotArena/Builds/...` ya da Unity'den **Build**).
2. `Sürüş tuşları`'na gir, **İleri**'yi `W`'den başka bir tuşa al — örneğin `I`.
3. Çakışan bir tuş dene: `A`. **Reddedilmeli** ve ekranda sebep yazmalı.
4. Ayrılmış bir tuş dene: `Space` ya da `Esc`. Bunlar da reddedilmeli.
5. Ekranı kapat ve **gerçekten o tuşla sür** — `I` ileri götürmeli, `W` hiçbir şey yapmamalı.
6. **Oyunu kapat, yeniden aç.** `I` hâlâ ileri olmalı.
7. `Varsayılana dön` → `W`/`S`/`A`/`D`/`LeftShift`/`LeftCtrl` geri gelmeli, başka hiçbir ayar (ses, çözünürlük) değişmemeli.

## 4 · Hayalet rekoru gerçek yeniden başlatmada kalıyor mu

Kayıt `%USERPROFILE%\AppData\LocalLow\<şirket>\<ürün>\driving-personal-best.json`
altında durur.

1. `Surus egitimi` → turu tamamla. Panelde `Tur: x.xx s` çıkar.
2. `Yeniden dene` → bu kez hayalet görünür (saydam, gölgesiz robot), seninle aynı saatte ilerler.
3. Daha **yavaş** bir tur at → panelde rekor değişmemeli.
4. Daha **hızlı** bir tur at → rekor güncellenmeli.
5. Kontrol noktalarını **sırasız** geç → `Gecersiz tur` yazmalı, rekor ezilmemeli.
6. `Egitimden cik` → hayalet ve rota işaretleri kaybolmalı.
7. **Oyunu kapat, yeniden aç**, eğitime gir → rekor ve hayalet geri gelmeli.
8. JSON dosyasını elle boz (içine çöp yaz) → eğitim rekorsuz açılmalı, hata vermemeli.

## 5 · Eğitim ↔ maç geçişi

Otomatik test maç sayaçlarını zaten koruyor
(`EgitimGirisCikisiMacSayaclariniDegistirmez`), ama ekranda da bakılmalı:

1. Eğitime gir, çık → maç **Ready**'de durmalı, sonuç metni değişmemeli.
2. Normal maç oyna, bitir.
3. Tekrar eğitime gir → çalışmalı, rekor korunmuş olmalı.

### Bilinmesi gereken davranış

Eğitim sırasında oyuncunun `Prototype` bileşeni kapalı — her fizik adımında
`ResetMotion` çağırdığı için kapatılması gerekiyor. Bunun yan etkisi:
`ConstrainToArena` da o süre boyunca devre dışı. Robotu arenada tutan şey o anda
**kafes duvarlarının fiziği**. Kenarları zorlarsan robot duvarda durur ama
yazılımsal sınırlama yoktur; bu beklenen davranış, hata değil.

## Otomatik kapsam

| Konu | Test |
| --- | --- |
| Sahnede bağlantı, çoğaltmama | `MainArenaDrivingIntegrationTests` (6 test) |
| Boost enerjisi, eşik, geri sürüş, sıfırlama | `DriveBoostTests` (10) |
| El freni, öncelik, eşik | `DriveHandbrakeTests` (9) |
| Eşleme, çakışma, bozuk kayıt | `DriveBindingsTests` (13) + `DriveBindingsScreenTests` (8) |
| Çarpışma sonrası kontrol | `CollisionRecoveryTests` (7) |
| Eğitim akışı | `TimeTrialSessionTests` (11) |
| Hayalet verisi ve koşusu | `GhostLapTests` (13) |

Son koşu: **89/89 PlayMode**, **49/49 EditMode**.

İlgili: [boost](surus-boost.md) · [el freni](surus-el-freni.md) ·
[tuşlar](surus-tuslari.md) · [eğitim](surus-egitimi.md) ·
[hayalet](surus-rekor-hayaleti.md)
