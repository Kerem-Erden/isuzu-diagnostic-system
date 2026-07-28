# ESP32–PC Serial Communication Protocol

## 1. Purpose

Bu doküman, ESP32 Diagnostic Gateway ile Windows masaüstü uygulaması
arasında USB Serial üzerinden kullanılacak ilk mesajlaşma protokolünü
tanımlar.

Protokolün amacı:

- ESP32 tarafından okunan araç verilerini PC uygulamasına aktarmak
- PC uygulamasından ESP32'ye teşhis komutları göndermek
- Bağlantı ve hata durumlarını bildirmek
- Geliştirme sırasında mesajların terminal üzerinden kolayca incelenmesini sağlamak

Bu protokol MVP geliştirme sürecinde güncellenebilir.

## 2. Initial Protocol Design

İlk prototipte metin tabanlı ve satır odaklı bir protokol kullanılacaktır.

Her mesaj:

- Tek bir satırda gönderilmelidir.
- Yeni satır karakteriyle sonlandırılmalıdır.
- Alanlar `:` karakteriyle ayrılmalıdır.
- Ondalıklı sayılarda nokta kullanılmalıdır.
- Gereksiz boşluk içermemelidir.

Genel mesaj yapısı:

```text
MESSAGE_TYPE:FIELD_1:FIELD_2
```

Örnek:

```text
LIVE:RPM:750
```

Bu mesajın anlamı:

- `LIVE`: Mesajın canlı veri mesajı olduğunu belirtir.
- `RPM`: Parametrenin motor devri olduğunu belirtir.
- `750`: Parametrenin mevcut değeridir.

## 3. Why a Text-Based Protocol Is Used

Metin tabanlı protokol ilk sürümde aşağıdaki nedenlerle seçilmiştir:

- Seri terminal üzerinden doğrudan okunabilir.
- ESP32 tarafında oluşturulması kolaydır.
- C# tarafında ayrıştırılması kolaydır.
- Hata ayıklama sürecini hızlandırır.
- İlk prototip için düşük geliştirme karmaşıklığı sağlar.

Metin tabanlı protokolün bazı sınırlamaları vardır:

- İkili protokole göre daha fazla veri taşıyabilir.
- Mesaj bütünlüğünü doğrulayan bir checksum içermez.
- Karmaşık veri yapılarında yönetilmesi zorlaşabilir.

Bu sınırlamalar MVP için kabul edilmektedir. İhtiyaç oluşması durumunda
protokole mesaj kimliği, checksum veya ikili veri formatı eklenebilir.

## 4. Character and Number Rules

Mesaj anahtarları büyük İngilizce karakterlerle gönderilecektir.

Doğru:

```text
LIVE:COOLANT_TEMP:86
```

Yanlış:

```text
canli:hararet:86
```

ESP32, kullanıcıya gösterilecek Türkçe açıklamaları taşımayacaktır.
ESP32 yalnızca teknik anahtarları ve değerleri gönderecektir.

Türkçe açıklamalar Windows uygulaması tarafından üretilecektir.

Bu ayrım sayesinde:

- Firmware dil bağımsız kalır.
- Kullanıcı arayüzünün dili değiştirilebilir.
- ESP32 üzerinde gereksiz metin saklanmaz.
- Teknik mesajlarla kullanıcı mesajları birbirine karışmaz.

Ondalıklı değerlerde nokta kullanılmalıdır:

```text
LIVE:BATTERY_VOLTAGE:13.8
```

Virgül kullanılmamalıdır:

```text
LIVE:BATTERY_VOLTAGE:13,8
```

Bunun nedeni farklı işletim sistemi ve bölgesel ayarların sayı
ayrıştırma davranışlarının değişebilmesidir.

## 5. PC-to-ESP32 Commands

Windows uygulamasından ESP32'ye gönderilen komutlar `CMD` önekiyle
başlamalıdır.

### 5.1 Connection Test

```text
CMD:PING
```

ESP32 çalışıyorsa aşağıdaki cevabı vermelidir:

```text
SYS:PONG
```

Bu komut, seri portun açık olmasının yanında doğru cihazla
haberleşildiğini doğrulamak için kullanılacaktır.

### 5.2 Start Live Data

```text
CMD:LIVE:START
```

ESP32 desteklenen canlı verileri okumaya ve PC uygulamasına göndermeye
başlamalıdır.

### 5.3 Stop Live Data

```text
CMD:LIVE:STOP
```

ESP32 sürekli canlı veri gönderimini durdurmalıdır.

### 5.4 Read DTC

```text
CMD:DTC:READ
```

ESP32 desteklenen ECU'dan kayıtlı DTC bilgilerini istemelidir.

### 5.5 Clear DTC

```text
CMD:DTC:CLEAR
```

Bu komut yalnızca Windows uygulaması kullanıcıdan açık onay aldıktan
sonra gönderilmelidir.

ESP32 kendi başına otomatik DTC temizleme işlemi başlatmamalıdır.

## 6. ESP32-to-PC System Messages

### 6.1 Device Ready

ESP32 başlatma işlemini tamamladığında:

```text
SYS:READY
```

mesajını göndermelidir.

Bu mesaj ESP32'nin çalıştığını gösterir. Ancak araç ECU bağlantısının
kurulduğunu tek başına garanti etmez.

### 6.2 Pong Response

```text
SYS:PONG
```

`CMD:PING` komutuna cevap olarak gönderilir.

### 6.3 Vehicle Connection Status

Araç haberleşmesi kurulduğunda:

```text
VEHICLE:CONNECTED
```

Araç haberleşmesi kesildiğinde:

```text
VEHICLE:DISCONNECTED
```

Araçtan cevap alınamadığında:

```text
VEHICLE:NO_RESPONSE
```

## 7. Live Data Messages

Canlı veri mesajlarının genel biçimi:

```text
LIVE:PARAMETER_NAME:VALUE
```

Örnek mesajlar:

```text
LIVE:RPM:750
LIVE:COOLANT_TEMP:86
LIVE:BATTERY_VOLTAGE:13.8
LIVE:VEHICLE_SPEED:0
LIVE:ENGINE_LOAD:24
LIVE:RAIL_PRESSURE:31.5
LIVE:OIL_PRESSURE:2.8
LIVE:OIL_TEMP:91
```

Parametrelerin birimleri seri mesajda gönderilmeyecektir.

Birim bilgisi Windows uygulamasındaki parametre tanımından veya veri
tabanından alınacaktır.

Örneğin:

```text
RPM -> rpm
COOLANT_TEMP -> °C
BATTERY_VOLTAGE -> V
RAIL_PRESSURE -> MPa
```

Bunun nedeni her mesajda aynı birim bilgisini tekrar göndererek gereksiz
veri oluşturmamaktır.

## 8. DTC Messages

Bir DTC taraması başladığında ESP32:

```text
DTC:BEGIN
```

mesajını göndermelidir.

Bulunan her DTC ayrı mesaj olarak gönderilmelidir:

```text
DTC:CODE:P0401
DTC:CODE:P0087
```

Tarama sona erdiğinde:

```text
DTC:END
```

mesajı gönderilmelidir.

Örnek tam akış:

```text
DTC:BEGIN
DTC:CODE:P0401
DTC:CODE:P0087
DTC:END
```

Hiç DTC bulunmadığında:

```text
DTC:BEGIN
DTC:NONE
DTC:END
```

gönderilmelidir.

DTC açıklaması, çözüm önerileri ve ilgili canlı veri eşlemeleri ESP32
tarafından gönderilmeyecektir.

Windows uygulaması yalnızca DTC kodunu kullanarak SQLite veri tabanından
ilgili teşhis profilini bulacaktır.

## 9. DTC Clear Result Messages

DTC temizleme isteği işlenmeye başladığında:

```text
CLEAR:STARTED
```

İşlem başarılı olduğunda:

```text
CLEAR:SUCCESS
```

İşlem başarısız olduğunda:

```text
CLEAR:FAILED:REASON_CODE
```

Örnekler:

```text
CLEAR:FAILED:NO_VEHICLE_CONNECTION
CLEAR:FAILED:ECU_NO_RESPONSE
CLEAR:FAILED:REQUEST_REJECTED
CLEAR:FAILED:UNSUPPORTED
```

Windows uygulaması teknik hata kodunu kullanıcıya anlaşılır bir mesaj
olarak çevirmelidir.

Örneğin:

```text
ECU_NO_RESPONSE
```

arayüzde:

```text
ECU'dan cevap alınamadı.
```

şeklinde gösterilebilir.

## 10. Error Messages

Genel hata mesajı biçimi:

```text
ERROR:SOURCE:ERROR_CODE
```

Örnekler:

```text
ERROR:CAN:RECEIVE_TIMEOUT
ERROR:CAN:BUS_OFF
ERROR:OBD:INVALID_RESPONSE
ERROR:SERIAL:INVALID_COMMAND
ERROR:SYSTEM:INTERNAL_ERROR
```

`SOURCE`, hatanın oluştuğu katmanı belirtir.

Örnek kaynaklar:

- `CAN`
- `OBD`
- `SERIAL`
- `SYSTEM`

Bu yapı hataların hangi modülden geldiğini daha kolay anlamamızı sağlar.

## 11. Message Validation Rules

Windows uygulaması gelen her satırı doğrudan geçerli veri kabul
etmemelidir.

Aşağıdaki durumlarda mesaj reddedilmelidir:

- Mesaj boşsa
- Mesaj bilinen bir mesaj türüyle başlamıyorsa
- Beklenen alan sayısı eksikse
- Sayısal değer ayrıştırılamıyorsa
- Parametre adı desteklenmiyorsa
- Satır izin verilen maksimum uzunluğu aşıyorsa

Örnek bozuk mesajlar:

```text
LIVE:RPM
LIVE:RPM:ABC
LIVE::750
UNKNOWN:DATA:123
```

Bozuk mesaj uygulamanın çökmesine neden olmamalıdır.

Mesaj reddedilebilir ve geliştirme loguna kaydedilebilir.

## 12. Initial State Flow

Beklenen ilk bağlantı akışı:

```text
ESP32 -> SYS:READY

PC    -> CMD:PING
ESP32 -> SYS:PONG

PC    -> CMD:LIVE:START
ESP32 -> LIVE:RPM:750
ESP32 -> LIVE:COOLANT_TEMP:86
ESP32 -> LIVE:BATTERY_VOLTAGE:13.8
```

DTC okuma akışı:

```text
PC    -> CMD:DTC:READ
ESP32 -> DTC:BEGIN
ESP32 -> DTC:CODE:P0401
ESP32 -> DTC:END
```

## 13. Deferred Protocol Features

Aşağıdaki özellikler ilk protokol sürümünde bulunmayacaktır:

- Checksum
- Mesaj sıra numarası
- Mesaj zaman damgası
- Binary encoding
- JSON mesaj formatı
- Sıkıştırma
- Şifreleme
- Bluetooth veya Wi-Fi transport

Bu özellikler sistemin gerçek ihtiyaçları ortaya çıktıktan sonra
değerlendirilecektir.

## 14. Protocol Version

İlk protokol sürümü:

```text
Version: 0.1
```

Protokolde geriye dönük uyumluluğu etkileyen bir değişiklik yapılırsa
sürüm numarası güncellenecektir.
