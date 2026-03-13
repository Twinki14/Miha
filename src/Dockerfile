FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ARG CUSTOM_VERSION=""
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR "/src"
COPY ["src/*.Props", "Miha/"]
COPY ["src/Miha/Miha.csproj", "Miha/"]
COPY ["src/Miha.Discord/Miha.Discord.csproj", "Miha.Discord/"]
COPY ["src/Miha.Logic/Miha.Logic.csproj", "Miha.Logic/"]
COPY ["src/Miha.Redis/Miha.Redis.csproj", "Miha.Redis/"]
COPY ["src/Miha.Shared/Miha.Shared.csproj", "Miha.Shared/"]
COPY . .
WORKDIR "/src/Miha/"
RUN dotnet restore "Miha.csproj"
RUN dotnet build "Miha.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Miha.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV MIHA_CUSTOM_VERSION=${CUSTOM_VERSION}
ENTRYPOINT ["dotnet", "Miha.dll"]
