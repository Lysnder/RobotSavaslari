# RobotSavaşları doğrulama ve düzeltme raporu — 18 Kasım 2026

Başlangıç `93b4ded847fba8b7893291bd5ac45fd03a67944c`; test edilen son kaynak `5de681341b31ffb81866adec3a2e2790b8abf364`. Unity `6000.4.4f1 (360f97ecca93)`, Windows Editor ve Windows x64 Player; komutlar WSL üzerinden native Windows yollarıyla çalıştırıldı. Çalışma dalı `fix/validation-reliability`, PR hedefi `test`. `main` üzerinde birleştirme yapılmadı.

Rapor ve kanıtlar kaynak testlerinden sonra ayrı belge commit’iyle eklenir. Oyun, sahne, Editor ve test kaynakları aynı kalır: `git diff 5de681341b31ffb81866adec3a2e2790b8abf364 HEAD -- RobotArena` boş olmalıdır.

**Son doğrulama**

| Kapsam | Geçti | Kaldı | Atlandı / inconclusive |
|---|---:|---:|---:|
| editmode | 197/197 | 0 | 0 / 0 |
| playmode | 213/213 | 0 | 0 / 0 |
| Windows bot maçları |48/48|0|0|
| Eski 14 testin tekil süreçleri |14/14|0|0|
| Windows derlemesi, iki gerçek fiziksel maç, seri/geçmiş/rövanş |Geçti|0|0|
| Editor bot senaryoları, taktik/dövüş/bölgesel hasar |Geçti|0|0|
| Gerçek OS fare/klavye kabulü |0/8 akış|—|8 akış teknik engel nedeniyle çalıştırılamadı|

Başlangıçtaki 402 testin tamamı isimleriyle karşılaştırıldı; eksilen test yok. Sekiz ek regresyonla 197 EditMode + 213 PlayMode = 410 benzersiz test var. Üç eski koşuya bağlı testin her biri 10 kez çalışır; bu, tam koşuda 27 ek yineleme ve toplam 437 test gövdesi çalıştırması demektir. 48 bot maçı ve Windows smoke kontrolleri bu NUnit sayısına eklenmez.

**Counter davranışının nedeni ve çözümü**

Başlangıç aynı Open arenasında, varsayılan spawn’larda, seed 740 ile Aggressive/Counter × Easy/Hard için ikişer gerçek maçtı. Oyuncu gerçek PlayerKeyboardDriver’a sanal InputSystem tuşları gönderir; Space tutar, rakibe döner ve global oyun zamanındaki 5 saniyelik döngünün 0.8 saniyelik bölümünde geri sürer. Her maç 75 saniye sınırında tek sonuç üretmelidir. Bunun OS klavye kabulü olduğu iddia edilmez.

Eski Counter/Easy ikinci maçında silah sağlamken istek/başlangıç/isabet/hasar 0/0/0/0 idi. Yan manevradan sonraki Waiting hem hareketi hem dönüşü durduruyordu. Yalnız beklerken nişana dönmek ilk sekiz maçı geçirdi, fakat 741/Easy koşusunda yeniden konumlanma sırasında 15 hazır fizik örneği hiçbir isteğe dönüşmedi; 123/Hard da iki kez sıfır saldırı verdi. Hazır silahla geçerli fırsat savunma durumunu bitirmelidir. Sonraki tekrarda 123/Easy henüz açı oluşmadan yakın rakibe rağmen flank sürdürdü; bunun için ayrı başarısız regresyon eklendi.

Son çözümde silah hazır ve rakip mevcut `followResumeDistance` yakın takip bandındaysa Counter önce normal pursuit/turn yoluna döner; yakın rakibe nişan almadan flank bitirmeye çalışmaz. Cooldown veya aktif saldırı savunmayı gereksiz yere kesmez. Normal 22° saldırı açısı, 1.85 m Arena giriş menzili, motor ve hasar değerleri korunur. Easy’nin 0.40 saniye tepkisi, nişan hatası ve kaçırılan kararları; Hard’ın 0.16 saniye tepkisi korunur. Duvar kurtarma önceliği ve saldırı burst/cooldown akışı aynı ortak motor/melee bileşenlerindedir.

Sayaçlar ayrı eventlerden gelir: `AttackRequest` niyet, `MeleeAttack.Started` kabul edilmiş başlangıç, `TargetHit` isabet, hedefin `Damaged` eventi gerçek verilen hasardır. Cooldown reddi başlangıç sayılmaz; yeni dört regresyon bunu da doğrular. TSV’deki `eligible`, adım sonu örneğidir: aynı adımda başlayan saldırı cooldown’u kapattığından 0 örnekle birlikte gerçek başlangıç görülebilir. Fırsat kaybı, sabit geometrili regresyonlar ve eski manevra trace’iyle ayrıca kanıtlandı.

| Final süreç | Seed | Maç | Sonuç |
|---|---|---:|---|
| Orijinal matris |740|8|8 geçti|
| Ayrı süreçte orijinal tekrar |740|8|8 geçti|
| Ek matris |741,123|16|16 geçti|
| Ek matrisin ayrı süreç tekrarı |741,123|16|16 geçti|

Seed tek başına render/fizik zamanlamasını sabitlemez; oyun zamanı ve gerçek durum geçişleri trace’te kaydedildi. Toplam 160 geçerli maç kaydında final öncesi 5 başarısız deneme de korunur. Bütün geçerli ara bot denemeleri, başarısız olanlar dahil [bot-trials.csv](evidence/validation-20261118/bot-trials.csv) içindedir. Kabul yalnız final kaynak fazlarına dayanır. Başlangıç tespiti, ara başarısız seed’ler ve son sonuçlar birleştirilip tek bir geçti sayısına çevrilmedi.

**Başlangıçtaki 14 hata**

Aşağıdaki her satır hem son tam PlayMode grubunda hem tekil yeni Windows Editor sürecinde geçti. Önceki tam assertion/stack, kaynak yolu/satırı, tekil başlangıç sonucu ve son sonuç [failure-ledger.json](evidence/validation-20261118/failure-ledger.json) içindedir. Başlangıçta 11 test tek başına da başarısızdı; rotor/debris/inspection tekil koşuda geçti, eski sunum koşuları tekrarlanarak hataları yeniden üretildi.

| # / kontrol | Önceki hata | Düzeltme | Son |
|---|---|---|---|
|1. Yeniden başlat düğmesi|Tıklama alınmıyor; maç bitmiş kalıyordu.|Native GUIView, aynı gesture state ve DPI dönüşümü.|Geçti|
|2. W / Space / spinner|İlk hasar, sürüş ve LateUpdate gözlenmeden döngüyü bitiriyordu.|Sürüşü 25 fizik adımında; silah başlangıcı/isabeti ve animasyonu ayrı gözle.|Geçti|
|3. Robot arena sınırı|Eski 5.89/4.89 m eşikleri, doğru 6.748 m konumunu reddediyordu.|8×8 hücre, 2 m/hücre ve gerçek ayak iziyle merkez sınırı.|Geçti|
|4. Sürüş rotası düğmeleri|Sonraki rota tıklaması alınmıyordu.|Native GUIView ve fiziksel piksel / ekran noktası dönüşümü.|Geçti|
|5. Duvar çarpışma raporu|Hareket sırasında zemine iniş duvar çarpışması sayılıyordu.|Yanal temas normali zorunlu; yeni iniş regresyonu.|Geçti|
|6. Menü / ayarlar düğmeleri|Ayarlar ve Oyna tıklamaları alınmıyordu.|Gerçek OnGUI → GUI.Button olay yolu.|Geçti|
|7. Ayarlar kontrolleri|Fullscreen / kalite / varsayılan / kaydet kontrolleri değişmiyordu.|Native input state, Layout ve mouse down/up aynı pencerede.|Geçti|
|8. Kamera düğmeleri|Follow → Duel geçmiyordu.|DPI ve clip dönüşümü düzeltilen tıklama yardımcısı.|Geçti|
|9. İnceleme paneli düğmeleri|Arayüz gizleme/çıkış tıklaması alınmıyordu.|Aynı native GUI olay düzeltmesi.|Geçti|
|10. Sonuçtan replay / inceleme|Düğme tıklamaları alınmıyordu.|Aynı native GUI olay düzeltmesi.|Geçti|
|11. Modüler demo rotoru|İki çok hızlı frame sonrasında Quaternion.Angle sıfır kalabiliyordu.|Gerçek Update ile ≥1° hareketi en fazla 1 saniye gözle; 10 tekrar.|Geçti|
|12. Kamera arena sınırı|Eski 5.91/4.91 m iç sınırlar kullanılıyordu.|Güncel 7.81 m iç sınır; gerçek collider overlap/linecast kontrolleri korunur.|Geçti|
|13. Duel kamera / debris|Kamera henüz oturmadan referans alınıyordu; 7.04979→6.94059.|Pivot/mesafe 0.005 m içinde 0.2 saniye sabit, en çok 4 saniye; eski 0.05 m tolerans korunur; 10 tekrar.|Geçti|
|14. İnceleme yan etkisi|Gövde sabitken Interpolate geçmişi görüntüyü 0.881544 m kaydırdı.|Yalnız donmuş test gövdelerinde interpolation kapatılır; 1e-4 m beklenti korunur; 10 tekrar.|Geçti|

**Test izolasyonu ve Editor başlangıcı**

`GameplayTestScene.Unload()` bütün sahneleri boşaltıp çalıştırıcının yaşam döngüsünü bozuyordu. Gameplay ve Presentation yardımcıları yalnız sahip oldukları sahneyi boşaltır; bootstrap ve ilgisiz sahneler korunur, zaman ölçeği normale döner. Yeni regresyon test nesnesinin silindiğini, ilgisiz nesnenin ve sahnenin iki temizleme boyunca yaşadığını denetler. InputTestFixture giriş durumunu, mevcut snapshot’lar ayarları geri yükler.

Tam EditMode koşusunda ArenaAuthoringTests’in üretim MainArena dosyasını kaydedip yeniden ürettiği bulundu. Test artık aynı MainArena’nın geçici asset kopyasında save/reopen/idempotence kontrolü yapar; sonunda kopyayı siler ve üretim sahnesinin baytlarının değişmediğini doğrular. Kaynak sahne/GUID/referansları değiştirilmedi. Tam grup ve Windows adımlarından önce/sonra, ayrıca 14 tekil süreçten sonra Git ile bütün RobotArena kaynakları kontrol edildi.

Bir ek eski beklenti güncellendi: Waiting’de `Command == Vector2.zero` yerine ileri hareketin 0 ve dönüşün normal limit içinde olması denetlenir. Bounded retreat/flank/opening ve ortak melee kuralları korunur; yeni karşı saldırı testleri eski kodda gerçekten başarısız oldu.

Editor ek denetimleri ilk kez Search.index yokken Play’e hemen giriyordu. Unity 6000.4 `SetupDefaultIndexFirstUse` Play’e geçerken indeks oluşturmayı atlar; `EnumerateAll` boş listenin ilk elemanına erişir. Begin artık Play’i bir sonraki `delayCall` içine koyar; Editor başlangıç callback’leri önce çalışır. Hem senaryo hem dövüş denetimi Search.index yeniden yok edilerek ayrı temiz süreçte geçti. Log hatası filtrelenmedi. [Unity SearchDatabase kaynağı](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.4/Modules/QuickSearch/Editor/Indexing/SearchDatabase.cs), [SearchInit kaynağı](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.4/Modules/QuickSearch/Editor/SearchInit.cs).

**Windows kabulü ve veri koruma**

18 Kasım devamı: güncel oturumda eski WSL yolu yerine doğru `/home/lysander/...` yolu iletiliyor, ancak masaüstü aracı yine JavaScript başlamadan aynı URI doğrulamasında engelleniyor. Build yeniden 194/194 hash ile eşleşti. Emin elle kabulü uygulayabileceğini bildirdi; henüz sonuç bildirilmediği için A–H bekliyor. [Devam raporu](windows-acceptance-20261118.md), [uygulanabilir kabul formu ve komutlar](windows-acceptance/Acceptance-Form.md). Yeni veri koruma hazırlık kontrolünde 47 registry değeri ve iki dosya geri yüklenip eşleşti; bu hazırlık kabul sonrası geri yükleme yerine geçmez.

Final build dosyalarının SHA256 manifest’i kaydedildi. 48 bot maçına ek olarak `-arena-smoke` iki ardışık fiziksel maçı gerçek oyun sürücüsü/InputSystem yolu, gerçek melee/temas hasarı, bölgesel parçalar ve temiz restart ile geçti. `-match-rules-smoke write/read` ayrı süreçlerde seri 2-0/2-1, süre/beraberlik, üç karşılaşmanın kalıcılığı ve temiz rövanşı doğrular; bu kontrollü kural denetimi fiziksel maç sayılmaz.

Doğrudan OS girdisi tamamlanamadı. `mcp__node_repl__js` en basit JS bile başlamadan `sandboxCwd is not a local file URI: file:///mnt/c/users/emins/documents/codex/2026-11-11/files-pasted-by-the-user-robotsava` hatası veriyor. Native exe/proje yolları, boşluk içeren Unity kurulum yolu ve Türkçe dosya adı Windows LiteralPath ile doğrulandı. Windows ve kayıtlı managed worktree yolları, yeni kernel ve resmi @oai/sky başlangıcı denendi; aktif isteğin metadata’sı eski WSL URI’sinde kaldı. Gerçek mouse/keyboard menü, WASD/oklar, eşleme, kamera, pause/dönüş, iki maç, seri/geçmiş/rövanş ve Counter/Easy gözlemi bu nedenle tamamlanmış sayılmıyor. [Teknik kayıt](evidence/validation-20261118/windows-input-blocker.json).

Kişisel veri kontrolü: registry=True, kayıt dosyaları=True; 47 registry değeri ve 2 dosya kontrol edildi. Çalışma başında alınan yeni 18 Kasım yedeği geri yüklendi; değer adları/türleri/içerikleri ve dosya SHA256’ları eşleşti. 8 bayt REG_DWORD float değerleri ham baytlarıyla korundu. Kişisel yedekler ve geçmiş içerikleri Git’e konulmadı.

**Yeniden üretim ve kanıtlar**

Unity.exe yolu ve argümanlar [commands.json](evidence/validation-20261118/commands.json), sonuçlar [validation.json](evidence/validation-20261118/validation.json), bütün benzersiz testler [CSV](evidence/validation-20261118/final-test-cases.csv), kapsam karşılaştırması [coverage-comparison.json](evidence/validation-20261118/coverage-comparison.json) içindedir. XML’ler, final tekil sonuçlar, bot trace/log’ları ve kaynak bütünlüğü kayıtları [evidence.zip](evidence/validation-20261118/evidence.zip) içinde; arşiv dosyalarının SHA256’ları manifest’te bulunur.

Kaydedilen Windows yollarını kendi checkout/evidence dizinine uyarlayıp PowerShell ile çalıştırın:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Invoke-ValidationStep.ps1 -CommandsFile .\commands.json -Name final-editmode
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Invoke-ValidationStep.ps1 -CommandsFile .\commands.json -Name final-playmode
```

XML kayıtları, donmuş test gövdelerine hız yazılan senaryolardaki Unity uyarılarını da içerir; bu çıktılar gizlenmedi. Normal fiziksel maçlar ayrı Windows süreçlerinde doğrulandı.

Native IMGUI testleri için PlayMode’da `-nographics` kullanılmaz. Yardımcı gerçek process handle’ını bekler; sıfır/eksik/atlanan testleri kabul etmez. Unity adımları aynı projede eşzamanlı çalıştırılmaz. Kişisel ayar/kayıt yedeği veya ayrı test profili kullanılır; write/read smoke yalnız belirtilen yeni test geçmişi dosyasına yönlendirilir.

İlk başlatıcıda native PowerShell alt süreçleri beklenmediği için eşzamanlı bir deneme geçersiz sayıldı. Ayrıca üretim sahnesini yeniden kaydeden ve ara bot düzeltmelerini kullanan koşular son kabulden ayrıldı. Yerel ham arşivler `verification-20261118/invalid-concurrent-attempt`, `pre-isolation-f9713e0`, `superseded-0d8dfb8`, `pre-editor-ordering-a0a9cc4`, `superseded-c32d1ea` dizinlerindedir. Final sonuçlar yalnız yukarıdaki kaynak SHA’sından yeniden üretildi.

Otomatik kontroller ve test dalını hedefleyen düzeltme teslimi hazırdır. Gerçek Windows OS fare/klavye kabulü teknik engel nedeniyle eksiktir; main’e aktarım için bu kabulün tamamlanması önerilir.
