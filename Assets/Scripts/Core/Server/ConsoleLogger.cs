using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;

public class ConsoleLogger : MonoBehaviour
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetStdHandle(int nStdHandle, IntPtr handle);

    const int STD_OUTPUT_HANDLE = -11;
    const int STD_ERROR_HANDLE = -12;

    void Awake()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        SetupConsole();
        Application.logMessageReceived += HandleLog;
#endif
    }

    void SetupConsole()
    {
        if (AllocConsole())
        {
            // Redirect stdout and stderr to the new console
            var stdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            var stdErr = GetStdHandle(STD_ERROR_HANDLE);
            var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
            Debug.Log("Console allocated and ready to receive logs.");
        }
        else
        {
            Debug.LogWarning("Failed to allocate console.");
        }
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        Console.WriteLine($"{type}: {logString}");
        if (type == LogType.Exception || type == LogType.Error)
        {
            Console.WriteLine(stackTrace);
        }
    }

    void OnApplicationQuit()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        FreeConsole();
#endif
    }
}
