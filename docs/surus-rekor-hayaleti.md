# Kişisel rekor hayaleti

En iyi geçerli tur yerel olarak saklanır; sonraki denemede saydam ve
etkileşimsiz bir hayalet olarak izlenir.

## Davranış

* İlk **geçerli** tur kaydedilir.
* Yalnızca **daha iyi geçerli** tur rekoru günceller. Geçersiz ya da daha yavaş
  tur rekoru ezmez.
* Yeni denemede hayalet, denemeyle **aynı saatte** ilerler: `Yeniden dene`
  süreyi sıfırlayınca hayalet de başa döner.
* Eğitimden çıkış hayaleti temizler.
* Panelde rekor süresi görünür; hayalet ekrandayken bunu da belirtir.

## Kayıt

`Application.persistentDataPath/driving-personal-best.json`

PlayerPrefs değil kendi dosyası: eşleme anahtarlarıyla ya da başka paketin
verisiyle karışmaz. Kayıt üç şey taşır:

| Alan | Niçin |
| --- | --- |
| `version` | Kayıt biçimi değişirse eski kayıt kullanılmaz |
| `courseId` | Rota değişirse eski rekor aynı parkur sayılmaz |
| `lapTime` + örnekler | Turun kendisi |

`courseId` rotadan türer: başlangıç pozu ile her kontrol noktasının konumu ve
yarıçapı. Rota bir metre bile kaysa kimlik değişir.

### Bozuk ya da eksik kayıt

Okuma ve yazma istisna fırlatmaz. Şu durumlarda kayıt **sessizce atlanır** ve
eğitim rekorsuz başlar:

* Dosya yok ya da boş
* JSON bozuk
* `version` farklı
* `courseId` bu parkura ait değil
* Süre sıfır ya da ikiden az örnek var

## Hayalet neyi taşımaz

Hayalet robotun hiyerarşisindeki `Renderer`'lardan **yalnızca mesh ve
materyal** kopyalanarak kurulur. Dolayısıyla üzerinde bulunmayanlar:

* Rigidbody, Collider
* Sağlık, dövüş, bot bileşenleri
* Kontrol noktası üreticisi, motor, sürücü

Gölge de kapalıdır. Fizik, hasar, kontrol noktası ilerlemesi ve maç sonucu
üzerinde sıfır etkisi vardır; test bunu bileşen bileşen doğrular.

Saydamlık için kaynak materyallerin kopyası alınır (alpha 0.35) ve nesneyle
birlikte yok edilir. Kopyalanacak görsel parça yoksa hayalet hiç kurulmaz.

## Yapı

| Dosya | Sorumluluk |
| --- | --- |
| `Scripts/Training/GhostLapRecording.cs` | Kayıt verisi, sürüm/parkur kontrolü, ara değer hesabı |
| `Scripts/Training/GhostLapStore.cs` | Yerel dosyaya okuma/yazma, güvenli atlama |
| `Scripts/Training/GhostRunner.cs` | Tur kaydı, rekor seçimi, hayaletin kurulup oynatılması |
| `Scripts/Training/TimeTrialHud.cs` | Rekor süresinin gösterimi |

Küçük ve kendine yeten bir veri yapısı kullanıldı; dövüş tarafındaki replay
sınıflarına bağlanılmadı.

## Testler

`RobotArena/Assets/Tests/Training/PlayMode/GhostLapTests.cs` — 11 test:

* Veri: eski sürüm, başka parkur, eksik kayıt, bozuk dosya, yazma/okuma, ara
  değer hesabının sınırlarda taşmaması.
* Koşu: ilk geçerli turun kaydedilmesi, geçersiz turun rekor olmaması, daha
  yavaş turun rekoru ezmemesi, hayaletin fizik/dövüş bileşeni taşımaması,
  çıkışta hayaletin temizlenmesi.

Son koşu: **83/83 PlayMode**, **49/49 EditMode**.

İlgili: [sürüş eğitimi](surus-egitimi.md)
