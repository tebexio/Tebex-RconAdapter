FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

RUN apt update && apt install -y git

RUN git clone https://github.com/tebexio/Tebex-RconAdapter
RUN cd Tebex-RconAdapter/Tebex-RCON && dotnet restore && dotnet publish -c Release -r linux-x64 --self-contained false -o /app

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS runtime
WORKDIR /app
COPY --from=build /app .
CMD ["./Tebex-RCON"]