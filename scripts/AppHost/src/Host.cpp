#include "Host.h"

#include <windows.h>
#include <nethost.h>
#include <hostfxr.h>
#include <filesystem>
#include <vector>

namespace fs = std::filesystem;

static const int HostApiBufferTooSmall		= static_cast<int>(0x80008098);
static const int FrameworkMissingFailure	= static_cast<int>(0x80008096);

static Splash*		activeSplash = nullptr;
static std::wstring	hostErrors;

static void HOSTFXR_CALLTYPE CaptureError(const char_t* message)
{
	hostErrors += message;
	hostErrors += L'\n';
}

static void SplashStatus(const wchar_t* status)
{
	if (activeSplash != nullptr)
	{
		activeSplash->SetStatus(status);
	}
}

static void SplashClose()
{
	if (activeSplash != nullptr)
	{
		activeSplash->Close();
	}
}

static std::wstring GetFrameworkDisplayName(const std::wstring& name)
{
	if (name == L"Microsoft.WindowsDesktop.App")
	{
		return L".NET Desktop Runtime";
	}

	if (name == L"Microsoft.AspNetCore.App")
	{
		return L"ASP.NET Core Runtime";
	}

	return L".NET Runtime";
}

static void DescribeMissingRuntime(const Overlay& overlay, std::wstring& error, std::wstring& downloadUrl)
{
	if (overlay.FrameworkName.empty())
	{
		error		= overlay.Name + L" requires the .NET Runtime (x64), which could not be found.";
		downloadUrl	= L"https://aka.ms/dotnet-download";
		return;
	}

	std::wstring version = overlay.FrameworkVersion.substr(0, overlay.FrameworkVersion.rfind(L'.'));

	error		= overlay.Name + L" requires the " + GetFrameworkDisplayName(overlay.FrameworkName) + L" " + version + L" (x64), which is not installed.";
	downloadUrl	= L"https://aka.ms/dotnet-core-applaunch?framework=" + overlay.FrameworkName + L"&framework_version=" + overlay.FrameworkVersion + L"&arch=x64&rid=win-x64&os=win10";
}

template <typename T>
static T GetExport(HMODULE module, const char* name)
{
	return reinterpret_cast<T>(GetProcAddress(module, name));
}

bool RunApp(const std::wstring& hostPath, const std::wstring& appDirectory, const Overlay& overlay, Splash* splash, int argc, wchar_t** argv, int& exitCode, std::wstring& error, std::wstring& downloadUrl)
{
	auto					entryPath	= (fs::path(appDirectory) / overlay.Entry).wstring();
	get_hostfxr_parameters	parameters	= { sizeof(get_hostfxr_parameters), entryPath.c_str(), nullptr };
	std::wstring			hostfxrPath(MAX_PATH, L'\0');
	size_t					size		= hostfxrPath.size();

	exitCode = get_hostfxr_path(hostfxrPath.data(), &size, &parameters);

	if (exitCode == HostApiBufferTooSmall)
	{
		hostfxrPath.resize(size);
		exitCode = get_hostfxr_path(hostfxrPath.data(), &size, &parameters);
	}

	if (exitCode != 0)
	{
		DescribeMissingRuntime(overlay, error, downloadUrl);
		return false;
	}

	hostfxrPath.resize(wcslen(hostfxrPath.c_str()));

	HMODULE	module			= LoadLibraryExW(hostfxrPath.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
	auto	setErrorWriter	= GetExport<hostfxr_set_error_writer_fn>(module, "hostfxr_set_error_writer");
	auto	initialize		= GetExport<hostfxr_initialize_for_dotnet_command_line_fn>(module, "hostfxr_initialize_for_dotnet_command_line");
	auto	setProperty		= GetExport<hostfxr_set_runtime_property_value_fn>(module, "hostfxr_set_runtime_property_value");
	auto	run				= GetExport<hostfxr_run_app_fn>(module, "hostfxr_run_app");
	auto	close			= GetExport<hostfxr_close_fn>(module, "hostfxr_close");

	if (setErrorWriter == nullptr || initialize == nullptr || setProperty == nullptr || run == nullptr || close == nullptr)
	{
		exitCode	= 1;
		error		= L"Failed to load " + hostfxrPath + L".";
		return false;
	}

	std::error_code				code;
	bool						selfContained	= fs::equivalent(fs::path(hostfxrPath).parent_path(), appDirectory, code);
	std::vector<const char_t*>	arguments		= { entryPath.c_str() };

	for (int i = 1; i < argc; i++)
	{
		arguments.push_back(argv[i]);
	}

	hostfxr_initialize_parameters	initializeParameters	= { sizeof(hostfxr_initialize_parameters), hostPath.c_str(), selfContained ? appDirectory.c_str() : nullptr };
	hostfxr_handle					handle					= nullptr;

	setErrorWriter(CaptureError);
	exitCode = initialize(static_cast<int>(arguments.size()), arguments.data(), &initializeParameters, &handle);

	if (exitCode == FrameworkMissingFailure)
	{
		DescribeMissingRuntime(overlay, error, downloadUrl);
		return false;
	}

	if (exitCode < 0 || handle == nullptr)
	{
		error = hostErrors.empty() ? L"Failed to initialize the .NET runtime." : hostErrors;
		return false;
	}

	auto statusPointer	= std::to_wstring(reinterpret_cast<uintptr_t>(&SplashStatus));
	auto closePointer	= std::to_wstring(reinterpret_cast<uintptr_t>(&SplashClose));

	activeSplash = splash;
	setProperty(handle, L"REDFOX_APPHOST_SPLASH_STATUS", statusPointer.c_str());
	setProperty(handle, L"REDFOX_APPHOST_SPLASH_CLOSE", closePointer.c_str());

	exitCode = run(handle);
	close(handle);

	activeSplash	= nullptr;
	error			= hostErrors;
	return hostErrors.empty();
}
