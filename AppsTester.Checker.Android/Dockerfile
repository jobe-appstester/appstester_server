FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["AppsTester.Checker.Android/AppsTester.Checker.Android.csproj", "AppsTester.Checker.Android/"]
RUN dotnet restore "AppsTester.Checker.Android/AppsTester.Checker.Android.csproj"
COPY . .
WORKDIR "/src/AppsTester.Checker.Android"
RUN dotnet build "AppsTester.Checker.Android.csproj" -c Release -o /app/build
RUN dotnet publish "AppsTester.Checker.Android.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS final
RUN apt-get update && apt-get upgrade -y
ENV SDK_URL="https://dl.google.com/android/repository/commandlinetools-linux-6858069_latest.zip" \
    ANDROID_HOME="/usr/local/android-sdk"
RUN apt-get install -y curl wget unzip openjdk-11-jdk
RUN mkdir "$ANDROID_HOME" .android \
    && cd "$ANDROID_HOME" \
    && mkdir cmdline-tools \
    && cd cmdline-tools \
    && curl -o sdk.zip $SDK_URL \
    && unzip sdk.zip \
    && mv cmdline-tools tools \
    && yes | $ANDROID_HOME/cmdline-tools/tools/bin/sdkmanager --licenses
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "AppsTester.Checker.Android.dll"]