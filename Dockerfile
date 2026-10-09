# Етап збірки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копіюємо абсолютно весь код з репозиторію в Docker (включаючи Domain, Application тощо)
COPY . .

# Переходимо в папку з головним API-проєктом
WORKDIR /src/TodoBot.Api

# Відновлюємо залежності та публікуємо
RUN dotnet restore "TodoBot.Api.csproj"
RUN dotnet publish "TodoBot.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Етап виконання (легкий образ для сервера Oracle)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TodoBot.Api.dll"]