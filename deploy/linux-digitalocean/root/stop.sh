#!/bin/bash

systemctl stop Tebex-RCONAdapter
sleep 1
systemctl status Tebex-RCONAdapter

echo "Stopped RCON Adapter."