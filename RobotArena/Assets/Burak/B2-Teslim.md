# B2 — Galibiyet, yenilgi ve tekrar bildirimleri

Sahne: `Assets/Burak/Scenes/BurakB1.unity` (B1 üstüne kurulan Burak deneme sahnesi).
Ortak Arena, robot hareket kodu, proje ayarları ve diğer geliştiricilerin işleri değiştirilmedi.

## Çalıştırma ve kabul

1. Sahneyi açıp Play'e bas. Hazır durumunda iki yenilgi düğmesini de dene:
   bildirim yok sayılır, sonuç sayısı 0, iki izin KAPALI kalır.
2. Başlat, sonra Rakibi yen: Oyuncu kazandı; sonuç sayısı 1, iki izin KAPALI.
3. Aynı düğmeye ve Oyuncuyu yen düğmesine tekrar bas:
   YOK SAYILDI gösterilir; galip ve sonuç sayısı değişmez.
4. Hazıra dön, Başlat, Oyuncuyu yen: Rakip kazandı; sonuç sayısı 1,
   iki izin KAPALI. Rakibi yen bildirimi artık bu sonucu değiştiremez.

Deneme düğmeleri can azaltmadan doğrudan `NotifyDefeat(GameObject robot, int matchId)`
bağlantısını kullanır. Böylece can zaten sıfırlanmış olsa bile tekrar bildirim yolu
sınanabilir. Yalnız atanmış iki robotun kök GameObject kimliği kabul edilir.
`MatchCount` geçerli maç kimliğidir. Gecikmeli/asenkron üretici bu kimliği işe
başlarken saklamalı ve bildirimle göndermelidir. Eski maç kimliği reddedilir.
Mevcut `RobotHealth.Defeated` olayı da aynı girişe bağlanmıştır.

`Outcome` programatik sonucu, `ResultCount` o maçtaki kesin sonuç sayısını verir.
`ResultDetermined` ilk sonuçta bir kez yayınlanır. Önce durum kilitlenir ve iki
robot durdurulur; ardından dinleyiciler çağrılır. Dinleyiciden iç içe gelen bildirim
de ikinci sonuç üretemez. Hazıra dönmek yeni maç için sonucu ve sayacı temizler.

## Test sonucu — 9 Kasım 2026

Unity 6000.4.4f1, temiz Play oturumunda `BurakB2Checks.Run()` geçti:

- İki ayrı maçta oyuncu ve rakip galibiyeti.
- Maç öncesi, 25'er tekrarlı, karşı taraftan geç gelen ve iç içe bildirimlerin reddi.
- Geçersiz/null robot kimliği ve önceki maç kimliğinin reddi.
- Maç başına tek sonuç olayı; sonuç olayı sırasında iki iznin de kapalı olması.
- Hazıra dönüşte temizleme ve mevcut can bileşeninin aynı bağlantıyı kullanması.

B1 regresyon kontrolleri de yeni Play oturumunda geçti. Console hata sayısı ve
derleme hatası sıfır. Sonuç paneli `B2-result.png` ekran görüntüsünde incelendi:
oyuncu galibiyeti, tek sonuç, yok sayılan sonraki bildirim ve iki kapalı izin
birlikte görünür. Play kare sayısı 3263'ten 5806'ya ilerledi.

Tekrar çalıştırmak için yeni Play oturumunda Pipeline `script eval` üzerinden
`return RobotSavaslari.Burak.Editor.BurakB2Checks.Run();` çağır.
Kod değişirken açık olan Play oturumunu durdurup yeniden başlat; can bileşeninin
çalışma anındaki event abonelikleri script reload ile kaybolabilir.

## Açık kabul maddesi — Emin'in kararı

Kullanıcı eşzamanlı yenilgi için henüz karar verilmediğini teyit etti.
Beraberlik uygulanmadı. Şimdilik ilk geçerli çağrı sonucu kilitler; bu davranış
eşzamanlı yenilgi için onaylanmış ekip kuralı sayılmaz.

Emin'den “aynı değerlendirme anı” sınırını (aynı fizik adımı mı, aynı kare mi)
ve iki yenilgide beraberlik istenip istenmediğini netleştirmek gerekiyor.
Beraberlik seçilirse bildirimler bu sınır boyunca toplanmalı ve ayrı senaryoda
iki geliş sırasıyla test edilmelidir. Karar ve bu senaryo tamamlanmadan kartın
eşzamanlı yenilgi kabul kutusu kapatılmamalıdır.

B1 teslim notundaki ortak prefab/spawn ve resmi başlangıç commit'i açık maddeleri
bu kartta da devam eder. PR açılmadı; bu dosya PR'a aktarılacak teslim notudur.
