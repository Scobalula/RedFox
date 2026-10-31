#pragma once

#include "Splash.h"

#include <string>

bool RunApp(const std::wstring& hostPath, const std::wstring& appDirectory, const Overlay& overlay, Splash* splash, int argc, wchar_t** argv, int& exitCode, std::wstring& error, std::wstring& downloadUrl);
