# Windows OS giriş kabulü — 18 Kasım 2026 devam raporu

**Güncel karar: Emin, A–H akışlarını uygulamadan kabul etmek istediğini bildirdi. Test sonucu: sekiz akış çalıştırılmadı; kullanıcı kabul kararı: uygulama yapılmadan kabul edildi.** Masaüstü erişimi engeli sürüyor. Bu karar başarılı test veya geri yükleme kanıtı değildir; main'e merge talimatı verilmedi.

18 Kasım açıklaması: Emin, testlerin uygulanıp uygulanmadığı sorusuna “Uygulamadım; geçmiş kabul edelim demek istedim”, geri yükleme sorusuna “Henüz geri yükleme/doğrulama yapılmadı” yanıtını verdi. [Karar kaydı](evidence/windows-acceptance-20261118/user-acceptance-decision.json). Önceki bekleyen insan kabulü planının yerini bu kullanıcı kararı aldı.

## Doğrulanan sürüm

- Başlangıç PR başı: `80ea1b57fd0935799bd12aeb6d3a0dbf60ef2936`; dal `fix/validation-reliability`, PR [#43](https://github.com/Lysnder/RobotSavaslari/pull/43), hedef `test`.
- Doğrulanan oyun kaynağı: `5de681341b31ffb81866adec3a2e2790b8abf364`.
- Her iki commit'in `RobotArena` ağacı: `f0be42bc323521f557fbc6992205b1bda1c75b86`.
- Build kimliği: `sha256:bd78388612c8197b5439e91fcb0a1851c75a49f4115034af10bf97429e4a6960` (kısa: `bd78388612c8`).
- EXE: `C:\Users\emins\Documents\Codex\robot-verify-93b4ded\RobotArena\Builds\Windows\RobotSavaslari.exe`.
- EXE SHA256: `413A04009D5FC13E03FE4E5C6941A8606C63C103EDD08853EFC6C9BD57E6151B`.

Manifestteki 194 dosya 09:15 UTC'de Python ile, 09:18 UTC'de native Windows PowerShell ile yeniden kontrol edildi; boyut/hash uyuşmazlığı yok. Build kimliği, manifest yollarının ordinal sırasıyla `yol + TAB + büyük harf SHA256 + LF` metninin UTF-8 SHA256'sıdır. Native Windows yol/EXE de doğrulandı: Windows NT 10.0.26300.0, interaktif kullanıcı oturumu. İnteraktif Windows sürecinin çalışması, masaüstü aracının çalıştığı anlamına gelmez.

Oyun/sahne/test kaynağı değiştirilmedi; build yeniden üretilmedi. Önceki 197 EditMode + 213 PlayMode, 48 final bot maçı ve otomatik Windows smoke sonuçları [önceki rapora](validation-20261118.md) aittir. Bu devamda yeniden çalıştırılmış sayılmazlar. Kabul hazırlığı ve kanıtlar `RobotArena` dışında tutulur; oyun kaynak ağacı aynı kalır.

## Masaüstü engelinin yeniden değerlendirilmesi

İstek ortamı artık `/home/lysander/.codex/worktrees/robot-native-validation/robot-verify-93b4ded`. Resmi `@oai/sky` ile `list_windows` denemesi, JavaScript başlamadan şunu döndürdü:

```text
Mcp error: -32602: js: codex/sandbox-state-meta:
sandboxCwd is not a local file URI:
file:///home/lysander/.codex/worktrees/robot-native-validation/robot-verify-93b4ded
```

Önceki kayıttaki eski `/mnt/c/...` dizini artık iletilmiyor. Buna rağmen doğru güncel POSIX URI de reddediliyor. Bu, sadece eski dizinin taşınmasıyla çözülmediğini gösterir; native Windows sağlayıcısıyla oturum ortamının uyumluluğu hâlâ engeldir. Araç içinde Windows okumasına, ekran görüntüsüne veya giriş denemesine ulaşılamadı. Oyuna tek bir OS girdisi gönderildiği iddia edilmiyor.

Önceki linked/managed worktree ve kernel sıfırlama denemeleri tekrarlanmadı. Mevcut araç adları içinde başka kullanılabilir gerçek Windows giriş aracı yoktu. Native süreç başlatma, GUIView testleri veya sanal InputSystem aynı kabulü sağlamaz. Özel yardımcı protokol, PowerShell UI Automation veya SendInput kullanılmadı.

Otomasyon için gereken yeni oturum koşulları: native Windows yürütme host'u, `C:\Users\emins\Documents\Codex\robot-verify-93b4ded` proje yolu, aynı kullanıcıya ait açık/kilitsiz interaktif masaüstü ve Windows'a uygun çalışma dizini metadata'sı. Önce resmi `sky.list_windows` gerçekten çalışmalı; sonra oyun penceresi gözlenip zararsız bir gerçek tıklama doğrulanmalı. Böyle bir oturumun bu çalışmada başarıyla oluşturulduğu söylenmiyor. Elle kabul bu teknik engeli beklemeden uygulanabilir.

## Emin için hazır kabul ve veri koruma

[Doldurulabilir form](windows-acceptance/Acceptance-Form.md) her akış için gerçek UI etiketlerini/tuşlarını, adımları, beklenen/gözlenen sonucu, test eden/tarih/kaynak/build'i ve kanıt alanlarını içerir. Formdaki normal açılış yalnız `-logFile` kullanır. C/G yeniden açılışları Save/Restore yapılmadan aynı oturumda çalışır; kalıcılık gözlenir. Sonunda ilk kişisel durum geri yüklenir.

[Preserve-UserData.ps1](windows-acceptance/Preserve-UserData.ps1), önceki doğrulamada kullanılan koruma yolundan uyarlanmıştır. Her insan kabulü öncesinde yeni tarih/GUID klasörüne Save alınır; önceki yedeğe güvenilmez. İki proje registry anahtarıyla rekor/ghost ve geçmiş dosyaları saklanır. Restore başlangıçta bulunmayan verileri de eski yokluk durumuna döndürür; isim/tür/değer ve dosya hash'lerini karşılaştırır. Unity'nin 8 bayt REG_DWORD değerleri korunur. Kişisel snapshot Git dışında kalır.

Hazırlık öz denetimi 09:17 UTC'de tamamlandı: yeni snapshot alındı; aynı snapshot üzerine Save reddedildi; geçici bir ek registry değeri Verify tarafından algılandı; `finally` içinde Restore çalıştı. Son kontrol **47 registry değeri + 2 dosya eşleşti**. Hiç oyun başlatılmadı ve insan kabulü uygulanmadı. Bu hazırlığın geri yükleme başarısı, ilerideki insan kabulünün son geri yüklemesi yerine geçmez.

[Confirm-Build.ps1](windows-acceptance/Confirm-Build.ps1) gerçek build'in 194 dosyasında geçti; ayrı geçici dosya fixture'ında yanlış SHA256'yı reddetti. Build ve kişisel dosyalar bu fixture denetiminde değiştirilmedi. Emin başlatırken aynı komut tekrar çalışır.

## Akış sonuçları

Ortak kaynak/build yukarıdaki kimliklerdir. Emin hiçbir akışı uygulamadığını açıkça bildirdi; uygulayan kişi, test tarihi, gözlem veya ekran/log kanıtı yoktur. Aşağıdaki sekiz akışın her biri test edilmeden kullanıcı tarafından kabul edilmiştir; test sonucu “çalıştırılmadı” olarak kalır.

| Akış | Gerçek test sonucu | Kullanıcı kabul kararı |
|---|---|---|
| A — menü ve ayarlar | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| B — WASD ve oklar | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| C — eşleme, Enter/C, kalıcılık | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| D — mouse orbit/zoom, zemin/duvar | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| E — başlat/saldır/pause/ayar/devam | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| F — iki ardışık fiziksel maç | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| G — seri/sonuç/geçmiş/yeni süreç/rövanş | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |
| H — Counter/Easy fırsat/dönüş/saldırı | Çalıştırılmadı | Emin tarafından uygulanmadan kabul edildi |

Normal Player UI/logu saldırı isteği, kabul edilmiş başlangıç ve isabet event sayaçlarını otomatik sunmaz. H formu erişilemeyen sayaçları “ölçülemedi” olarak bırakır; önceki otomatik koşuların sayıları bu insan koşusuna taşınmaz. Video animasyonu ve can kaybı iç event sayısı olarak yorumlanmaz.

Yeni doğrulanmış oyun hatası yok; üretim düzeltmesi yapılmadı. İnsan kabulü uygulanmadı; bu kabul için yeni snapshot alındığı doğrulanmadı ve Restore/Verify yapılmadı. Önceki hazırlık öz denetiminin 47 değer/2 dosya eşleşmesi geçerlidir; kullanıcı kararından sonra kişisel verilere dokunulmadı veya eski yedekten geri yükleme yapılmadı. İleride form uygulanırsa o anki veriler yeniden yedeklenmeli ve koşu sonunda geri yüklenip doğrulanmalıdır.

## PR ve aktarım koşulları

Uzak dallar fetch edildi: `test = 93b4ded847fba8b7893291bd5ac45fd03a67944c`, `main = 8452fa4a9f568966c98f5a2468ba57436ee05624`. Başlangıçta PR #43 açık, merge edilmemiş, `mergeable=true`, `mergeable_state=blocked` ve inceleme listesi boştu.

[Etkin test kuralı](https://github.com/Lysnder/RobotSavaslari/rules/26480038): en az bir uygun onay; code-owner incelemesi; yeni push'ta eski onayları düşürme; son push onayı; konuşmaları çözme. Son gönderimi yapan kişiden farklı uygun bir inceleyenin son kanıt commit'ini de kapsayan onayı gerekir. Bypass yok. Son gönderimden sonraki güncel HEAD/inceleme durumu PR açıklamasına ve teslim yanıtına kaydedilir; bu rapordaki başlangıç anlık görüntüsü son onay kanıtı değildir.

Emin bu sürüm için gerçek Windows kabulü yapılmadan ilerlemeyi kabul etti; bu karar eksik A–H kanıtlarının yerine başarılı test sonucu yazılmasına yol açmaz. Son belge commit'ini kapsayan uygun ekip incelemesi hâlâ gerekir. Gerçek OS-input ve insan koşusunun geri yükleme kanıtları yoktur. **Teknik doğrulamanın tümü tamamlandı denemez; main'e merge talimatı yoktur.** Hiçbir dala merge yapılmadı.

## Yeni kanıtlar

[Kanıt dizini](evidence/windows-acceptance-20261118/): `build-verification.json`, `native-path.json`, `desktop-blocker.json`, `data-protection-selfcheck.json`, `build-helper-selfcheck.json`, `acceptance-status.json`, `test-ruleset.json`, `user-acceptance-decision.json`. Güncel durum ve karar kayıtları uygulanmayan testleri geçmiş gibi işaretlemez.
