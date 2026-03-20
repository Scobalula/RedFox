// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics;

namespace RedFox.IO.ProcessMemory
{
    /// <summary>
    /// Provides helpers for locating target processes by name.
    /// </summary>
    public static class ProcessFinder
    {
        /// <summary>
        /// Finds process identifiers for all running processes that match the provided process name.
        /// </summary>
        /// <param name="processName">The process name to locate. The optional .exe suffix is supported.</param>
        /// <returns>Matching process identifiers sorted by PID in ascending order.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        public static int[] FindProcessIdsByName(string processName)
        {
            Process[] processes = FindProcessesByName(processName);

            try
            {
                int[] processIds = new int[processes.Length];
                for (int index = 0; index < processes.Length; index++)
                {
                    processIds[index] = processes[index].Id;
                }

                return processIds;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        /// <summary>
        /// Finds running processes that match the provided process name.
        /// </summary>
        /// <param name="processName">The process name to locate. The optional .exe suffix is supported.</param>
        /// <returns>Matching processes sorted by PID in ascending order. Caller owns disposal.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        public static Process[] FindProcessesByName(string processName)
        {
            string normalizedName = NormalizeProcessName(processName);
            Process[] processes = Process.GetProcessesByName(normalizedName);
            Array.Sort(processes, static (left, right) => left.Id.CompareTo(right.Id));
            return processes;
        }

        internal static int GetSingleProcessIdByName(string processName)
        {
            int[] processIds = FindProcessIdsByName(processName);
            if (processIds.Length == 0)
            {
                throw new InvalidOperationException($"No process with name '{processName}' was found.");
            }

            if (processIds.Length > 1)
            {
                string allIds = string.Join(", ", processIds);
                throw new InvalidOperationException($"Multiple processes with name '{processName}' were found ({allIds}). Specify a PID instead.");
            }

            return processIds[0];
        }

        internal static ProcessModuleInfo[] GetProcessModules(int processId)
        {
            using Process process = Process.GetProcessById(processId);
            ProcessModuleCollection? modules = process.Modules;
            if (modules is null || modules.Count == 0)
            {
                return [];
            }

            ProcessModuleInfo[] result = new ProcessModuleInfo[modules.Count];
            for (int index = 0; index < modules.Count; index++)
            {
                ProcessModule module = modules[index]!;
                string moduleName = module.ModuleName;
                string? filePath = module.FileName;
                nint baseAddress = module.BaseAddress;
                int moduleMemorySize = module.ModuleMemorySize;
                result[index] = new ProcessModuleInfo(moduleName, filePath, baseAddress, moduleMemorySize);
            }

            return result;
        }

        internal static ProcessModuleInfo GetMainModule(int processId)
        {
            using Process process = Process.GetProcessById(processId);
            ProcessModule? mainModule =
                process.MainModule ?? throw new InvalidOperationException($"Process {processId} does not expose a main module.");

            return new ProcessModuleInfo(
                mainModule.ModuleName,
                mainModule.FileName,
                mainModule.BaseAddress,
                mainModule.ModuleMemorySize);
        }

        private static string NormalizeProcessName(string processName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(processName);

            string normalizedName = processName.Trim();
            if (normalizedName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalizedName = normalizedName[..^4];
            }

            if (normalizedName.Length == 0)
            {
                throw new ArgumentException("Process name cannot be empty.", nameof(processName));
            }

            return normalizedName;
        }
    }
}
