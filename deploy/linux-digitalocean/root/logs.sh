#!/bin/bash

latestLog=$(ls -t Tebex-RCONAdapter/linux-x64/*.log | head -n 1)
tail -f $latestLog