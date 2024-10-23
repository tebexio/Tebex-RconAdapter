#!/bin/bash
# Sync the Linux release to a remote server, to build a snapshot image of a basic RCON adapter

SNAPSHOT_SYSTEM="root@addr"
VERSION="1.1.1"

ssh $SNAPSHOT_SYSTEM -t 'mkdir -p /root/Tebex-RCONAdapter'

# Sync distribution files to the server used for snapshots
rsync -rv ../linux/ $SNAPSHOT_SYSTEM:/root/Tebex-RCONAdapter/linux-x64/
rsync -v ./Tebex-RCONAdapter.service $SNAPSHOT_SYSTEM:/etc/systemd/system/Tebex-RCONAdapter.service
rsync -v ./etc/motd $SNAPSHOT_SYSTEM:/etc/motd
rsync -rv ./root/* $SNAPSHOT_SYSTEM:/root/

# Run DO cleanup scripts

ssh $SNAPSHOT_SYSTEM -t 'wget -N https://raw.githubusercontent.com/digitalocean/marketplace-partners/refs/heads/master/scripts/90-cleanup.sh'
ssh $SNAPSHOT_SYSTEM -t 'wget -N https://raw.githubusercontent.com/digitalocean/marketplace-partners/refs/heads/master/scripts/99-img-check.sh'
ssh $SNAPSHOT_SYSTEM -t 'chmod +x *.sh;'