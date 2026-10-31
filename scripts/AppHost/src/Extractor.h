#pragma once

#include "Overlay.h"

#include <functional>
#include <string>

bool PrepareAppDirectory(const std::wstring& hostPath, const Overlay& overlay, const std::function<void(double)>& progress, std::wstring& appDirectory, std::wstring& error);
