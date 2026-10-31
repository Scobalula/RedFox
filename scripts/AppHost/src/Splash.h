#pragma once

#include "Overlay.h"

#include <windows.h>
#include <string>

namespace Gdiplus
{
	class Bitmap;
}

class Splash
{
private:
	const Overlay&		overlay;
	HANDLE				thread		= nullptr;
	HANDLE				ready		= nullptr;
	HWND				window		= nullptr;
	Gdiplus::Bitmap*	image		= nullptr;
	float				scale		= 1.0f;
	int					width		= 0;
	int					height		= 0;
	SRWLOCK				lock		= SRWLOCK_INIT;
	std::wstring		status;
	double				progress	= -1.0;

public:
	Splash(const Overlay& overlay);
	~Splash();

	void SetStatus(const std::wstring& value);
	void SetProgress(double value);
	void Close();

private:
	static DWORD WINAPI ThreadMain(void* parameter);
	static LRESULT CALLBACK WindowProc(HWND handle, UINT message, WPARAM wParam, LPARAM lParam);
	void Run();
	void Render();
};
