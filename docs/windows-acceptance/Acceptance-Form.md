# Windows kabul formu — Emin

Güncel durum: **A–H uygulanmadı; Emin uygulanmadan kabul etmeyi seçti.** [Karar kaydı](../evidence/windows-acceptance-20261118/user-acceptance-decision.json). Aşağıdaki adımlar ileride kullanılabilir bir formdur; boş alanlar test kanıtı değildir. Bu formdaki beklenen sonuçlar gözlem değildir. Önceki InputSystem/GUIView testleri gerçek OS giriş kabulü yerine sayılmaz.

## Sürüm ve oturum

- Test eden: Emin. Uygulayan kişi değişirse her akışa gerçek adı yaz.
- Tarih / saat / saat dilimi: **doldur**.
- Windows sürümü, ekran çözünürlüğü / ölçek, fare-klavye: **doldur**.
- Kaynak: `5de681341b31ffb81866adec3a2e2790b8abf364`.
- Oyun ağacı: `f0be42bc323521f557fbc6992205b1bda1c75b86`.
- Build: `sha256:bd78388612c8197b5439e91fcb0a1851c75a49f4115034af10bf97429e4a6960` (kısa etiket: `bd78388612c8`).
- EXE: `C:\Users\emins\Documents\Codex\robot-verify-93b4ded\RobotArena\Builds\Windows\RobotSavaslari.exe`.
- Üretim sahnesi: `Assets/Scenes/Gameplay/MainArena.unity`.
- 18 Kasım hazırlığında manifestteki **194/194 dosya eşleşti**. Kabul başlangıcında aşağıdaki komut tekrar kontrol eder. Başka build ile bu formun sonuçları kullanılamaz.
- Özel oturum/yedek klasörü (`$acceptanceSession`): **başlatma komutunun yazdığı yolu buraya kaydet**.
- Her A–H sonucunda yukarıdaki kaynak/build geçerlidir; farklıysa akışı durdur ve yeni kimliği kaydet.

## 1. Yedekle ve normal oyuncu olarak başlat

Oyunu ve bu projeyi açan Unity Editor'ü kapat. Windows'ta aynı kullanıcı hesabında, kilidi açık masaüstünde çalış. Bu kabul sırasında başka checkout'tan RobotArena çalıştırma; aynı kişisel veriyi kullanabilir.

Windows PowerShell'i `powershell.exe -NoProfile -ExecutionPolicy Bypass` ile aç. Aşağıdaki bloğu o pencerede çalıştır; pencereyi kabul bitene kadar açık tut. Hata varsa oyunu açma.

```powershell
$ErrorActionPreference = 'Stop'
$acceptanceRepo = 'C:\Users\emins\Documents\Codex\robot-verify-93b4ded'
$acceptanceBuild = Join-Path $acceptanceRepo 'RobotArena\Builds\Windows'
$acceptanceHelper = Join-Path $acceptanceRepo 'docs\windows-acceptance\Preserve-UserData.ps1'
$acceptanceSession = Join-Path $env:USERPROFILE ('Documents\Codex\robot-windows-acceptance-data\manual-' + (Get-Date -Format yyyyMMdd-HHmmss) + '-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $acceptanceRepo 'docs\windows-acceptance\Confirm-Build.ps1') -Repository $acceptanceRepo
& $acceptanceHelper -Action Save -SnapshotDirectory $acceptanceSession
Write-Host ('Kabul oturumu: ' + $acceptanceSession)
$acceptanceProcess = Start-Process -FilePath (Join-Path $acceptanceBuild 'RobotSavaslari.exe') -WorkingDirectory $acceptanceBuild -ArgumentList ('-logFile "' + (Join-Path $acceptanceSession 'player-01.log') + '"') -PassThru
```

`Save`, o anki iki proje PlayerPrefs registry anahtarını ve `driving-personal-best.json` / `match-history.json` dosyalarını, yoklukları dahil saklar. Tuş eşlemeleri/ayarlar registry'de, ghost/rekorlar rekor dosyasındadır. Yedek Git dışında kalır; eski hazırlık yedeğini kullanmaz. Mevcut yedeğin üstüne yazmaz. Sadece `-logFile` kullanılır; smoke/test/sanal giriş parametresi yoktur.

Ekran kaydını Windows'un mevcut kayıt aracıyla alabilir veya kritik anların görüntüsünü saklayabilirsin. A–H harfi ve zaman damgası kullan (`C-remap.mp4` gibi). Kanıt dosya yolunu ve videodaki zamanı her akışa yaz. Kayıt bulunmuyorsa “yok” yaz; dosya varmış gibi işaretleme.

Kabul öncesi yedek sonucu: **bekliyor**. `user-data-Save.json` iki eşleşme alanı `true` olmalı. Özel `state.clixml`, kişisel rekor/geçmiş içeriklerini PR'a ekleme.

## 2. Yeniden açma — C ve G

Oyunu normal “Çıkış” / pencere kapatma yoluyla kapat; işlem tamamen sonlansın. **Bu noktada Restore yapma veya yeni Save alma**: kalıcılığı aynı kabul oturumundaki veriler üzerinden kontrol edeceksin. Aynı PowerShell penceresinde aşağıdaki bloğu çalıştır. İkinci yeniden açılışta log adını `player-03.log` yap; önceki logu ezme.

```powershell
if (Get-Process RobotSavaslari -ErrorAction SilentlyContinue) { throw 'Oyun tamamen kapanmali.' }
& (Join-Path $acceptanceRepo 'docs\windows-acceptance\Confirm-Build.ps1') -Repository $acceptanceRepo
$acceptanceProcess = Start-Process -FilePath (Join-Path $acceptanceBuild 'RobotSavaslari.exe') -WorkingDirectory $acceptanceBuild -ArgumentList ('-logFile "' + (Join-Path $acceptanceSession 'player-02.log') + '"') -PassThru
```

## A. Menü ve ayarlar

1. Ana menüde gerçek fareyle “Ayarlar [Esc]” düğmesine tıkla.
2. Ses sürgüsünü, tam ekran seçimini ve etkin kalite okunu değiştir. Eski/yeni değerleri yaz. Kamera hassasiyetini de değiştirip etkisini D'de kontrol et.
3. “Kaydet ve dön [Esc]” ile dön; tekrar açıp değerlerin durduğunu kontrol et.
4. Menüde “Maç geçmişi”ni sağdaki maç kurulumu panelinden açıp “Geri” ile dön. “Maça başla [Enter]” tıklamasını E'de uygula. “Çıkış”ı C/G'deki süreç kapatmada kullan.

Beklenen: Her düğme görünen işlemi yapar, değerler uygulanır/kaydedilir; ayarlara girip çıkmak maçı kendiliğinden başlatmaz. Menü, pencere/tam ekran geçişinde tıklanabilir kalır.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Gözlenen (düğme ve eski→yeni değerler): **doldur**.
- Kanıt (dosya / zaman, `player-01.log`): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi** — geçti / kaldı / teknik engel seçeneklerinden uygulama sonrası birini yaz.

## B. WASD ve ok tuşları

1. Ana menüde “Sürüş tuşları” → “Varsayılanlara dön” → “Geri”; böylece mevcut kişisel eşlemenin etkisini kaldır. Kişisel ilk durum sonda geri yüklenecek.
2. “Tek maç” ve “60 sn” seç; Enter veya Maça başla ile geri sayımı başlat, BAŞLA'yı bekle.
3. Boş alanda önce W, S, A, D'yi **tek tek** bas/bırak. İleri, geri, sola ve sağa dönmeyi kaydet.
4. Ayrı olarak ↑, ↓, ←, → tuşlarını tek tek dene. W+A / ↑+← gibi birer birleşik dönüş dene.
5. Hareket tuşunu bırak ve birkaç saniye davranışı izle. Shift boost ve sol Ctrl el frenini ayrı dene.

Beklenen: WASD ve oklar aynı eylemlere gider. Bırakılan tuş yeni sürüş komutu üretmez; motorun normal yavaşlaması/fizik kayması olabilir (anında tam duruş beklenmez). Dönüş takılı kalmaz. Geri sayımda maç sürüşü/saldırısı başlamaz.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Gözlenen (W/S/A/D ve ↑/↓/←/→ ayrı, bırakma): **doldur**.
- Kanıt (dosya / zaman / log): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## C. Tuş eşleme, Enter/C ve kalıcılık

1. Maç sonucundan “Ana menü”ye dön, “Surus tuslari”nı aç.
2. İleri satırındaki mevcut tuş düğmesine tıkla; yakalama açıkken Enter'a bas. Reddedilmesini ve **maçın başlamamasını** kontrol et. NumPad Enter varsa onu da dene.
3. Yakalamada C'ye bas. Eşlemenin değişmediğini, ayrılmış tuş mesajını ve kameranın aynı kaldığını kontrol et. Esc ile yakalamayı iptal et; ekranı “Geri” ile kapat.
4. Eşlemeyi tekrar açıp ileri hareketi boş bir tuşa (ör. I; önceden atanmışsa boş başka tuş seç) ata. Tuşu ve eylemi kaydet.
5. Ekranı kapat, maç başlat; yeni tuşun ileri gittiğini, eski W'nin artık ileri eylemi vermediğini ve ↑ alternatifinin çalıştığını dene.
6. Oyunu normal kapatıp 2. bölümdeki komutla yeniden aç. Eşleme arayüzünde yeni tuşun kaldığını ve yeni maçta çalıştığını doğrula.
7. Bittiğinde sonraki akışlar için “Varsayılanlara dön” → “Geri”.

Beklenen: Enter/C ayrılmıştır; yakalamada oyun/menü/kamera komutu oluşmaz. Geçerli eşleme hemen uygulanır, yeni süreçte kalır; ok alternatifi korunur. Yakalamadan çıkınca normal girişler geri gelir.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Yeni eşleme / yeniden açılış sonucu: **doldur**.
- Gözlenen (Enter, NumPad Enter, C, kamera, W/yeni tuş/↑): **doldur**.
- Kanıt (dosya / zaman / `player-01.log`, `player-02.log`): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## D. Gerçek fareyle kamera

1. Normal maçta robotu sürerken **sağ fare tuşunu tutup** yatay/dikey hareket ettir; farklı yönlerden takip et.
2. Tekerleği iki yönde çevirerek yakınlaştır/uzaklaştır; C ile takip/düello modları arasında geç ve aynı girdileri dene.
3. Robotu arena sınırına ve köşesine sür. Kamerayı en düşük erişilebilir açıya getir, duvar yakınında orbit/zoom yap.
4. Ayarların Kamera sekmesindeki yatay/dikey hassasiyet ve dikey ekseni ters çevirme etkisini kontrol et; ayarlar açıkken fare/tekerlek/C'nin arka kamerayı değiştirmediğini kontrol et.

Beklenen: Hareket/yakınlaşma doğru yönde ve sınırlarda çalışır, kamera zemine/duvara girip görüntüyü bozmaz. Düello modunda iki robotu kadraja alma sınırları nedeniyle orbit/zoom takip moduyla birebir aynı olmayabilir. Ayarlarda girdi kameraya sızmaz.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Gözlenen (iki mod, iki scroll yönü, alçak açı, duvar/köşe): **doldur**.
- Kanıt (dosya / zaman / log): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## E. Başlat, saldır, duraklat, ayarlar, devam

1. Menüde “Maça başla [Enter]”a gerçek fareyle bas, geri sayımı izle.
2. WASD/oklarla rakibe git, Space ile saldır. Silah hareketi ve uygun temas varsa hasarı gözle.
3. Sürüşten sonra tüm tuşları bırak, Esc ile duraklat/ayarları aç; bir süre bekle. Ayrı denemede “Duraklat (Esc)” düğmesine tıkla.
4. Ayarlarda fareyle bir değeri değiştir, Enter/Space/W bas-bırak; arka maçın ilerlemediğini izle.
5. “Kaydet ve dön [Esc]” ile dön. Tuşları bırakıp yeniden sürüş ve saldırı yap; sonra Esc ile aç/kapatmayı da dene.

Beklenen: Geri sayım bir kez başlar. Oyun duraklamada ilerlemez, ayar girdileri oyuna gitmez. Dönüşte odak/input geri gelir; eski basılı hareket/saldırı takılı kalmaz.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Gözlenen (başlatma, silah/hasar, pause süresindeki can/süre, devam): **doldur**.
- Kanıt (dosya / zaman / log): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## F. İki ardışık fiziksel maç

1. Menüden Tek maç / 60 sn seç; gerçek klavyeyle sürerek/saldırarak sonuç oluşana kadar oyna. Süre veya fiziksel yenilgiyle bitiş nedenini kaydet; debug yenilgi/konsol/smoke kullanma.
2. Sonuçtaki “Yeni maç (R)” düğmesiyle ikinci maçı başlat; geri sayımı, spawn'ları ve başlangıç canlarını kaydet.
3. İkinci maçı da gerçek klavyeyle sürerek/saldırarak sonuca kadar oyna. Birinci maçtan hasarlı/parçalanmış durum, hız, hedef veya bitmiş sonuç taşınıp taşınmadığını gözle.

Beklenen: İki maçta da fizik ve oyuncu kontrolü çalışır, her biri tek sonuç verir. İkinci maçın canı/konumu/hasarlı parçaları ve sonuç sayaçları doğru sıfırlanır; silah normal bekleme süresinden sonra çalışır.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Maç 1 (sonuç / neden / başlangıç-son can / süre): **doldur**.
- Maç 2 (sonuç / neden / başlangıç-son can / süre / reset gözlemi): **doldur**.
- Kanıt (iki maç için dosya / zaman / log): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## G. Seri, sonuç, geçmiş, yeni süreç ve rövanş

1. Ana menüde “İlk 2 galibiyet” / “60 sn” seç. Raundları normal sürüş/saldırıyla oyna; biri iki galibiyete ulaşana kadar “Sonraki raund (R)” ile ilerle.
2. Her raundun sonucu/skorunu yaz. Süre dolarsa kalan **can oranı** yüksek olan kazanmalı. Beraberlik oluşursa galibiyet artmamalı, “Raundu tekrarla (R)” ile aynı raund tekrar edilmelidir. Beraberlik gözlenmediyse bunu açık yaz; gözlenmiş sayma.
3. “Seri bitti” sonucunu, toplam istatistikleri ve “Ana menü” → “Maç geçmişi” satırını kontrol et. Tek bir tamamlanan karşılaşma kaydı, doğru skor/süre/rakip beklenir.
4. Oyunu normal kapat; 2. bölüm komutunu **`player-03.log`** ile çalıştır (Save/Restore yapma). Geçmişi yeniden aç; aynı seri kaydı okunuyor mu kontrol et.
5. O kaydın “Rövanş”ına tıkla. Kural/rakip bilgileri seçilen kayda uymalı; yeni seri sıfır galibiyet, temiz can/spawn ve yeni geri sayımla başlamalı. Sürüş/saldırı yaparak girişin çalıştığını doğrula.

Beklenen: Seri iki galibiyette tamamlanır, sonuç/geçmiş tutarlıdır ve yeni süreçte okunur. Rövanş eski skor/hasarı taşımaz. Yalnız görsel sonuç değil, yeniden açılan geçmiş kaydını da gözle.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Raund sonuçları / seri skoru / beraberlik gözlemi: **doldur**.
- Geçmiş ilk süreç / yeni süreç / rövanş gözlemi: **doldur**.
- Kanıt (dosya / zaman / `player-02.log`, `player-03.log`): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## H. Counter / Easy

1. Ana menüye dön; “RAKİP DAVRANIŞI” panelinde **Counter** ve **Easy** seçimini görüntüle/kaydet. Tek maç / 60 sn ile başlat.
2. Silahı sağlam rakibe açık alanda yaklaş; kısa süre durarak ve yanına geçerek hedefe dönme fırsatları oluştur. Salt duvara sıkışmış veya silahı kopmuş anları saldırı fırsatı sayma.
3. Birkaç yakınlaşmayı videoda işaretle: rakibin sana dönmesi, savunma/yan manevradan takibe gelmesi, silahın başlaması ve ayrı olarak isabet/hasar. Oyuncu saldırısını kısa süre bırakmak rakibin davranışını ayırmayı kolaylaştırır.
4. Normal maçın kalanını oyna. Sürekli yakında/sağlam silahla fırsat varken hiç dönmeme veya hiç saldırmama görürsen süreleri ve yeniden üretim yolunu yaz; tek kaçırmayı otomatik hata sayma (Easy tepki/nişan hataları korunur).

Beklenen: Yakın ve geçerli fırsatta Counter hedefe dönüp mevcut melee akışında saldırabilir; Waiting/flank nedeniyle kalıcı sıfır saldırı oluşmaz. Her isteğin başlangıca veya her başlangıcın isabete dönüşmesi beklenmez.

Normal build'in mevcut UI'si istek/başlangıç/isabet sayaçlarını sunmaz; `AttackRequest`, `MeleeAttack.Started`, `TargetHit` eventleri normal player loguna otomatik dökülmez. Erişilemiyorsa aşağıya **ölçülemedi** yaz. Görsel animasyon veya can kaybından iç event sayısı türetme; temas hasarı melee isabetiyle aynı değildir. Önceki otomatik event ölçümleri bu insan koşusunun sayacı olarak kullanılamaz.

- Test eden / tarih: **Emin / doldur**.
- Kaynak / build: yukarıdaki sabit kimlikler.
- Gözlenen fırsatlar (zaman / silah durumu / dönme / saldırı / isabet): **doldur**.
- Bu koşuya ait istek / kabul edilmiş başlangıç / isabet (kaynakla): **ölçülemedi veya doğrulanmış ayrı değerler**.
- Kanıt (Counter/Easy seçimi + fırsatlar, dosya / zaman / log): **doldur**.
- Gerçek test sonucu: **çalıştırılmadı**; kullanıcı kararı: **uygulanmadan kabul edildi**.

## 3. Sonunda geri yükle ve doğrula

Başarısız/yarım kalan kabulde de uygula. Önce yeni test loglarını ve gerekli görüntüleri sakla. Gerekirse bu kabulde oluşan `match-history.json` dosyasını **özel oturum klasörüne** kopyala; PR'a kişisel içerik koyma. Sonra oyunu normal kapat; süreç tamamen bitsin. Aynı PowerShell penceresinde:

```powershell
if (Get-Process RobotSavaslari -ErrorAction SilentlyContinue) { throw 'Geri yuklemeden once oyunu tamamen kapat.' }
& $acceptanceHelper -Action Restore -SnapshotDirectory $acceptanceSession
Get-Content (Join-Path $acceptanceSession 'user-data-Restore.json')
```

PowerShell kapanmışsa `$acceptanceHelper`ı yukarıdaki sabit yola, `$acceptanceSession`ı bu formda kaydettiğin **tam manual-... klasörüne** yeniden ata; eski `prep-...` yedeğini seçme. Restore hem geri yükler hem ad/tür/değer ve dosya SHA256 karşılaştırır. İki `*_matches_snapshot` alanı da `true` olmadan veri geri yüklemeyi geçti işaretleme. Hata varsa özel yedeği koru ve tam hata metnini ilet.

- Kabul öncesi yedek zamanı ve oturum: **bekliyor**.
- Kabul sonu geri yükleme zamanı: **bekliyor**.
- Registry eşleşmesi / kontrol edilen değer sayısı: **bekliyor**.
- Kayıt dosyası eşleşmesi / dosya sayısı: **bekliyor**.
- Kanıt: `user-data-Save.json`, `user-data-Restore.json` (**bekliyor**).

## Sonucu iletme

A–H için sonucu ve gözlemi doldur; hata varsa tam yeniden üretim adımlarını, video zamanını ve ilgili player logunu ekle. Test anında oluşan Error/Exception satırlarını ayrıca işaretle. Henüz uygulanmayan alt adımları bekliyor bırak. Doldurulmuş form ve kişisel içeriklerden arındırılmış kanıtlar PR #43'ün devam kanıtına işlenecek.

Form ileride gerçekten uygulanırsa sekiz akışın sonuçları ve kabul sonrası veri geri yükleme doğrulaması kaydedilmelidir. Emin'in mevcut kabul kararı bu uygulamanın yapıldığı anlamına gelmez. Son kanıt gönderiminden sonra `test` kuralına uygun en az bir bağımsız onay gerekir; bu formun hazırlanması main'e aktarım onayı değildir.
