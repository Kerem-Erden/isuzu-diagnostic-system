Dört haftalık geliştirme planı
1. Hafta — Tasarımın kilitlenmesi ve ilk çalışan bağlantı

Amaç: Araştırmada boğulmadan geliştirmeye başlayacak zemini oluşturmak.

Yapılacaklar:

Mevcut FR/NFR listesini MVP’ye göre son kez temizlemek
Scope ve out-of-scope’u kilitlemek
Teknolojileri kesinleştirmek:
ESP32: C + ESP-IDF
PC: C# + .NET
Veritabanı: SQLite
İlk haberleşme: USB Serial
Repo ve proje klasörlerini oluşturmak
ESP32 hello_world, build ve flash testi
ESP32’den PC’ye örnek seri veri göndermek
C# uygulamasında seri portları listelemek ve bağlanmak
Basit mesaj protokolünü tasarlamak

Hafta sonu çalışan çıktı:

ESP32 → USB Serial → C# PC uygulaması

Örneğin ESP32:

RPM:750
TEMP:86
VOLTAGE:13.8

gönderecek, PC uygulaması bunları okuyup gösterecek.

Bu hafta C kitabını projenin önüne koymayacağız. Pointer veya struct gerektiğinde konuyu açıp öğrenip devam edeceğiz.

2. Hafta — CAN/OBD ve ESP32 firmware

Amaç: ESP32’nin yalnızca sahte veri değil, CAN/OBD verisi işleyebilmesi.

Yapılacaklar:

CAN transceiver bağlantısı
ESP-IDF TWAI sürücüsü
CAN frame gönderme ve alma
İlk etapta kontrollü test verisi veya loopback testi
OBD-II istek-cevap mantığı
Temel Mode 01 canlı veri sorguları
DTC okuma için Mode 03
DTC byte’larını P0401 biçimine dönüştürme
ESP32-PC mesaj protokolünü düzenlemek
Bağlantı kopması ve zaman aşımı yönetimi

Hafta sonu hedefi:

RPM
motor sıcaklığı
sistem voltajı
mümkünse hız ve motor yükü
örnek veya gerçek DTC

PC uygulamasına aktarılacak.

Gerçek araç protokolü beklediğimiz gibi çalışmazsa haftayı çöpe atmayız. Önceden kaydedilmiş veya simüle edilmiş CAN/OBD cevaplarıyla tüm sistemi geliştirmeye devam ederiz.

3. Hafta — PC uygulaması, veritabanı ve teşhis mantığı

Amaç: Ham veriyi işe yarayan teşhis ekranına çevirmek.

Yapılacaklar:

C# masaüstü arayüzü
Bağlantı ekranı
Canlı veri ekranı
DTC tarama ekranı
SQLite veri tabanı
DTC açıklaması
Muhtemel nedenler
Önerilen kontrol adımları
DTC → ilgili canlı veriler eşlemesi
Canlı verileri referans kurallarıyla karşılaştırma
Normal, warning ve critical durumları
Hysteresis
Süre bazlı eşik doğrulaması
Yalnızca state değiştiğinde log oluşturma

Örnek akış:

P0087 tespit edildi
        ↓
İlgili profil veritabanından getirildi
        ↓
Target rail pressure
Actual rail pressure
RPM
Engine load
        ↓
Referanslarla karşılaştırıldı
        ↓
Çözüm ve kontrol adımları gösterildi

İlk veri tabanında yüzlerce kod olmasına gerek yok. 10–20 düzgün hazırlanmış Isuzu DTC profili, bin tane çöp kayıttan daha değerlidir.

4. Hafta — DTC temizleme, entegrasyon ve test

Amaç: Parçaları birleştirip gösterilebilir, çalışır prototip çıkarmak.

Yapılacaklar:

Standart DTC temizleme fonksiyonu
Temizleme öncesi kullanıcı onayı
Silmeden önce mevcut kodları loglama
Silme sonrası otomatik yeniden tarama
Geri gelen aktif kodları gösterme
Bağlantı ve bozuk veri testleri
CAN/OBD cevap vermeme senaryoları
Hysteresis ve uyarı testleri
Veritabanı testleri
UI hatalarını düzeltme
README
Mimari diyagram
Kurulum dokümanı
Test sonuçları
Demo videosu
SDLC raporunun Phase 2–5 bölümlerini güncelleme
Dört hafta sonunda “Done” sayılacak şartlar

Aşağıdakiler çalışırsa MVP tamamlanmış kabul edilir:

ESP32 ve PC uygulaması bağlanıyor
Canlı veriler ekranda sürekli güncelleniyor
Bellek kontrolsüz büyümüyor
DTC okunuyor
DTC açıklaması ve çözüm bilgisi gösteriliyor
DTC’ye göre ilgili canlı veriler öne çıkarılıyor
Referans dışı değerler hysteresis ile uyarı üretiyor
Uyarı başlangıcı ve bitişi loglanıyor
Desteklenen ortamda DTC temizleme deneniyor
Sistem bağlantı kesilince çökmüyor
Proje kurulup yeniden çalıştırılabiliyor
Dokümantasyon ve test sonuçları mevcut