#!/bin/bash
# Sync the Linux release to a remote server, to build a snapshot image of a basic RCON adapter

SNAPSHOT_SYSTEM="root@addr"
VERSION="1.0.0-beta"

# Sync distribution files to the server used for snapshots
rsync -rv ../../Tebex-RCON/bin/Release/net7.0/linux-x64 $SNAPSHOT_SYSTEM:/root/Tebex-RCONAdapter/
rsync -v ./Tebex-RCONAdapter.service $SNAPSHOT_SYSTEM:/etc/systemd/system/Tebex-RCONAdapter.service
rsync -v ./etc/motd $SNAPSHOT_SYSTEM:/etc/motd
rsync -rv ./root/* $SNAPSHOT_SYSTEM:/root/