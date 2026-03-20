// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.IO.ProcessMemory.Internal
{
    internal static class ProcessModuleResolver
    {
        public static ProcessModuleInfo GetModuleByName(ProcessModuleInfo[] modules, string moduleName, int processId, ProcessModuleInfo? preferredModule)
        {
            ArgumentNullException.ThrowIfNull(modules);
            string normalizedModuleName = NormalizeModuleName(moduleName);
            string normalizedModuleNameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedModuleName);

            List<ProcessModuleInfo> matchingModules = [];

            foreach (ProcessModuleInfo module in modules)
            {
                if (!IsModuleMatch(module, normalizedModuleName, normalizedModuleNameWithoutExtension))
                {
                    continue;
                }

                bool hasSameBaseAddress = matchingModules.Exists(existingModule => existingModule.BaseAddress == module.BaseAddress);

                if (hasSameBaseAddress)
                {
                    continue;
                }

                matchingModules.Add(module);
            }

            if (matchingModules.Count == 0)
            {
                string message = $"No module named '{moduleName}' was found in process {processId}.";
                throw new InvalidOperationException(message);
            }

            if (matchingModules.Count == 1)
            {
                return matchingModules[0];
            }

            if (preferredModule.HasValue)
            {
                ProcessModuleInfo preferred = preferredModule.Value;
                ProcessModuleInfo? resolvedPreferred = matchingModules.Find(candidate => candidate.BaseAddress == preferred.BaseAddress);
                if (resolvedPreferred.HasValue)
                {
                    return resolvedPreferred.Value;
                }
            }

            string duplicateMessage = $"Multiple modules named '{moduleName}' were found in process {processId}.";
            throw new InvalidOperationException(duplicateMessage);
        }

        private static string NormalizeModuleName(string moduleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
            string trimmedModuleName = moduleName.Trim();
            return Path.GetFileName(trimmedModuleName);
        }

        private static bool IsModuleMatch(ProcessModuleInfo module, string normalizedModuleName, string normalizedModuleNameWithoutExtension)
        {
            string moduleFileName = module.FilePath is null ? module.Name : Path.GetFileName(module.FilePath);
            string moduleNameWithoutExtension = Path.GetFileNameWithoutExtension(module.Name);
            string moduleFileNameWithoutExtension = Path.GetFileNameWithoutExtension(moduleFileName);

            return string.Equals(module.Name, normalizedModuleName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(moduleFileName, normalizedModuleName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(moduleNameWithoutExtension, normalizedModuleNameWithoutExtension, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(moduleFileNameWithoutExtension, normalizedModuleNameWithoutExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
