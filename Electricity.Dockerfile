ARG PROJECT=ProjectOrigin.Electricity

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.303 AS build
ARG PROJECT

WORKDIR /builddir

COPY ./Directory.Build.props ./Directory.Build.props
COPY ./protos ./protos
COPY ./src ./src

RUN dotnet restore ./src/${PROJECT}
RUN dotnet build ./src/${PROJECT} -c Release --no-restore -p:CustomAssemblyName=App
RUN dotnet publish ./src/${PROJECT} -c Release --no-build -p:CustomAssemblyName=App -o /app/publish

# ------- production image -------
FROM mcr.microsoft.com/dotnet/aspnet:9.0.7-noble-chiseled-extra AS production

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "App.dll"]
