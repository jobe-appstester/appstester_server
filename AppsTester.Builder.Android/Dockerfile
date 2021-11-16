# Т.к. основным инструментом для сборки Android-проектов является Gradle, 
# и по счастливому стечению обстоятельств есть официальный Docker-образ 
# мы решили за основу взять именно его с нужной нам версией Gradle
FROM gradle:6.8.3-jdk8

# Задаем переменные с локальной папкой для Android SDK и 
# версиями платформы и инструментария
ENV SDK_URL="https://dl.google.com/android/repository/commandlinetools-linux-6858069_latest.zip" \
    ANDROID_HOME="/usr/local/android-sdk" \
    ANDROID_VERSION=29 \
    ANDROID_BUILD_TOOLS_VERSION=29.0.3

# Создаем папку, скачиваем туда SDK и распаковываем архив,
# который после сборки удаляем
RUN mkdir "$ANDROID_HOME" .android \
    && cd "$ANDROID_HOME" \
    && mkdir cmdline-tools \
    && cd cmdline-tools \
    && curl -o sdk.zip $SDK_URL \
    && unzip sdk.zip \
    && mv cmdline-tools tools \
    && yes | $ANDROID_HOME/cmdline-tools/tools/bin/sdkmanager --licenses

# Запускаем обновление SDK и установку build-tools, platform-tools
RUN $ANDROID_HOME/cmdline-tools/tools/bin/sdkmanager --update
RUN $ANDROID_HOME/cmdline-tools/tools/bin/sdkmanager "build-tools;${ANDROID_BUILD_TOOLS_VERSION}" \
    "platforms;android-${ANDROID_VERSION}" \
    "platform-tools"