# PvE atölyesi · MainArena doğrulaması

## Sürüş ve silah ölçümü

Başlangıç ölçümü birleşik `test` sürümü `a1754a26547b4b0fa6e933500c8032ee8bf12334` üzerinde alındı. Atölye üretim sürümü `1096cc76c4a9e287d775ed43045ba0bd130b41fd`. Unity 6000.4.4f1, Windows x64, 50 Hz fizik adımı kullanıldı.

Her satır Ready menüsünden başlayan standart Open düellosuna ait. Hızlanma için ilk 0,12 saniyedeki gerçek Rigidbody hız farkı, hız için 0,64 saniyelik tam gaz sonunda ileri hız, fren için gazı bıraktıktan sonra 0,05 m/s eşiğine kadar gidilen mesafe, dönüş için duruştan 0,40 saniye komut verince gövdenin yaw açısı ölçüldü. Hedef bu sürüş bölümü boyunca park edildi. Ardından bot tekrar etkinleştirildi. Aynı 1,80 m merkez uzaklığında gerçek silah fonksiyonu hedefin canını sıfırlayana kadar çalıştırıldı. İlk hasar, hedefin o andaki ileri hızı ve saldırı saatinden okunan bekleme ölçüldü. İkinci eşzamanlı istek bekleme nedeniyle reddedildi.

| Lastik / zırh | Silah | Kütle kg | İvme m/s² | Hız m/s | Fren m | Dönüş ° | İlk hasar HP | İtiş m/s | Bekleme s | İsabet / sonuç |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Hazır | Döner | 12,00 | 16,55 | 5,93 | 0,561 | 47,00 | 25 | 0,0 | 1,00 | 4 / galibiyet |
| Hazır | Kama | 12,00 | 16,55 | 5,93 | 0,561 | 47,00 | 18 | 2,4 | 1,25 | 6 / galibiyet |
| Yumuşak / hafif | Döner | 11,04 | 18,65 | 5,58 | 0,439 | 53,05 | 25 | 0,0 | 1,00 | 4 / galibiyet |
| Yumuşak / hafif | Kama | 11,04 | 18,65 | 5,58 | 0,439 | 53,05 | 18 | 2,4 | 1,25 | 6 / galibiyet |
| Yumuşak / ağır | Döner | 13,44 | 14,80 | 5,58 | 0,439 | 46,83 | 25 | 0,0 | 1,00 | 4 / galibiyet |
| Yumuşak / ağır | Kama | 13,44 | 14,80 | 5,58 | 0,439 | 46,83 | 18 | 2,4 | 1,25 | 6 / galibiyet |
| Sert / hafif | Döner | 11,04 | 18,65 | 6,29 | 0,700 | 46,83 | 25 | 0,0 | 1,00 | 4 / galibiyet |
| Sert / hafif | Kama | 11,04 | 18,65 | 6,29 | 0,700 | 46,83 | 18 | 2,4 | 1,25 | 6 / galibiyet |
| Sert / ağır | Döner | 13,44 | 14,80 | 6,29 | 0,700 | 41,31 | 25 | 0,0 | 1,00 | 4 / galibiyet |
| Sert / ağır | Kama | 13,44 | 14,80 | 6,29 | 0,700 | 41,31 | 18 | 2,4 | 1,25 | 6 / galibiyet |

Mevcut sürüş değerlerinden türetilen ilk tasarım katsayıları yumuşak/sert lastikte üst hız için 0,94/1,06, fren için 1,12/0,90. Hafif/ağır zırhta kütle 0,92/1,12, can 0,92/1,10 ve ivme 1,12/0,90. Yumuşak lastik ile hafif zırh dönüşe ayrı ayrı 1,06, sert ile ağır ayrı ayrı 0,94 katsayısı uygular. Hazır robotların ölçümleri iki sürümde de aynı çıktı.

## Kayıt, seri ve eğitim

Hedefli PlayMode koşusunda 5 test geçti. Menüde önizlemeden vazgeçmek mevcut kütleyi korudu. Art arda iki uygulama ve hazır robota dönüş, geçici dosyadan yeniden okuma, eğitime girip çıkma ve Tactical'a geçiş denendi. Oyuncu kökü, klavye sürücüsü ve çarpışma kutusu aynı kaldı. Sekiz önizlemede collider, Rigidbody, sağlık ve saldırı bileşeni aranarak canlı savaşla etkileşim kontrol edildi.

Bilinmeyen lastik, eksik zırh ve gelecek sürüm dosyalarının üçü de açıklama üretti. Her biri için hazır Kama seçildi ve eski dosya içeriği birebir korundu. İlk 2 Galibiyet serisinde ara geri sayımda hazır robota geçiş reddedildi ve ikinci tur ilk turla aynı modüler kurulumla başladı. Eski hazır Döner kaydı modüler robotta uyumsuz bulundu ve açılmadı. Döner'e dönünce yeniden uyumlu bulundu.

Ölçüm çıktıları `RobotArena/Logs/PveEvidence/workshop-measure-final.xml` ve `.log` içinde. XML SHA-256 `e9ac2342dac46b5d555d124029bb1a17d64d1ff118ea224b86959f0791442b37`. Başlangıç kopyasındaki `stock-base-a1754a2.xml` SHA-256 `3e6f6bcfe7b75552260e5ac4d1ae42d1667d3d25e00f4e321e5f3e3792fcfc3a`. Hedefli koşunun sonunda sekiz dosya, 46 kayıt defteri girdisi ve bir eski tekrar dosyası için Restore ve Verify geçti.

## Windows x64 serbest bot düelloları

Windows x64 test oyunu, Direct3D 12 ve GeForce RTX 5090 ile yedi testin yedisini geçti. Testlerin kaynak revizyonu `9e73eef944a7dd98cf8af7dc219f4c134fd5538d`. Sanal klavye her düelloda aynı takip kuralıyla W/A/S/D ve boşluk tuşlarını gönderdi. Oyuncu ve bot normal doğuş noktalarından başladı. Bot, temas, silah, zaman sınırı ve arena etkileri maç boyunca açıktı. Open hızlı düello Kolay/Saldırgan, Tactical ve PulseArena karşılaştırmaları Zor/Karşılıkçı ayarındaydı. Her maçta tek sonuç oluştu, maçlar can tükenmesiyle bitti. İsabet sütunu oyuncunun silahını sayar. Temas ve tehlike hasarı ayrıca uygulanır.

| Arena | Lastik / zırh / silah | Sonuç | Süre s | Kalan can oyuncu / bot | Silah isabeti |
| --- | --- | --- | ---: | --- | ---: |
| Open | Yumuşak / hafif / Döner | Bot | 18,34 | 0 / 20,2 | 2 |
| Open | Yumuşak / hafif / Kama | Oyuncu | 8,86 | 67 / 0 | 5 |
| Open | Yumuşak / ağır / Döner | Oyuncu | 5,02 | 110 / 0 | 4 |
| Open | Yumuşak / ağır / Kama | Oyuncu | 8,84 | 85 / 0 | 5 |
| Open | Sert / hafif / Döner | Bot | 17,10 | 0 / 17,7 | 2 |
| Open | Sert / hafif / Kama | Bot | 12,18 | 0 / 53 | 1 |
| Open | Sert / ağır / Döner | Bot | 15,95 | 0 / 12,6 | 2 |
| Open | Sert / ağır / Kama | Bot | 17,94 | 0 / 11,2 | 2 |
| Tactical | Yumuşak / hafif / Döner | Oyuncu | 10,03 | 90,9 / 0 | 4 |
| Tactical | Sert / ağır / Kama | Bot | 13,45 | 0 / 23,9 | 3 |
| PulseArena | Yumuşak / hafif / Döner | Oyuncu | 4,03 | 42 / 0 | 3 |
| PulseArena | Sert / ağır / Kama | Oyuncu | 6,61 | 35 / 0 | 3 |

Grafik akışında sert/ağır/Kama önizlemeden uygulandı. Oyuncu Open maçını 29,30 saniyede rakibe 1,6 HP bırakarak kaybetti. Sonuç ekranından başlatılan rövanş yeni karşılaşma kimliği, aynı kurulum, tam can, bağlı parçalar ve sıfır silah beklemesiyle başladı. İkinci sonuç 27,03 saniyede geldi. Gerçek 1280 × 720 kareler [Atölye](evidence/pve-workshop-20261124/01-workshop.jpg), [Maç](evidence/pve-workshop-20261124/02-playing.jpg), [Sonuç](evidence/pve-workshop-20261124/03-result.jpg) ve [Rövanş](evidence/pve-workshop-20261124/04-rematch.jpg).

**Denge kararı.** İlk tekrar ölçümünde öncelik sert lastiğin frenine verilmeli. Ölçülen %13 yüksek hızın karşılığında durma mesafesi %60 uzadı. Bu takip kuralıyla Open'da sert lastik dört eşleşmeyi de kaybetti, yumuşak lastik dördün üçünü kazandı. Her eşleşmede bir örnek var. Farklı fren zamanlaması kullanan bir sürücüyle aynı hücreleri tekrarlayıp sonuç sürerse sert lastiğin fren katsayısını 0,90'dan 0,95'e taşımak ilk küçük deneme olur. Kama aynı mesafede 2,4 m/s itiş verirken altı isabet gerektiriyor. PulseArena'da sert/ağır/Kama 35 HP ile kazandı. Bu avantajı tekrar gözlemek için ilk turda silah değerleri korunabilir.

## Yeniden çalıştırma

Windows çalışma alanında görevler için ortak `PvE/Run-PveUnity.ps1` yedekleme, Restore ve Verify yapar. Unity proje kökü `PvE/workshop/RobotArena`. Geçici tarihçe, tekrar ve atölye dosyalarını fixture kendi çalışma dizininde oluşturur.

```powershell
$dir = (Resolve-Path './PvE/workshop/RobotArena/Logs/PveEvidence').Path
$env:WORKSHOP_WINDOWS_FRAMES = Join-Path $dir 'workshop-frames'
$env:WORKSHOP_PLAYER_XML = Join-Path $dir 'workshop-win-local.xml'
& './PvE/Run-PveUnity.ps1' -Task workshop -Mode StandaloneWindows64 -Label workshop-win-02 -Filter 'WorkshopDuelTests' -AdditionalSaveFiles @('pve-workshop-v1.json','pve-workshop-v1.json.lock')
```

Editor XML'i `RobotArena/Logs/PveEvidence/workshop-win-02.xml`, SHA-256 `a21ca5d8d6ae5ba22b7dcff7b88fe0335dc9f50c4a7fd2e3bd08ef75809385bd`. Aynı koşunun doğrudan Windows oyuncusundan gelen XML'i `workshop-win-local.xml`, SHA-256 `1524929ff0f864ea5344117ebd45aa9619397499c442722add3fdad921aaac53`. Önceki `workshop-win-01` denemesi Editor'a sonuç dönmediği için ölçümlere dahil edilmedi. Son Windows koşusunda Restore ve Verify sekiz dosya, 46 kayıt defteri girdisi ve bir tekrar dosyasında eşleşti. Normal Windows sürümünün derleme kaydı `RobotArena/Logs/PveEvidence/workshop-release-01.log`, çalıştırılabilir dosyası `RobotArena/Builds/Windows/RobotSavaslari.exe`.
