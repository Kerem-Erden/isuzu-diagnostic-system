# System Overview

## 1. Purpose

Bu doküman, Isuzu Diagnostic System projesinin genel sistem
mimarisini, ana bileşenlerini ve bileşenler arasındaki veri akışını
tanımlar.

Bu mimari MVP geliştirme sürecinde güncellenebilir. Henüz araştırması
tamamlanmamış protokoller ve üreticiye özel teşhis yöntemleri kesin
tasarım kararı olarak değerlendirilmemelidir.

## 2. High-Level Architecture

```text
Vehicle ECU / CAN Network
          |
          | CAN / OBD-II
          v
SN65HVD230 CAN Transceiver
          |
          | TWAI TX / RX
          v
ESP32 Diagnostic Gateway
          |
          | USB Serial
          v
Windows Desktop Application
          |
          +--------------------+
          |                    |
          v                    v
Diagnostic Database       Diagnostic Logs
(SQLite)                  (SQLite)
```

## 3. Main Components

### 3.1 Vehicle ECU and CAN Network

Araç ECU'su canlı motor verilerini ve Diagnostic Trouble Code
bilgilerini sağlayan kaynak sistemdir.

Desteklenen araç ve ECU'ya bağlı olarak aşağıdaki iletişim
yöntemlerinden biri veya birkaçı kullanılabilir:

- Standard OBD-II services
- CAN frames
- ISO-TP
- J1939
- Manufacturer-specific diagnostic messages

Üreticiye özel Isuzu teşhis yöntemleri araştırma ve araç testleri
sonucunda doğrulanacaktır.

### 3.2 SN65HVD230 CAN Transceiver

SN65HVD230 modülü, aracın fiziksel CAN hattı ile ESP32'nin TWAI
kontrolcüsü arasında elektriksel bağlantı sağlar.

Temel görevleri:

- CAN High ve CAN Low sinyallerini almak
- Fiziksel CAN sinyallerini ESP32'nin anlayabileceği lojik sinyallere çevirmek
- ESP32 tarafından oluşturulan lojik sinyalleri CAN hattına aktarmak

SN65HVD230 teşhis mesajlarının anlamını yorumlamaz. Yalnızca fiziksel
haberleşme katmanında görev yapar.

### 3.3 ESP32 Diagnostic Gateway

ESP32, araç ile Windows masaüstü uygulaması arasında gateway görevi
görür.

Temel sorumlulukları:

- TWAI sürücüsünü başlatmak
- CAN mesajlarını göndermek ve almak
- OBD-II veya desteklenen teşhis sorgularını oluşturmak
- ECU cevaplarını almak
- Canlı veri ve DTC bilgilerini ayrıştırmak
- DTC temizleme komutlarını yönetmek
- Haberleşme hatalarını belirlemek
- Araçtan alınan verileri PC uygulamasına aktarmak
- PC uygulamasından gelen desteklenen komutları işlemek

DTC açıklamaları, çözüm önerileri ve geniş araç veri tabanı ESP32
üzerinde tutulmayacaktır. Bu bilgiler PC uygulaması ve SQLite veri
tabanı tarafından yönetilecektir.

### 3.4 USB Serial Communication

ESP32 ile Windows masaüstü uygulaması arasındaki ilk haberleşme
yöntemi USB Serial olacaktır.

USB Serial seçilmesinin nedenleri:

- Geliştirme sırasında kolay hata ayıklama
- Bluetooth eşleştirmesi gerektirmemesi
- Wi-Fi bağlantısına ihtiyaç duymaması
- Kurulum karmaşıklığının düşük olması
- Mesajların terminal üzerinden okunabilmesi

İlk prototipte satır tabanlı ve okunabilir bir mesaj formatı
kullanılacaktır. İhtiyaç oluşursa mesaj kimliği, checksum veya ikili
veri formatı daha sonra değerlendirilebilir.

### 3.5 Windows Desktop Application

Windows masaüstü uygulaması sistemin kullanıcı arayüzünü oluşturur.

Temel sorumlulukları:

- Kullanılabilir seri portları listelemek
- ESP32 cihazına bağlanmak ve bağlantıyı kesmek
- ESP32'den gelen mesajları sürekli okumak
- Gelen mesajları doğrulamak ve ayrıştırmak
- Canlı araç verilerini görüntülemek
- DTC kodlarını listelemek
- DTC açıklamalarını ve teşhis bilgilerini veri tabanından getirmek
- DTC ile ilişkili canlı verileri öncelikli göstermek
- Referans değer karşılaştırmasını gerçekleştirmek
- Hysteresis ve uyarı durumlarını yönetmek
- DTC temizleme işlemi için kullanıcı onayı almak
- Teşhis ve uyarı geçmişini kaydetmek
- Haberleşme hatalarını kullanıcıya göstermek

Masaüstü uygulamasının ilk teknoloji adayı C# ve .NET'tir.

### 3.6 SQLite Diagnostic Database

SQLite sistemin yerel ve çevrimdışı bilgi kaynağı olacaktır.

Veri tabanında aşağıdaki bilgiler saklanabilir:

- Araç markaları ve modelleri
- Model yılları
- Motor ve ECU türleri
- DTC kodları ve açıklamaları
- Muhtemel arıza nedenleri
- Önerilen kontrol adımları
- DTC ile ilişkili canlı veri parametreleri
- Canlı veri referans değerleri
- Hysteresis giriş ve çıkış eşikleri
- Süre tabanlı doğrulama kuralları
- Teşhis geçmişi
- Uyarı geçmişi

SQLite seçilmesinin nedenleri:

- Ayrı bir veritabanı sunucusu gerektirmemesi
- Tek bir yerel dosyada çalışabilmesi
- Çevrimdışı kullanıma uygun olması
- C# ve .NET ile kullanılabilmesi
- MVP için yeterli sadelik ve performans sunması

## 4. Main Data Flows

### 4.1 Live Data Flow

```text
Desktop application requests live data
                  |
                  v
ESP32 creates diagnostic request
                  |
                  v
Request is transmitted over CAN
                  |
                  v
Vehicle ECU returns diagnostic response
                  |
                  v
ESP32 validates and parses response
                  |
                  v
ESP32 sends live data to desktop application
                  |
                  v
Desktop application displays the value
                  |
                  v
Reference rule engine evaluates the value
                  |
                  v
Normal / Warning / Critical state is determined
```

### 4.2 DTC Reading Flow

```text
User selects "Read DTC"
          |
          v
Desktop application sends command to ESP32
          |
          v
ESP32 sends supported DTC request to ECU
          |
          v
ECU returns stored DTC information
          |
          v
ESP32 converts response into DTC codes
          |
          v
Desktop application receives DTC list
          |
          v
Database returns diagnostic information
          |
          v
DTC details and related live data are displayed
```

### 4.3 DTC Clearing Flow

```text
User selects "Clear DTC"
          |
          v
Desktop application displays warning
          |
          v
User confirms operation
          |
          v
Current DTC list is stored
          |
          v
Clear command is sent to ESP32
          |
          v
ESP32 sends supported clear request to ECU
          |
          v
Operation result is returned
          |
          v
DTCs are scanned again
          |
          v
Remaining or recurring DTCs are displayed
```

### 4.4 Live Data Warning Flow

```text
New live data value received
          |
          v
Applicable reference rule selected
          |
          v
Threshold and duration evaluated
          |
          v
Warning state changed?
       /        \
     No          Yes
     |            |
Continue      Create event log
                  |
                  v
Update user interface
```

Aynı uyarı durumu devam ederken her yeni veri için tekrar log
oluşturulmamalıdır. Log yalnızca durum değişikliklerinde oluşturulmalıdır.

Örnek durum geçişleri:

```text
NORMAL -> WARNING
WARNING -> CRITICAL
CRITICAL -> WARNING
WARNING -> NORMAL
```

## 5. Responsibility Boundaries

### ESP32 Responsibilities

- Düşük seviye CAN haberleşmesi
- Teşhis isteklerinin gönderilmesi
- ECU cevaplarının alınması
- Temel protokol ayrıştırma
- PC ile seri haberleşme

### Desktop Application Responsibilities

- Kullanıcı arayüzü
- DTC bilgi eşlemesi
- Referans karşılaştırması
- Hysteresis ve uyarı durumu yönetimi
- Teşhis önerileri
- Yerel veri saklama
- Geçmiş ve log yönetimi

### Database Responsibilities

- Araç ve motor bilgileri
- DTC teşhis içeriği
- Canlı veri referans kuralları
- DTC ile ilgili canlı veri eşlemeleri
- Geçmiş kayıtları

Bu sorumluluk ayrımı sayesinde ESP32 firmware'i gereksiz şekilde
büyümez. Teşhis bilgileri değiştiğinde firmware yeniden derlenmeden
veri tabanı güncellenebilir.

## 6. Initial Design Principles

- Sistem modüler olarak geliştirilecektir.
- Donanım haberleşmesi ile kullanıcı arayüzü ayrılacaktır.
- Desteklenmeyen veri geçerli teşhis sonucu olarak gösterilmeyecektir.
- Güvenilir referans bulunmayan değerler tahmin edilmeyecektir.
- DTC temizleme kullanıcı onayı olmadan gerçekleştirilmeyecektir.
- İlk sürüm Isuzu araçlarına odaklanacaktır.
- Gelecekte farklı markaların eklenmesine uygun yapı hedeflenecektir.
- Kritik tasarım kararları dokümante edilecektir.

## 7. Deferred Architecture Decisions

Aşağıdaki kararlar araştırma ve prototip sonuçlarına göre
kesinleştirilecektir:

- Kullanılacak ISO-TP kütüphanesi
- J1939 kapsamı
- Isuzu üreticiye özel teşhis yöntemi
- C# masaüstü arayüz teknolojisi
- SQLite erişim yöntemi
- Seri mesaj formatının son hâli
- Gerçek araçtan okunabilecek canlı veri parametreleri
- Desteklenecek ilk Isuzu modeli ve motoru
