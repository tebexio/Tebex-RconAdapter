﻿using System;
using Tebex_RCON;
using Tebex.API;
using Tebex.RCON;

/*
var adapter = new TebexRconAdapter();
TebexApi.Instance.InitAdapter(adapter);
*/

var adapter = new TebexTelnetAdapter();
TebexApi.Instance.InitAdapter(adapter);

while (true)
{
    Console.Write("Tebex> ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input))
    {
        continue;
    }
    
    if (input == "exit")
    {
        return;
    }

    if (input == "clear" || input == "cls")
    {
        Console.Clear();
        continue;
    }
    
    /* TODO this worked for RCON only, support Telnet too
    if (input.Contains("tebex."))
    {
        adapter.HandleTebexCommand(input);
        continue;
    }

    if (adapter.Rcon != null) //Rcon passthrough
    {
        var response = adapter.Rcon.SendCommandAndReadResponse(2, input);
        Console.WriteLine(response);
        return;
    }*/
    
    // Null rcon
    Console.WriteLine("Unrecognized command. If you ran a server command, we are not connected to a game server.");
    Console.WriteLine("Please check your secret key and server connection.");
}