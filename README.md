# AppsTester
Сервис для автоматической проверки заданий по android-разработке и выставлению оценок на платформе Moodle

## Технологии
* .NET 5.0
* Moodle
* Redis (опционально)
* RabbitMQ
* Sentry
* ADB
* Android SDK


## Требования
- JDK 11+ (при наличии нескольких необходимо установить переменную окружения JAVA_HOME на корректную версию)

```mermaid
sequenceDiagram
    autonumber
    actor Студент

    Студент ->> Moodle: Загрузка задания
    loop Раз в секунду
      Controller ->> Moodle: Запрос решений
    end
    note right of Controller: Есть актуальные решения
    Controller -) Очередь сообщений: Отправка запроса на проверку
    Очередь сообщений ->> +Android checker: Запрос на проверку
    Android checker ->> Controller: Запрос задания и решения
    Android checker ->> Android device (через ADB): Выполнение теста
    Android device (через ADB) ->> Android checker: Результат работы
    Android checker -> Android checker: Парсинг рзультата и формирование результата проверки
    Android checker -) -Очередь сообщений: Результат проверки
    Очередь сообщений ->> Controller: Результат проверки
    Controller ->> Moodle: Обновление результата решения
```


## Конфигурация
Для локальной разработки можно хранить конфигурацию в `/{Project}}/appsettings.Local.json`
#### AppsTester/Controller
* **ConnectionStrings__RabbitMq** - строка подключения к RabbitMQ. Пример: ```amqp://root:root@localhost:5672```
* **ConnectionStrings__DefaultConnection** - строка подключения к БД. Пример: ```Server=localhost;Port=5432;Database=appstester;User Id=postgres;Password=root;```
* **Moodle__BasicToken** - токен для подключения к Moodle, генерируется в Moodle в разделе "Управление ключами"
* **Moodle_Url** - url для подключения к Moodle. Пример: ```http://localhost:6003```
* **Moodle__Token** - аналогичен **Moodle__BasicToken**
#### AppsTester/Checker/Android
* **ConnectionStrings__RabbitMq** - строка подключения к RabbitMQ. Пример: ```amqp://root:root@localhost:5672```
* **ConnectionStrings__DevicesSynchronizationRedis** - строка подключения к Redis для получения списка незарезервированных устройств.
* **Controller__Url** - строка подключения к **AppsTester/Controller**. Пример: ```http://localhost:5000```
* **Adb__Host** - хост ADB-сервера. Пример: ```localhost```
* **ANDROID_SDK_ROOT** - путь к Android SDK. Примеры: Linux - ```/home/ubuntu/android-sdk/```; Windows - ```C:\\Users\\PC\\AppData\\Local\\Android\\Sdk```

## Инструкция к запуску и подготовке всей системы
### Moodle
1. Установите и запустите moodle любой версии (https://download.moodle.org/windows/ для Windows)
3. Загрузите плагин для типа вопроса https://github.com/jobe-appstester/qtype_appstester и переименуйте папку qtype_appstester в appstester
4. Загрузите плагин для поведения вопроса https://github.com/jobe-appstester/qbehaviour_appstester и переименуйте папку qbehaviour_appstester в appstester
5. Поместите папку плагина поведения вопроса в ```...{MOODLE_PATH}}/question/behaviour```
6. Поместите папку плагина типа вопроса в ```...{MOODLE_PATH}}/question/type```
7. Перезапустите moodle и следуйте появившимся указаниям для установки плагинов
8. Разрешить протокол REST в разделе администратора->"Управление протоколами"
9. Сгенерировать токен для службы submissions в разделе администратора->"Управление ключами"
10. Создайте курс и тест, в нем добавьте вопрос с типом "Тест на реализацию приложения"
11. В настройках вопроса, в разделе "Параметры проверки" загрузите шаблонный ZIP-архив (спрашивайте у разработчиков)
### Appstester
1. Загрузите систему https://github.com/jobe-appstester/appstester_server
2. Настройте конфигурацию для Appstester.Controller и Appstester.Checker.Android
3. Подготовьте окружение для Appstester.Controller и Appstester.Checker.Android согласно конфигурации и запустите их