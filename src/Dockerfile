FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ARG CUSTOM_VERSION=""
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY . .
WORKDIR /src
RUN dotnet restore "Miha/Miha.csproj"
RUN dotnet build "Miha/Miha.csproj" -c Release --no-restore
RUN dotnet publish "Miha/Miha.csproj" -c Release -o /app/publish --no-build

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV MIHA_CUSTOM_VERSION=${CUSTOM_VERSION}
ENTRYPOINT ["dotnet", "Miha.dll"]
