#!/bin/bash

# Function to validate input is not empty
validate_input() {
    local input=$1
    local prompt_message=$2

    while [[ -z "$input" ]]; do
        read -p "$prompt_message" input
    done

    echo "$input"
}

# Function to validate that port is a number
validate_port() {
    local port=$1

    while ! [[ "$port" =~ ^[0-9]+$ ]]; do
        read -p "Please enter a valid Rcon Port (number only): " port
    done

    echo "$port"
}

# Ask for Secret Key
secret_key=$(validate_input "" "Enter Secret Key: ")

# Ask for Rcon IP Address
rcon_ip=$(validate_input "" "Enter RCON IP Address: ")

# Ask for Rcon Port
rcon_port=$(validate_input "" "Enter RCON Port: ")
rcon_port=$(validate_port "$rcon_port")

# Ask for Rcon Password
rcon_password=$(validate_input "" "Enter RCON Password: ")

mkdir -p /etc/systemd/system/Tebex-RCONAdapter.service.d/
echo "[Service]" > /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
echo "Environment=\"RCON_ADAPTER_GAME=$game\"" >> /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
echo "Environment=\"RCON_ADAPTER_KEY=$secret_key\"" >> /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
echo "Environment=\"RCON_ADAPTER_HOST=$rcon_ip\"" >> /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
echo "Environment=\"RCON_ADAPTER_PORT=$rcon_port\"" >> /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
echo "Environment=\"RCON_ADAPTER_PASSWORD=$rcon_password\"" >> /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
echo "Environment=\"RCON_ADAPTER_SERVICEMODE=true\"" >> /etc/systemd/system/Tebex-RCONAdapter.service.d/myenv.conf
systemctl daemon-reload

echo "Configuration updated successfully."
echo "  Run ./start.sh to run new configuration."