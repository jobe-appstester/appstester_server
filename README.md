
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
В рамках разработки параметры можно задавать через [user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
### AppTester.Checker.Android
* `Adb:ExecutablePath`: Полный путь к [adb серверу](https://developer.android.com/studio/command-line/adb). Если указан - будет запущен, если еще не был.

## Вспомогательные сервисы
Для работы в режиме разработки вспомогательные сервисы наряду с разрабатываемыми представлены в `docker-compose.yml`. При запуске они хранят данные под папкой `dev-data` в корне проекта.
### RabbitMQ
Если в системе не установлен RabbitMQ, для разработки можно поднять его экземпляр с пользователем `root:root` командой
```
docker compose up -d rabbitmq
```
После этого сам RabbitMQ будет доступен на порту `5672`, строка подключения будет `amqp://root:root@localhost:5672`

Web интерфейс будет доступен по адресу [http://localhost:15672](http://localhost:15672)

### Сервисы работы с метриками
Для работы с метриками используются [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/), [Prometheus](https://prometheus.io/) и [Grafana](https://grafana.com). Если они не установлены в системе, для разработки мх можно поднять командой
```
docker compose up -d otel-collector prometheus grafana
```
* OpenTelemetry Collector конфигурация представлена в `otel-collector-config.yaml`, он занимает несколько портов
    * `4317` для отправки на него метрик по gRPC со стороны сервисов. Строка подключения в конфигурации -  `"OtlpExporterEndpoint": "http://localhost:4317"`
    * `8888` по пути `/metrics` будут раздаваться метрики самого коллектора
    * `8889` по пути `/metrics` будут раздаваться метрики сервисов, которые прислали метрики
* Prometheus конфигурация представлена в `prometheus.yaml`, он занимает порт `9090`
* Grafana не имеет изначальной конфигурации, занимает порт `3000`

## Метрики
```mermaid
sequenceDiagram
    participant Controller
    participant Checker.Android
    participant otel_collector
    participant Prometheus
    participant Grafana
    link otel_collector: Docs @ https://opentelemetry.io/docs/collector/
    link Prometheus: Docs @ https://prometheus.io/
    link Grafana: Docs @ https://grafana.com
    loop отправка метрик
        Controller ->> otel_collector: gRPC
        Checker.Android ->> otel_collector: gRPC
    end
    Note right of Checker.Android: .NET шлет данные в формате OpenTelemetry
    loop стягивание метрик
      Prometheus ->> otel_collector: GET /metrics
    end
    loop обновление dashboard
      Grafana ->> Prometheus: 
    end
```
