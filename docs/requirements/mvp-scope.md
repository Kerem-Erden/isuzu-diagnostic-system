# MVP Scope 

## 1. Purpose

Bu dokumanin amaci, Isuzu Dianostic System projesinin 
Minumum Viable Product kapsamini tanimlamaktir.

MVP, sistemin temel teknik yaklasiminin calistigini kanitlayan
ve ana teshis fonksiyonlarini gosteren ilk kullanilabilir prototipi ifade eder.

Bu surumun amaci profesyonel servis cihazlarinin butun ozelliklerini
sunmak degil, aractan veri alinabildigini, ESP32 ile PC uygulamasinin
haberlesebildigini ve alinan teshis bilgilerinin anlamli sekilde kullaniciya sunabildigini gostermektir.

## 2. Target Users

- Oto elektrik teknisyenleri
- Araç bakım ve onarım teknisyenleri
- Isuzu araçları üzerinde çalışan servis personeli
- Otomotiv teşhis sistemlerini inceleyen geliştiriciler

## 3. In Scope

### 3.1 ESP32 and Vehicle Communication

- ESP32 tabanli teshis cihazinin gelistirilmesi
- SN65HVD230 CAN transceiver kullanilmasi
- CAN/OBD-II haberlesmesi
- Haberlesme hatalarinin PC uygulamsina bildirilmesi

### 3.2 ESP32 and PC Communication

- ESP32 ile windows uygulamasi arasinda USB Serial haberlesmesi
- Chiaz baglanti durumunun gosterilmesi
- Canli veri ve teshis mesajlarinin PC uygulamasina aktarilmasi
- Bozuk veya desteklenmeyen mesajlarin reddedilmesi

### 3.3 Live Data 

- Canli arac verilerinin okunmasi
- Canli verilerin masaustu uygulamasinda goruntulenmesi
- ilk asamada temel parametrelerin desteklenmesi

Ornek parametreler:

- Engine RPM
- Engine Coolant Temperature
- Battery Voltage
- Vehicle Speed
- Engine Load

Araç ve ECU desteğine bağlı olarak aşağıdaki parametreler de eklenebilir:

- Fuel Rail Pressure
- Engine Oil Pressure
- Engine Oil Temperature
- Turbo Pressure
- Injector Correction Values
- EGR Data
- DPF Data
- Exhaust Gas Temperature

### 3.4 Live Data Reference Comparison

- Canlı verilerin veri tabanındaki referans aralıklarla karşılaştırılması
- Referans dışındaki değerlerin kullanıcıya uyarı olarak gösterilmesi
- Uyarıların kesin arıza teşhisi olarak sunulmaması
- Hysteresis kullanılması
- Süre tabanlı eşik doğrulaması
- Uyarının başlangıç ve bitiş durumlarının loglanması
- Aynı uyarının sürekli tekrar loglanmasının engellenmesi

### 3.5 Diagnostic Trouble Codes

- Desteklenen DTC kayıtlarının okunması
- DTC kodlarının kullanıcıya listelenmesi
- DTC kodunun Türkçe açıklamasının gösterilmesi
- Muhtemel nedenlerin gösterilmesi
- Önerilen teşhis adımlarının gösterilmesi
- DTC ile ilişkili canlı verilerin öncelikli gösterilmesi

### 3.6 DTC Clearing

- Desteklenen DTC kayıtlarının kullanıcı onayıyla temizlenmesi
- Temizleme öncesindeki DTC listesinin kaydedilmesi
- Temizleme işleminden sonra yeniden tarama yapılması
- Silinmeyen veya yeniden oluşan DTC kodlarının gösterilmesi
- İşlem sonucunun kullanıcıya bildirilmesi

### 3.7 Desktop Application

- Windows masaüstü uygulaması
- ESP32 bağlantı ekranı
- Canlı veri ekranı
- DTC ekranı
- DTC ayrıntı ve teşhis ekranı
- Uyarı ve bağlantı durumlarının gösterilmesi
- Temel teşhis geçmişinin yerel olarak saklanması

### 3.8 Local Database

- SQLite tabanlı yerel veri tabanı
- DTC açıklamaları
- Muhtemel nedenler
- Önerilen kontrol adımları
- DTC ve ilgili canlı veri eşlemeleri
- Referans değerler
- Araç, motor ve ECU bilgileri
- Teşhis ve uyarı geçmişi

## 4. Out of Scope

Aşağıdaki özellikler MVP kapsamında değildir:

- Mobil uygulama
- Tüm araç markalarının desteklenmesi
- Bütün Isuzu modellerinin desteklenmesi
- ABS, airbag ve şanzıman modüllerinin tam desteği
- ECU yazılım güncelleme
- ECU kodlama veya programlama
- Immobilizer işlemleri
- Anahtar programlama
- Aktüatör testleri
- Enjektör kodlama
- DPF zorunlu rejenerasyon komutları
- Bulut altyapısı
- Kullanıcı hesabı
- Uzaktan teşhis
- Yapay zekâ tarafından kesin arıza teşhisi
- Profesyonel IDSS veya Jaltest seviyesinde tam araç desteği
- Mobil mağaza yayını
- Özel PCB tasarımı

## 5. MVP Completion Criteria

MVP aşağıdaki koşullar karşılandığında tamamlanmış kabul edilecektir:

- ESP32 firmware'i karta başarıyla yüklenebilmelidir.
- ESP32 ile Windows uygulaması arasında veri aktarımı çalışmalıdır.
- Canlı veriler masaüstü uygulamasında güncellenebilmelidir.
- Desteklenen DTC kodları okunabilmelidir.
- DTC açıklaması ve teşhis bilgileri gösterilebilmelidir.
- DTC ile ilişkili canlı veriler öne çıkarılabilmelidir.
- Referans dışı canlı veriler için uyarı oluşturulabilmelidir.
- Hysteresis ve süre tabanlı doğrulama çalışmalıdır.
- Desteklenen koşullarda DTC temizleme işlemi gerçekleştirilebilmelidir.
- Temizleme sonrasında otomatik yeniden tarama yapılmalıdır.
- Bağlantı kesildiğinde masaüstü uygulaması çökmemelidir.
- Temel teşhis ve uyarı geçmişi kaydedilebilmelidir.
- Proje kurulum ve kullanım adımları dokümante edilmelidir.

## 6. Future Scope

MVP sonrasında değerlendirilebilecek özellikler:

- Diğer Isuzu modelleri ve motorları
- Diğer araç markaları
- Mobil uygulama
- Bluetooth veya Wi-Fi haberleşmesi
- Daha gelişmiş teşhis protokolleri
- J1939 desteğinin genişletilmesi
- ABS, şanzıman ve diğer ECU modülleri
- Rapor oluşturma
- Güncellenebilir araç veri paketleri
- Gelişmiş grafik ve kayıt özellikleri
