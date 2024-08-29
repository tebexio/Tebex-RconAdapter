namespace Tebex.Util;

public static class Ansi
{
    // Yellow formats the provided text in yellow
    public static string Yellow(string text)
    {
        return $"\x1b[33m{text}\x1b[0m";
    }

    // Red formats the provided text in red
    public static string Red(string text)
    {
        return $"\x1b[31m{text}\x1b[0m";
    }

    // Green formats the provided text in green
    public static string Green(string text)
    {
        return $"\x1b[32m{text}\x1b[0m";
    }

    // Blue formats the provided text in blue
    public static string Blue(string text)
    {
        return $"\x1b[34m{text}\x1b[0m";
    }

    // Purple formats the provided text in purple
    public static string Purple(string text)
    {
        return $"\x1b[35m{text}\x1b[0m";
    }

    // White formats the provided text in white
    public static string White(string text)
    {
        return $"\x1b[37m{text}\x1b[0m";
    }

    // Bold formats the provided text in bold
    public static string Bold(string text)
    {
        return $"\x1b[1m{text}\x1b[0m";
    }

    // Clear returns the ASCII code to clear the screen
    public static string Clear()
    {
        return "\x1b[2J";
    }

    // ResetCursor returns the ANSI code to move the cursor to the top left corner
    public static string ResetCursor()
    {
        return "\x1b[1;1H";
    }
    
    // Underline formats the provided text with an underline
    public static string Underline(string text)
    {
        return $"\x1b[4m{text}\x1b[0m";
    }
}