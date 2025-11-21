# Modern Minecraft Mod Translator (AI Powered)

![Platform](https://img.shields.io/badge/platform-Windows-blue) ![License](https://img.shields.io/badge/license-MIT-green) ![Built With](https://img.shields.io/badge/built%20with-WPF%20%2F%20C%23%20.NET-purple)

Minecraft modlarını ve mod paketlerini, Google Translate altyapısını kullanarak saniyeler içinde istediğiniz dile çeviren, modern ve akıllı bir masaüstü uygulaması.


## 🌟 Proje Hakkında

Bu araç, İngilizce bilmeyen veya Minecraft'ı kendi ana dilinde oynamak isteyen oyuncular için geliştirilmiştir. Karmaşık dil dosyalarıyla uğraşmanıza gerek kalmadan, tek bir `.jar` dosyasını veya yüzlerce mod içeren koca bir mod paketini tek tuşla otomatik olarak çevirebilirsiniz.

Arka planda Google Translate'in ücretsiz API'sini kullanır, ancak IP engellemesi (ban) yememek için geliştirdiğimiz özel **"Akıllı Paketleme" (Smart Batching)** ve **"Güvenli Gecikme"** algoritmalarını kullanır.

## ✨ Temel Özellikler

* **Modern ve Şık Arayüz:** WPF ile hazırlanmış, göz yormayan karanlık tema (Dark Mode).
* **İki Farklı Çalışma Modu:**
    * **Tek Mod:** Sadece seçtiğiniz bir `.jar` dosyasını çevirir.
    * **Mod Paketi (Klasör):** `mods` klasörünüzdeki yüzlerce modu sırayla, otomatik olarak çevirir.
* **Otomatik Sürüm Algılama:**
    * Yeni sürümler (1.13+) için `.json` dosyalarını tanır ve işler.
    * Eski sürümler (1.12.2 ve öncesi) için `.lang` dosyalarını tanır ve işler.
* **Akıllı ve Güvenli Çeviri Motoru:**
    * Google'dan ban yememek için metinleri paketler halinde (Batching) gönderir.
    * Modlar arasında otomatik bekleme süreleri ekler.
    * Uzun cümlelerde URL hatası vermez.
    * Paket çevirisi başarısız olursa, otomatik olarak "Tek Tek Çeviri" moduna geçerek %100 başarı sağlar.
* **Format Koruma:** Minecraft'ın renk kodlarını (`§c`, `§a`) ve değişkenlerini (`%s`, `%d`) bozmadan çeviri yapar.
* **Çoklu Dil Desteği:** Türkçe, Almanca, Fransızca, İspanyolca, Rusça ve daha fazlası.
* **Akıllı İsimlendirme:** Hedef dosyayı, seçilen dile ve Minecraft sürümüne göre otomatik adlandırır (Örn: `tr_tr.json` veya `tr_TR.lang`).

## 🚀 Kurulum

1.  Bu reponun **[Releases](../../releases)** kısmından en son sürümü (`.zip` veya `.exe`) indirin.
2.  Zip dosyasını bir klasöre çıkarın.
3.  `ModernModTranslator.exe` dosyasını çalıştırın.

*(Gereksinim: Windows işletim sistemi ve yüklü değilse .NET Desktop Runtime gerektirebilir.)*

## 📖 Nasıl Kullanılır?

### Senaryo 1: Tek Bir Modu Çevirmek

1.  Uygulamayı açın.
2.  "ÇALIŞMA MODU" kısmından **"Tek Mod Dosyası (.jar)"** seçeneğini seçin.
3.  Hedef dili seçin (Örn: Türkçe).
4.  "DOSYA SEÇ" butonuna tıklayıp modunuzu seçin.
5.  "ÇEVİRİYİ BAŞLAT" butonuna basın ve arkanıza yaslanın.

### Senaryo 2: Tüm Mod Paketini Çevirmek

1.  Uygulamayı açın.
2.  "ÇALIŞMA MODU" kısmından **"Mod Paketi (Klasör)"** seçeneğini seçin.
3.  Hedef dili seçin.
4.  "MODS KLASÖRÜ SEÇ" butonuna tıklayın. Açılan pencerede, oyununuzun kurulu olduğu `mods` klasörünün içine girin ve **herhangi bir mod dosyasını seçin**. (Program klasörü otomatik algılayacaktır).
5.  Toplam mod sayısı ekranda görünecektir.
6.  "ÇEVİRİYİ BAŞLAT" butonuna basın. Program tüm modları sırayla işleyecektir.

**⚠️ Önemli Not:** Mod paketi çevirisi, mod sayısına ve internet hızınıza bağlı olarak zaman alabilir. Google'ın engellememesi için işlem bilinçli olarak yavaşlatılmıştır. Lütfen sabırlı olun.

## 🛠️ Geliştiriciler İçin (Build)

Bu projeyi geliştirmek veya kaynak koddan derlemek istiyorsanız:

1.  Repoyu klonlayın.
2.  **Visual Studio 2022** (veya daha yenisi) ile `.sln` dosyasını açın.
3.  `.NET Desktop Development` iş yükünün yüklü olduğundan emin olun.
4.  Projeyi "Release" modunda derleyin.

## ⚖️ Lisans

Bu proje MIT Lisansı altında sunulmuştur. Detaylar için `LICENSE` dosyasına bakabilirsiniz.

**Yasal Uyarı:** Bu araç, çeviri için Google Translate'in halka açık arayüzünü kullanır. Ticari olmayan, kişisel kullanım için tasarlanmıştır. Aşırı yoğun kullanım geçici IP kısıtlamalarına neden olabilir.
