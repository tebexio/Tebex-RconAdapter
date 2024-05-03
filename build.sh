#!/bin/bash

rm -rf ./Tebex-RCON/bin
rm -rf ./Tebex-RCON/obj
rm -rf ./deploy/windows
rm -rf ./deploy/linux

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false .
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false .
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=false .

mkdir -p ./deploy/windows
mkdir -p ./deploy/linux
mkdir -p ./deploy/osx

cp ./Tebex-RCON/bin/Release/net7.0/win-x64/publish/* ./deploy/windows/
cp ./Tebex-RCON/bin/Release/net7.0/linux-x64/publish/* ./deploy/linux/
cp ./Tebex-RCON/bin/Release/net7.0/osx-x64/publish/* ./deploy/osx/