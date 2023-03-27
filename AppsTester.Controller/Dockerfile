FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AppsTester.Controller/AppsTester.Controller.csproj", "AppsTester.Controller/"]
RUN dotnet restore "AppsTester.Controller/AppsTester.Controller.csproj"
COPY . .
WORKDIR "/src/AppsTester.Controller"
RUN dotnet build "AppsTester.Controller.csproj" -c Release -o /app/build
RUN dotnet publish "AppsTester.Controller.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "AppsTester.Controller.dll"]
