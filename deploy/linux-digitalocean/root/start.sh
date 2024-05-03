#!/bin/bash

echo "Starting RCON Adapter via systemctl..."
systemctl start Tebex-RCONAdapter

echo "Waiting for connection..."
sleep 2

systemctl status Tebex-RCONAdapter