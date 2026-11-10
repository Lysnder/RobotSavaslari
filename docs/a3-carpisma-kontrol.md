# A3 · Çarpışmadan sonra kontrol ve girdi güvenliği

## Deneme sahnesi

**`RobotArena/Assets/Scenes/Tests/Driving/CollisionRecovery.unity`**

Dört senaryo, her biri kendi başlangıç noktasıyla:

| Rota | Senaryo | Giriş |
| --- | --- | --- |
| 5 · Önden çarpışma | Düz duvar | Tam gaz, 0° |
| 6 · Açılı çarpışma | 30° eğik duvar | Tam gaz, yaklaşık 30° |
| 7 · Köşeye sürme | İki duvarın köşesi | Tam gaz, köşeye doğru |
| 8 · Duvar boyunca | 42 m uzunluğunda duvar | Duvara yaslanarak ileri |

Tuşlar A2 ile aynı: `R` baştan al, `Tab` sonraki rota, `1`/`2` ayar seti,
`C` kontrolü kapat/aç, `P` deneme kontrolü.

## Çarpışma kaydı

`CollisionReporter` her çarpışmayı telemetriye yazar:

```
Carpisma: 6.0 m/s, 0 derece -> kalan 0.5 m/s
```

* **Giriş hızı** `collision.relativeVelocity` üzerinden okunur. Gövdenin o anki
  hızı çarpışma çözüldükten *sonraki* değeri verdiği için kullanılmaz — ilk
  ölçümde bu yüzden "kalan hız giriş hızından büyük" gibi imkânsız sayılar
  çıkmıştı.
* **Açı** robotun burnu ile duvar normali arasından: 0° tam önden, 90'a
  yaklaştıkça yalayarak.
* **Kalan hız** çarpışmadan sonraki fizik adımında okunur. Komut kesilmişken
  robot hâlâ hareketliyse, telemetride ayrılan şey budur.

## Odak güvenliği

`PlayerKeyboardDriver.OnApplicationFocus` pencere arkaya düştüğünde girdiyi ve
komutu temizler. Odak geri geldiğinde değerler o karede klavyeden yeniden
okunur, yani **tuşa basılmadıkça robot komut almaz**. Telemetrideki kaynak
satırı bu durumda `odak disinda - girdi okunmuyor` yazar.

Kontrol kapatma `C` tuşuyla ya da sağ üstteki düğmeyle. Kapanırken motora
`Stop()` gider; açılırken eski değer kullanılmaz.

## Ölçümler

| Ölçüm | Değer |
| --- | --- |
| Önden çarpışma giriş hızı | 6.00 m/s, 0° |
| Çarpışmadan sonra kalan hız | 0.51 m/s |
| Komut kesildikten sonra kalan hareket | 0.38 m |

## Kabul maddeleri

| Madde | Durum |
| --- | --- |
| Seçilen duvar ve köşe senaryolarında robot arena dışına geçmez | ✅ dört otomatik test |
| Köşede kalan oyuncu geri çıkıp yönünü değiştirebilir | ✅ köşeden uzaklaşma ve dönüş ölçülüyor |
| Odak geri gelince tuş basmadan robot komut almaz | ✅ otomatik test |
| Kontrol kapalıyken sürüş girdisi motora ulaşmaz, açılınca eski değer kullanılmaz | ✅ otomatik test |

Testler: `RobotArena/Assets/Tests/Driving/PlayMode/CollisionRecoveryTests.cs`
(7 test) — önden çarpışma sonrası geri sürüşle çıkma, açılı çarpışma sonrası yön
düzeltme, köşeden geri çıkma, duvar boyunca sürüşte kontrolün kaybolmaması, odak
kaybı, odak dönüşü, hareketsiz ve hızlı robotun ayrı ayrı durdurulması.

## Düzeltilenler

* Çarpışma giriş hızı yanlış okunuyordu (çözüm sonrası değer). `relativeVelocity`
  ile düzeltildi.
* Odak kaybında eski komut duruyordu. `OnApplicationFocus` eklendi.

Ortak collider ya da ortak motor değişikliği gerekmedi; iki düzeltme de
Atalay'ın kendi bileşenlerinde.
