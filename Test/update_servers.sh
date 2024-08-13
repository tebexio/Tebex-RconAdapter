#!/bin/bash

BLUE='\033[1;34m'
RESET='\033[0m'

info() {
    echo -e "${BLUE}$*${RESET}"
}

# Install SteamCMD if not present
if [ ! -d "./SteamCMD" ]; then
  info "Installing SteamCMD..."
  mkdir ./SteamCMD && cd ./SteamCMD
  curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_osx.tar.gz" | tar zxvf -
  chmod +x steamcmd.sh
  cd ..
fi

info "Installing/updating Rust..."
./SteamCMD/steamcmd.sh  +@sSteamCmdForcePlatformType linux +force_install_dir "$(pwd)/rust-dedicated" +login anonymous +app_update 258550 validate +quit
info "Installing/updating ARK:SE..."
./SteamCMD/steamcmd.sh  +@sSteamCmdForcePlatformType linux +force_install_dir "$(pwd)/ark-se-dedicated" +login anonymous +app_update 376030 validate +quit

info "Servers updated successfully."