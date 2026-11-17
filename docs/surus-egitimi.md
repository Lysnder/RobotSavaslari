# MainArena sürüş eğitimi (Time Trial)

Ready ekranından açılan, sıralı kontrol noktaları, süre, yanlış sıra uyarısı,
bitiş ve yeniden deneme içeren oynanabilir eğitim. Ayrı bir demo sahnesi değil:
aynı MainArena, aynı robot, aynı fizik dünyası.

## Kullanım

MainArena **Ready** durumundayken alt ortadaki **Sürüş eğitimi** düğmesi
(sürüş tuşları düğmesinin hemen üstünde).

| Ekran | İş |
| --- | --- |
| Üst orta panel | Kontrol noktası ilerlemesi ve süre |
| Yeniden dene | Pozu, hareketi, kontrol noktalarını ve süreyi sıfırlar |
| Eğitimden çık | Sahneyi eski durumuna döndürür |

Eğitim kontrolü için klavye kısayolu **eklenmedi**: `R`, `Esc`, `Enter` ve
`Space` başka sahiplerde, yenilerini sahiplenmemek için kontrol yalnızca
ekrandaki düğmelerle.

## Rota

Güncel MainArena, `ArenaBounds` üzerinden 8 × 8 adet 2 m hücre ve 0.82 m
zemin yüksekliği kullanır. Beş kontrol noktası kenarlardan içeride, saat yönünde
bir tur oluşturur (yaklaşık 27 m).

Kontrol noktaları **collider taşımaz**; tespit mesafe ölçümüyle yapılır. İki
sebep: fiziğe sıfır müdahale ve testlerde deterministik davranış. Görsel
işaretler de yalnızca render'dır ve robota parent edilmez. İşaret merkezi
`ArenaBounds.SurfaceHeight + 0.03` yüksekliğindedir; sabit dünya y=0.03 kullanılmaz.
Arena bağlantısı olmayan bağımsız testlerde rota başlangıcının y değeri kullanılır.

Her `Retry`, oturum Running kalsa da `AttemptNumber` değerini artırır. GhostRunner
bu yeni denemede önceki örnekleri ve örnekleme saatini temizler.

## Giriş sırası — kaynaktan doğrulanmış tuzak

`RobotPrototypeController.OnDisable()` şunu içeriyor:

```csharp
if (_keyboardDriver != null) _keyboardDriver.enabled = false;
```

Yani oyuncunun Prototype'ını kapatmak sürücüyü de kapatıyor. Doğru sıra:

1. İki robotta `StopCombat()` — eğitimde hasar ve sonuç üretilmez.
2. `MatchController` ve `MainMenuScreen` kapatılır.
3. Rakip Prototype kapatılır.
4. **Oyuncu Prototype'ı kapatılır** — her fizik adımında `ResetMotion()`
   çağırabildiği için şart.
5. **Sonra** sürücü açılır.

Ters sırada eğitim hiç çalışmaz: adım 4 adım 5'i geri alır.

## Çıkış ve durum geri yükleme

Girişte `MatchController.enabled`, `MainMenuScreen.enabled`, iki Prototype'ın
`enabled` durumu ve sürücünün `enabled` durumu saklanır. Çıkışta hepsi geri
yüklenir, sonra mevcut `MatchController.Prepare()` çağrılır.

`Prepare()` kaynaktan okundu: durumu Ready yapar, iki robotu spawn'a döndürür,
`ResultCount`'u sıfırlar ve **`MatchCount`'a dokunmaz**. Eğitim boyunca
`MatchController` kapalı olduğu için zaten sonuç üretemez.

Ayarlar paneli açılırsa eğitim beklemez, güvenle çıkar.

## Kontrol noktası kuralı

* Sıradaki noktaya girmek ilerletir.
* Sıradaki **olmayan** bir noktaya girmek ilerletmez, turu geçersiz işaretler
  ve uyarı gösterir: *"Yanlis sira: once 1. noktaya git."*
* Bitiş yalnızca **bir kez** raporlanır; sonrasında süre işlemez.

Geçersiz tur tamamlanabilir ama `LastLapValid` false kalır.

## Yapı

| Dosya | Sorumluluk |
| --- | --- |
| `Scripts/Training/TimeTrialSession.cs` | Giriş/çıkış, durum saklama, sıra, süre, yeniden deneme |
| `Scripts/Training/TimeTrialHud.cs` | Ready erişimi, süre/ilerleme paneli, rota işaretleri |
| `Scripts/Match/DrivingFeatureInstaller.cs` | Bağlantı ve erişim koşulu |

Oturum Gameplay assembly'sinde: `MatchController`, `MainMenuScreen` ve
`RobotPrototypeController` orada ve Driving'den Gameplay'e referans bağımlılık
döngüsü yaratırdı.

Oturum maç bileşenleri olmadan da çalışır (hepsi null kontrollü), bu yüzden
testler maç kurulumunu taklit etmeden gerçek robotu kullanabiliyor.

## Bilinen sınır

Oyuncu Prototype'ı kapalıyken `ConstrainToArena()` da çalışmıyor; robotu arenada
tutan şey kafesin fiziksel duvarları oluyor. MainArena'da elle doğrulanmalı.

## Testler

`RobotArena/Assets/Tests/Training/PlayMode/TimeTrialSessionTests.cs` — 9 test:
girişin kontrolü açması ve başlangıca koyması, izin yokken girilememesi,
sıranın yalnız doğru yönde ilerlemesi, turun tamamlanması ve bitişin bir kez
raporlanması, yanlış sıralı turun geçersiz kalması, sürenin işlemesi, yeniden
denemenin her şeyi sıfırlaması, çıkışta saklanan kontrol durumunun geri
yüklenmesi, bitişten sonra sürenin durması.

17 Kasım 2026 doğrulaması: **184/184 tüm EditMode**, **32/32 eğitim PlayMode**,
**55/55 sürüş PlayMode**, **5/5 düello kamera PlayMode**. Yeni altı MainArena
regresyonu düzeltme öncesi 6 hata, sonrası 6 başarı üretti. PR #41 ile yerel
birleştirmede aynı altı kontrol ve dokuz maç testi de geçti.

[SHA, XML kanıtları ve komutlar](evidence/input-training/validation.json).

İlgili: [boost](surus-boost.md) · [el freni](surus-el-freni.md) ·
[sürüş tuşları](surus-tuslari.md)
