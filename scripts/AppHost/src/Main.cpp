#include "Extractor.h"
#include "Host.h"
#include "Overlay.h"
#include "Splash.h"

#include <windows.h>
#include <shellapi.h>
#include <cstdlib>
#include <filesystem>
#include <memory>

static std::wstring GetExecutablePath()
{
	std::wstring	path(MAX_PATH, L'\0');
	DWORD			length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));

	while (length == path.size())
	{
		path.resize(path.size() * 2);
		length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
	}

	path.resize(length);
	return path;
}

static bool WriteConsoleText(const std::wstring& text)
{
	DWORD written = 0;
	return WriteConsoleW(GetStdHandle(STD_ERROR_HANDLE), text.c_str(), static_cast<DWORD>(text.size()), &written, nullptr) != FALSE;
}

static void ReportError(bool console, const std::wstring& title, const std::wstring& message)
{
	if (!console)
	{
		MessageBoxW(nullptr, message.c_str(), title.c_str(), MB_OK | MB_ICONERROR);
		return;
	}

	if (WriteConsoleText(message + L"\n"))
	{
		return;
	}

	DWORD		written	= 0;
	int			length	= WideCharToMultiByte(CP_UTF8, 0, message.c_str(), static_cast<int>(message.size()), nullptr, 0, nullptr, nullptr);
	std::string	text(length, '\0');

	WideCharToMultiByte(CP_UTF8, 0, message.c_str(), static_cast<int>(message.size()), text.data(), length, nullptr, nullptr);
	text += '\n';
	WriteFile(GetStdHandle(STD_ERROR_HANDLE), text.data(), static_cast<DWORD>(text.size()), &written, nullptr);
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
	std::wstring	hostPath	= GetExecutablePath();
	HANDLE			file		= CreateFileW(hostPath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
	HANDLE			mapping		= CreateFileMappingW(file, nullptr, PAGE_READONLY, 0, 0, nullptr);
	auto			image		= static_cast<const uint8_t*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, 0));
	LARGE_INTEGER	size		= {};
	Overlay			overlay		= {};
	std::wstring	title		= std::filesystem::path(hostPath).stem().wstring();

	if (image == nullptr || !GetFileSizeEx(file, &size))
	{
		MessageBoxW(nullptr, L"Failed to read the executable.", title.c_str(), MB_OK | MB_ICONERROR);
		return 1;
	}

	bool console = IsConsoleSubsystem(image);

	if (!ReadOverlay(image, static_cast<uint64_t>(size.QuadPart), overlay))
	{
		ReportError(console, title, L"This executable has no application attached. Run the RedFox AppHost packer on it.");
		return 1;
	}

	std::unique_ptr<Splash> splash;

	if (!console)
	{
		splash = std::make_unique<Splash>(overlay);
		splash->SetStatus(L"Starting...");
	}

	std::wstring	appDirectory;
	std::wstring	error;
	std::wstring	downloadUrl;
	int				exitCode	= 1;
	int				lastPercent	= -1;
	bool			prepared	= true;

	if (overlay.Mode == AppHostMode::SplashOnly)
	{
		appDirectory = std::filesystem::path(hostPath).parent_path().wstring();
	}
	else
	{
		prepared = PrepareAppDirectory(hostPath, overlay, [&](double progress)
		{
			int percent = static_cast<int>(progress * 100.0);

			if (splash != nullptr)
			{
				splash->SetStatus(L"Unpacking files...");
				splash->SetProgress(progress);
			}
			else if (percent != lastPercent && WriteConsoleText(L"\rUnpacking files... " + std::to_wstring(percent) + L"%"))
			{
				lastPercent = percent;
			}
		}, appDirectory, error);

		if (lastPercent >= 0)
		{
			WriteConsoleText(L"\n");
		}
	}

	if (splash != nullptr)
	{
		splash->SetProgress(-1.0);
		splash->SetStatus(L"Starting " + overlay.Name + L"...");
	}

	bool succeeded = prepared && RunApp(hostPath, appDirectory, overlay, splash.get(), __argc, __wargv, exitCode, error, downloadUrl);
	splash.reset();

	if (!succeeded && !downloadUrl.empty() && !console)
	{
		std::wstring prompt = error + L"\n\nWould you like to download it now?";

		if (MessageBoxW(nullptr, prompt.c_str(), overlay.Name.c_str(), MB_YESNO | MB_ICONERROR) == IDYES)
		{
			CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
			ShellExecuteW(nullptr, L"open", downloadUrl.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
		}
	}
	else if (!succeeded && !downloadUrl.empty())
	{
		ReportError(console, overlay.Name, error + L"\n\nDownload it from " + downloadUrl);
	}
	else if (!succeeded)
	{
		ReportError(console, overlay.Name, error);
	}

	return exitCode;
}
