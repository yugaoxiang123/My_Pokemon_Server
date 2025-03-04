using UnityEngine;
using System;

public static class GameLogger
{
    public static void Log(string message)
    {
        string timeStamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Debug.Log($"[{timeStamp}] {message}");
    }

    public static void LogNetwork(string message)
    {
        Log($"[Network] {message}");
    }

    public static void LogPlayer(string message)
    {
        Log($"[Player] {message}");
    }

    public static void LogError(string message, Exception e = null)
    {
        string errorMsg = e != null ? $"{message}: {e.Message}" : message;
        Debug.LogError($"[{DateTime.Now:HH:mm:ss.fff}] {errorMsg}");
    }

    public static void LogAuth(string message)
    {
        Log($"[Auth] {message}");
    }
} 