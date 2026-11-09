# B3 — Sonuç ekranı ve temiz yeni maç

Sahne: `Assets/Burak/Scenes/BurakB1.unity`. B1 ve B2 üstüne kuruludur.

## Çalıştırma

1. Sahneyi açıp Play'e bas, Başlat'a bas.
2. İsteğe bağlı “Yerel deneme durumunu kirlet” düğmesiyle canı azalt,
   yerel saldırı beklemesini 5'e çıkar ve yerel bot hedefini kaldır.
3. Rakibi yen: “Oyuncu kazandı” görünür. Oyuncuyu yen: “Rakip kazandı” görünür.
   İki tarafın komut izni sonuçta KAPALI'dır.
4. Her iki sonuçta da aynı “Yeniden başlat / Yeni maç” düğmesini kullan.
   Hazırla ve Başlat şeklinde iki işlem gerekmez: yeni maç otomatik Oynanıyor'a geçer.
5. Yeni maçta sonuç metni boştur, sonuç sayısı 0'dır, iki izin AÇIK'tır;
   robotlar spawn'larında tam canla başlar. Yerel bekleme 0, hedef Mavi Robot'tur.
6. Tekrar yenilgi bildir: sonuç sayısı yine 1 olur. Üç tur tekrarla.

## Sıfırlama düzeni ve entegrasyon noktaları

Ortak `ArenaMatchController.RestartMatch()` mevcut sahnedeki robotları yeniden
konumlandırıp `Restore()` ve `ConfigureCombat()` çağırır; sahne yüklemesi kullanmaz.
Burak denemesi aynı yerinde sıfırlama düzenini izler. SceneManager veya Build
Settings değişikliği gerekmez; ortak dosyalar değiştirilmedi.

`RestartMatch()` yalnız Sonuç durumunda kabul edilir ve hemen Restarting durumuna
geçer. İkinci tıklama, Başlat, Hazıra dön ve yenilgi bildirimleri bu sırada etkisizdir.
Düğme devre dışı görünür. Coroutine bir kare sonra `Prepare()` ve `StartMatch()`
çalıştırır. Eski maç kimlikleri yeni maçta reddedilir. Yeni abonelik eklenmez;
can olaylarına abonelik yalnız ilk Start'ta yapılır, OnDestroy'da kaldırılır.

Yeni dövüş ve bot bileşenleri eklendiğinde bağlantılar:

- **Can:** `Prepare()` içindeki `ResetRobot()` mevcut `RobotHealth.Restore()`
  metodunu çağırır. Yeni can bileşeni burada başlangıç canını ve hasar durumunu
  sıfırlamalı; komutlar açılmadan önce tamamlanmalıdır.
- **Saldırı beklemesi:** `StartMatch()` içindeki `ConfigureCombat()` mevcut ortak
  bileşenin `_nextHitTime`, `_nextImpactTime` ve `WeaponActive` alanlarını sıfırlar.
  Yeni saldırı bileşeninin bekleyen saldırıları ve cooldown'u aynı başlangıç
  noktasında temizlenmeli; gecikmiş işler önceki maç kimliğiyle iptal edilmelidir.
- **Bot hedefi:** aynı `StartMatch()` rakibe `ConfigureCombat(true, player)` ile
  oyuncuyu yeniden hedef atar. Yeni bot bileşeni burada yeni maçın oyuncu
  referansını almalı; önceki takip/karar durumunu temizlemelidir.

Panel gerçek canları ve ayrıca `DemoAttackCooldown`, `DemoBotTarget` yerel deneme
göstergelerini gösterir. Bu iki gösterge gelecekteki saldırı/bot bileşenlerini
taklit eden sıfırlama kanıtıdır; gerçek cooldown sayacı veya bot sistemi değildir.

## Doğrulama — 9 Kasım 2026

Unity 6000.4.4f1 Play Mode'da B1 ve B2 regresyonları geçti.
`BurakB3Checks.Begin()` ile kareler arasında çalışan B3 kontrolleri geçti:

- Galibiyet ve yenilgiden sonra aynı yeniden başlatma metodu.
- Her turda 25 hızlı ek çağrının reddi; maç sayısında yalnız bir artış.
- Üç ardışık yeni maçta konum, can, eski hareket, sonuç ve yerel göstergelerin temizliği.
- Eski maç bildiriminin reddi, yalnız iki robot, yeni maçta tek sonuç olayı.
- Dört sonuçta dinleyici tam dört kez çağrıldı; olay birikmesi gözlenmedi.
- Oyun kare sayısı 1618'den 3855'e ilerledi; test donmuş karede yapılmadı.
- Console ve derleme hatası yok. `B3-result.png` incelendi; kazanan, yeniden
  başlatma düğmesi, tek sonuç ve iki kapalı izin birlikte görünür.

Tekrar test: temiz Play oturumunda Pipeline `script eval` ile
`return RobotSavaslari.Burak.Editor.BurakB3Checks.Begin();` çağır.
Sonraki çağrıda `return RobotSavaslari.Burak.Editor.BurakB3Checks.Status;`
ile PASS sonucunu oku. Test sonunda sonuç ekranı açık kalır.

B2'nin eşzamanlı yenilgi kuralı hâlâ Emin'in kararını bekler. B1'deki ortak
prefab/spawn ve başlangıç commit'i açık maddeleri de değişmedi. B3 bu ortak
kararları veya ilk birleşik maç/P05B işini üstlenmez. PR açılmadı.
