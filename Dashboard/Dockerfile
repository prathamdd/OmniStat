FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Dashboard.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 5116
ENV ASPNETCORE_URLS=http://0.0.0.0:5116

ENTRYPOINT ["dotnet", "Dashboard.dll"]
