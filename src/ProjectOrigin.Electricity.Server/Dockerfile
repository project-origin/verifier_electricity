FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0.303 AS build

WORKDIR /src

COPY ./ProjectOrigin.Electricity.Server ./ProjectOrigin.Electricity.Server
COPY ./Directory.Build.props ./Directory.Build.props
COPY ./Protos ./Protos

RUN dotnet restore ProjectOrigin.Electricity.Server
RUN dotnet build ProjectOrigin.Electricity.Server -c Release --no-restore -o /app/build
RUN dotnet publish ProjectOrigin.Electricity.Server -c Release -o /app/publish

# ------- production image -------
FROM mcr.microsoft.com/dotnet/aspnet:8.0.7 AS production

ENV USER=dotnetuser
RUN groupadd -r "$USER" && useradd -r -g "$USER" "$USER"

WORKDIR /app
COPY --chown=root:root --from=build /app/publish .
RUN chmod -R 655 .

USER $USER
EXPOSE 5000
ENTRYPOINT ["dotnet", "ProjectOrigin.Electricity.Server.dll"]
