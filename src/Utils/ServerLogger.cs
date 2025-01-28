using System;

namespace MyPokemon.Utils
{
    public static class ServerLogger
    {
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss.fff");
            lock (_lock)
            {
                Console.WriteLine($"[{timeStamp}] {message}");
            }
        }

        public static void LogNetwork(string message)
        {
            Log($"[Network] {message}");
        }

        public static void LogPlayer(string message)
        {
            Log($"[Player] {message}");
        }

        public static void LogError(string message, Exception? e = null)
        {
            string errorMsg = e != null ? $"{message}: {e.Message}" : message;
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {errorMsg}");
                Console.ResetColor();
            }
        }
    }
} 