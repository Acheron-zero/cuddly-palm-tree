﻿﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using unlockfps_nc.Model;
using unlockfps_nc.Utility;

namespace unlockfps_nc.Service
{
    public class ProcessService
    {
        private static Native.WinEventProc _eventCallback;
        private static uint[] PriorityClass =
        {
            0x00000100,
            0x00000080,
            0x00008000,
            0x00000020,
            0x00004000,
            0x00000040
        };

        private CancellationTokenSource _cts = new();
        private readonly IntPtr _winEventHook;
        private GCHandle _pinnedCallback;
        private IntPtr _gameHandle = IntPtr.Zero;
        private IntPtr _remoteUnityPlayer = IntPtr.Zero;
        private IntPtr _remoteUserAssembly = IntPtr.Zero;
        private int _gamePid = 0;
        private bool _gameInForeground = true;
        private bool _failover = false;

        private IntPtr _pFpsValue = IntPtr.Zero;

        private readonly ConfigService _configService;
        private readonly Config _config;

        private readonly IpcService _ipcService;

        public ProcessService(ConfigService configService, IpcService ipcService)
        {
            _configService = configService;
            _config = _configService.Config;

            _eventCallback = WinEventProc;
            _pinnedCallback = GCHandle.Alloc(_eventCallback, GCHandleType.Normal);
            _winEventHook = Native.SetWinEventHook(
                3, // EVENT_SYSTEM_FOREGROUND
                3, // EVENT_SYSTEM_FOREGROUND
                IntPtr.Zero,
                _eventCallback,
                0,
                0,
                0 // WINEVENT_OUTOFCONTEXT
                );
            _ipcService = ipcService;
        }

        public bool Start()
        {
            if (!File.Exists(_config.GamePath))
            {
                MessageBox.Show(@"Game path is invalid.", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (IsGameRunning())
            {
                MessageBox.Show(@"An instance of the game is already running.", @"Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            if (_gameHandle != IntPtr.Zero)
            {
                Native.CloseHandle(_gameHandle);
                _gameHandle = IntPtr.Zero;
            }

            _failover = false;
            _ipcService.Stop();

            _cts = new();
            Process.GetProcesses()
                .ToList()
                .Where(x => x.ProcessName is "GenshinImpact" or "YuanShen")
                .ToList()
                .ForEach(x => x.Kill());

            Task.Run(Worker, _cts.Token);
            return true;
        }

        public void OnFormClosing()
        {
            _ipcService.Stop();
            _cts.Cancel();
            _pinnedCallback.Free();
            Native.UnhookWinEvent(_winEventHook);
            Native.CloseHandle(_gameHandle);
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != 3)
                return;

            if (_gameHandle == IntPtr.Zero)
                return;

            Native.GetWindowThreadProcessId(hWnd, out var pid);
            _gameInForeground = pid == _gamePid;

            if (_config.Affinity != 0)
            {
                Process proc = Process.GetProcessById((int)pid);
                long affinityMask = _config.Affinity;
                proc.ProcessorAffinity = (IntPtr)affinityMask;
            }

            if (!_config.UsePowerSave)
                return;

            uint targetPriority = _gameInForeground ? PriorityClass[_config.Priority] : 0x00000040;
            Native.SetPriorityClass(_gameHandle, targetPriority);
        }

        private bool IsGameRunning()
        {
            if (_gameHandle == IntPtr.Zero)
                return false;

            if (!Native.GetExitCodeProcess(_gameHandle, out var exitCode))
                return false;

            return exitCode == 259;
        }

        private async Task Worker()
        {
            STARTUPINFO si = new();
            uint creationFlag = _config.SuspendLoad ? 4u : 0u;
            var gameFolder = Path.GetDirectoryName(_config.GamePath);

            if (_config.UseHDR)
            {
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\miHoYo\\原神", "WINDOWS_HDR_ON_h3132281285", 1);
            }

            if (!Native.CreateProcess(_config.GamePath, BuildCommandLine(), IntPtr.Zero, IntPtr.Zero, false, creationFlag, IntPtr.Zero, gameFolder, ref si, out var pi))
            {
                MessageBox.Show(
                    $@"CreateProcess failed ({Marshal.GetLastWin32Error()}){Environment.NewLine} {Marshal.GetLastPInvokeErrorMessage()}",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ProcessUtils.InjectDlls(pi.hProcess, _config.DllList))
            {
                MessageBox.Show(
                    $@"Dll Injection failed ({Marshal.GetLastWin32Error()}){Environment.NewLine} {Marshal.GetLastPInvokeErrorMessage()}",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (_config.SuspendLoad)
                Native.ResumeThread(pi.hThread);

            _gamePid = pi.dwProcessId;
            _gameHandle = pi.hProcess;

            Native.CloseHandle(pi.hThread);

            SpinWait.SpinUntil(() => ProcessUtils.GetWindowFromProcessId(_gamePid) != IntPtr.Zero);

            if (!SetupData())
                return;

            while (IsGameRunning() && !_cts.Token.IsCancellationRequested)
            {
                ApplyFpsLimit();
                await Task.Delay(1000, _cts.Token);
            }

            if (!IsGameRunning())
            {
                _ipcService.Stop();
                _pFpsValue = IntPtr.Zero;
                _gameHandle = IntPtr.Zero;

                if (_config.AutoClose)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        Application.Exit();
                    });
                }

            }
        }

        private void ApplyFpsLimit()
        {
            if (_pFpsValue == IntPtr.Zero)
                return;

            int fpsTarget = _gameInForeground ? _config.FPSTarget : _config.UsePowerSave ? 10 : _config.FPSTarget;

            if (!_failover)
            {
                var toWrite = BitConverter.GetBytes(fpsTarget);
                if (!Native.WriteProcessMemory(_gameHandle, _pFpsValue, toWrite, 4, out _) && IsGameRunning())
                {
                    // make sure we are actually failing to write (game is running and we are getting access denied)
                    if (Marshal.GetLastWin32Error() == 5)
                    {
                        _ipcService.Start(_gamePid, _pFpsValue);
                        _failover = true;
                    }
                }
            }
            else
            {
                _ipcService.ApplyFpsLimit(fpsTarget);
            }
        }

        private string BuildCommandLine()
        {
            string commandLine = $"{_config.GamePath} ";
            if (_config.PopupWindow)
                commandLine += "-popupwindow ";

            if (_config.UseCustomRes)
                commandLine += $"-screen-width {_config.CustomResX} -screen-height {_config.CustomResY} ";

            commandLine += $"-screen-fullscreen {(_config.Fullscreen ? 1 : 0)} ";
            if (_config.Fullscreen)
                commandLine += $"-window-mode {(_config.IsExclusiveFullscreen ? "exclusive" : "borderless")} ";

            if (_config.UseMobileUI)
                commandLine += "use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ";

            commandLine += $"-monitor {_config.MonitorNum} ";
            commandLine += $"{_config.AdditionalCommandLine} ";
            return commandLine;
        }

        private unsafe bool SetupData()
        {
            var gameDir = Path.GetDirectoryName(_config.GamePath);
            var gameName = Path.GetFileNameWithoutExtension(_config.GamePath);
            var dataDir = Path.Combine(gameDir, $"{gameName}_Data");

            var unityPlayerPath = Path.Combine(gameDir, "UnityPlayer.dll");
            var userAssemblyPath = Path.Combine(dataDir, "Native", "UserAssembly.dll");

            using ModuleGuard pUnityPlayer = Native.LoadLibraryEx(unityPlayerPath, IntPtr.Zero, 32);
            using ModuleGuard pUserAssembly = Native.LoadLibraryEx(userAssemblyPath, IntPtr.Zero, 32);

            if (!pUnityPlayer || !pUserAssembly)
            {
                if (!File.Exists(unityPlayerPath) && !File.Exists(userAssemblyPath))
                {
                    if (SetupDataEx())
                        return true;
                    goto BAD_PATTERN;
                }

                MessageBox.Show(
                    @"Failed to load UnityPlayer.dll or UserAssembly.dll",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!UpdateRemoteModules())
                return false;

            var dosHeader = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(pUnityPlayer);
            var ntHeader = Marshal.PtrToStructure<IMAGE_NT_HEADERS>((IntPtr)(pUnityPlayer.BaseAddress.ToInt64() + dosHeader.e_lfanew));

            if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 3.7
            {
                byte* address = (byte*)ProcessUtils.PatternScan(pUnityPlayer, "7F 0F 8B 05 ? ? ? ?");
                if (address == null)
                    goto BAD_PATTERN;

                byte* rip = address + 2;
                int rel = *(int*)(rip + 2);
                var localVa = rip + rel + 6;
                var rva = localVa - pUnityPlayer.BaseAddress.ToInt64();
                _pFpsValue = (IntPtr)(pUnityPlayer.BaseAddress.ToInt64() + rva);
            }
            else
            {
                byte* rip = null;
                if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 4.3
                {
                    byte* address = (byte*)ProcessUtils.PatternScan(pUserAssembly, "E8 ? ? ? ? 85 C0 7E 07 E8 ? ? ? ? EB 05");
                    if (address == null)
                        goto BAD_PATTERN;

                    rip = address;
                    rip += *(int*)(rip + 1) + 5;
                    rip += *(int*)(rip + 3) + 7;
                }
                else
                {
                    byte* address = (byte*)ProcessUtils.PatternScan(pUserAssembly, "B9 3C 00 00 00 FF 15");
                    if (address == null)
                        goto BAD_PATTERN;

                    rip = address;
                    rip += 5;
                    rip += *(int*)(rip + 2) + 6;
                }

                byte* remoteVa = rip - pUserAssembly.BaseAddress.ToInt64() + _remoteUserAssembly.ToInt64();
                byte* dataPtr = null;

                while (dataPtr == null)
                {
                    byte[] readResult = new byte[8];
                    Native.ReadProcessMemory(_gameHandle, (IntPtr)remoteVa, readResult, readResult.Length, out _);

                    ulong value = BitConverter.ToUInt64(readResult, 0);
                    dataPtr = (byte*)value;
                }

                byte* localVa = dataPtr - _remoteUnityPlayer.ToInt64() + pUnityPlayer.BaseAddress.ToInt64();
                while (localVa[0] == 0xE8 || localVa[0] == 0xE9)
                    localVa += *(int*)(localVa + 1) + 5;

                localVa += *(int*)(localVa + 2) + 6;
                var rva = localVa - pUnityPlayer.BaseAddress.ToInt64();
                _pFpsValue = (IntPtr)(_remoteUnityPlayer.ToInt64() + rva);
            }

            return true;

            BAD_PATTERN:
            MessageBox.Show(
                @"outdated fps pattern",
                @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private unsafe bool SetupDataEx()
        {
            var gameName = Path.GetFileNameWithoutExtension(_config.GamePath);
            var remoteExe = ProcessUtils.GetModuleBase(_gameHandle, $"{gameName}.exe");
            if (remoteExe == IntPtr.Zero)
                return false;

            using ModuleGuard pGenshinImpact = Native.LoadLibraryEx(_config.GamePath, IntPtr.Zero, 32);
            if (!pGenshinImpact)
                return false;

            var vaResults = ProcessUtils.PatternScanAllOccurrences(pGenshinImpact, "B9 3C 00 00 00 E8");
            if (vaResults.Count == 0)
                return false;

            var localVa = (byte*)vaResults
                .Select(x => x + 5)
                .Select(x => x + *(int*)(x + 1) + 5)
                .FirstOrDefault(x => *(byte*)x == 0xE9);

            if (localVa == null)
                return false;

            while (localVa[0] == 0xE8 || localVa[0] == 0xE9)
                localVa += *(int*)(localVa + 1) + 5;

            localVa += *(int*)(localVa + 2) + 6;
            var rva = localVa - pGenshinImpact.BaseAddress.ToInt64();
            _pFpsValue = (IntPtr)(remoteExe + rva);

            return true;
        }

        private bool UpdateRemoteModules()
        {
            int retries = 0;

            while (true)
            {
                _remoteUnityPlayer = ProcessUtils.GetModuleBase(_gameHandle, "UnityPlayer.dll");
                _remoteUserAssembly = ProcessUtils.GetModuleBase(_gameHandle, "UserAssembly.dll");

                if (_remoteUnityPlayer != IntPtr.Zero && _remoteUserAssembly != IntPtr.Zero)
                    break;

                if (retries > 10)
                    break;

                Task.Delay(2000, _cts.Token).Wait();
                retries++;
            }

            if (_remoteUnityPlayer == IntPtr.Zero || _remoteUserAssembly == IntPtr.Zero)
            {
                MessageBox.Show(
                    @"Failed to get remote module base address",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

    }
}
