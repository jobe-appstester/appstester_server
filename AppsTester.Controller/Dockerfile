FROM sdmitrij/testt:6.0 AS build
WORKDIR /src
COPY ["AppsTester.Controller/AppsTester.Controller.csproj", "AppsTester.Controller/"]
RUN dotnet restore "AppsTester.Controller/AppsTester.Controller.csproj"
COPY . .
WORKDIR "/src/AppsTester.Controller"
RUN dotnet build "AppsTester.Controller.csproj" -c Release -o /app/build
RUN dotnet publish "AppsTester.Controller.csproj" -c Release -o /app/publish
RUN dotnet tool install --tool-path /tools dotnet-trace \
 && dotnet tool install --tool-path /tools dotnet-counters \
 && dotnet tool install --tool-path /tools dotnet-dump \
 && dotnet tool install --tool-path /tools dotnet-gcdump

FROM sdmitrij/aspx:6.0 AS final
WORKDIR /tools
COPY --from=build /tools .
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "AppsTester.Controller.dll"]
