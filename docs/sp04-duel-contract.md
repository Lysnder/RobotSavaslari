# PvE başlangıç sözleşmesi ve SP04 provası

MainArena’da hazır robot kartı oyuncunun silahını seçer. Döner seçilince bot Kama kullanır. Kama seçilince bot Döner kullanır. Her iki yönde aynı oyuncu sürücüsü, bot karar sistemi, MeleeAttack, RobotHealth ve MatchController çalışır. Botun sökülebilir silah parçası ve isabet profili birlikte değişir.

| Aşama | Kaynak ve doğrulama |
| --- | --- |
| Ready | Menü arena, kişilik, zorluk, süre ve maç biçimini mevcut DuelSetup v1 tercihlerinden alabilir. Robotlar canlı seçimden okunur. |
| Geri sayım | DuelSetup v2, geçerli katalog revizyonu, mevcut arena, iki robot kimliği, oyuncu ve bot rolleri ile o karşılaşma için üretilen kimliği taşır. MatchController bunları mevcut sahneyle karşılaştırır, bot silahını takar ve salt okunur DuelStart değerini saklar. |
| Maç ve seri | Komutlar mevcut sürücü ve bota gider. Hasar ve raund sonucu mevcut otoriteden gelir. Aynı serinin sonraki raundu aynı başlangıç değerini kullanır. |
| Rövanş | Aynı ayarlarla yeni karşılaşma kimliği oluşur. Geçmiş v1 kaydında yalnız bulunan kurallar kullanılır. Arena ve robot kimlikleri güncel seçimden alınır. |
| Atölye | Birleşik kurulumda atölyenin uyguladığı sabit şasi, lastik, zırh ve silah kimlikleri kabul edilir. Maç otoritesi kimliği canlı robotta takılı olanla karşılaştırır. Sayısal bir hasar, can veya kütle alanı başlangıç sözleşmesinde bulunmaz. |

## M2 sonrası iki Windows istemcisi

Bu bölüm ayrı ürün onayından sonra yürütülecek SP04 provasının önerilen kabul koşullarıdır.

- İlk turda bir Open arenası ve iki hazır robot kullanılır. Ev sahibi karşılaşma kimliğini, katalog revizyonunu, iki robotu ve slot sahipliğini doğrular. Ardından iki tarafa aynı kurulum onayını iletir ve geri sayımı başlatır.
- İstemci yalnız oturum kimliği, slot, artan komut numarası, hareket, dönüş ve saldırı isteği gönderir. Ev sahibi yinelenen, eski, yanlış slota ait ya da izin verilen hızın üstündeki komutları eler. Kabul edilen isabet ve bitiş olaylarını tek sıra numarasıyla yayınlar. Görüntü yumuşatma için pozisyon ve hız anlık görüntüleri kullanılır.
- İki ayrı Windows makinesinde önce yerel ağ, sonra 100 ms RTT, 20 ms jitter ve %1 paket kaybı denenir. Her iki rol yönünde 10 maç için komuttan ekranda görülen isabete p50/p95, onaylı isabet/sonuç kimlikleri, yinelenen olay ve iki uçtaki son can değerleri kaydedilir.
- İstemci koparsa maç askıya alınır. 30 saniyelik yeniden bağlanma penceresinde son otorite durumu ve olay sırası alınır. Pencere aşılırsa oturum kontrollü kapatılır. Ev sahibi koparsa maç kapanır. Yarım kalan oturum galibiyet kaydı üretmez.
- Taşıma, relay ve barındırma kararı iki yöndeki gerçek paket boyutu, sıklık ve trafik ölçümünden sonra verilir. Atölye PvP kapsamına girdiğinde iki uç katalog revizyonunda anlaşır, ev sahibi parça kimliklerini tekrar doğrular ve geri sayımda kilitler.
