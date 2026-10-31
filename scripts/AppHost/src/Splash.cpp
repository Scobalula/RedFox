#include "Splash.h"

#include <algorithm>
#include <shellscalingapi.h>
#include <shlwapi.h>

namespace Gdiplus
{
	using std::min;
	using std::max;
}

#include <gdiplus.h>

static const wchar_t	SplashClassName[]	= L"RedFoxAppHostSplash";
static const UINT		RenderMessage		= WM_APP + 1;

static void AddOutline(Gdiplus::GraphicsPath& path, Gdiplus::RectF bounds, float radius)
{
	float diameter = radius * 2.0f;

	if (radius <= 0.0f)
	{
		path.AddRectangle(bounds);
		return;
	}

	path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180.0f, 90.0f);
	path.AddArc(bounds.X + bounds.Width - diameter, bounds.Y, diameter, diameter, 270.0f, 90.0f);
	path.AddArc(bounds.X + bounds.Width - diameter, bounds.Y + bounds.Height - diameter, diameter, diameter, 0.0f, 90.0f);
	path.AddArc(bounds.X, bounds.Y + bounds.Height - diameter, diameter, diameter, 90.0f, 90.0f);
	path.CloseFigure();
}

static void DrawLabel(HDC context, const std::wstring& text, HFONT font, uint32_t color, RECT area, UINT format)
{
	SelectObject(context, font);
	SetTextColor(context, RGB((color >> 16) & 0xFF, (color >> 8) & 0xFF, color & 0xFF));
	DrawTextW(context, text.c_str(), static_cast<int>(text.size()), &area, format | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS);
}

static BOOL CALLBACK FindAppWindow(HWND handle, LPARAM parameter)
{
	auto	result		= reinterpret_cast<HWND*>(parameter);
	DWORD	processId	= 0;

	GetWindowThreadProcessId(handle, &processId);

	if (handle == *result || processId != GetCurrentProcessId() || !IsWindowVisible(handle))
	{
		return TRUE;
	}

	*result = handle;
	return FALSE;
}

static void RestoreAlpha(void* bits, int width, const RECT& area, uint32_t alpha)
{
	auto pixels = static_cast<uint32_t*>(bits);

	for (LONG y = area.top; y < area.bottom; y++)
	{
		for (LONG x = area.left; x < area.right; x++)
		{
			pixels[y * width + x] = (pixels[y * width + x] & 0x00FFFFFF) | alpha;
		}
	}
}

Splash::Splash(const Overlay& overlay) : overlay(overlay)
{
	ready	= CreateEventW(nullptr, TRUE, FALSE, nullptr);
	thread	= CreateThread(nullptr, 0, ThreadMain, this, 0, nullptr);

	WaitForSingleObject(ready, INFINITE);
}

Splash::~Splash()
{
	Close();
	WaitForSingleObject(thread, INFINITE);
	CloseHandle(thread);
	CloseHandle(ready);
}

void Splash::SetStatus(const std::wstring& value)
{
	AcquireSRWLockExclusive(&lock);
	status = value;
	ReleaseSRWLockExclusive(&lock);

	if (window != nullptr)
	{
		PostMessageW(window, RenderMessage, 0, 0);
	}
}

void Splash::SetProgress(double value)
{
	AcquireSRWLockExclusive(&lock);
	progress = value;
	ReleaseSRWLockExclusive(&lock);

	if (window != nullptr)
	{
		PostMessageW(window, RenderMessage, 0, 0);
	}
}

void Splash::Close()
{
	if (window != nullptr)
	{
		PostMessageW(window, WM_CLOSE, 0, 0);
	}
}

DWORD WINAPI Splash::ThreadMain(void* parameter)
{
	static_cast<Splash*>(parameter)->Run();
	return 0;
}

LRESULT CALLBACK Splash::WindowProc(HWND handle, UINT message, WPARAM wParam, LPARAM lParam)
{
	switch (message)
	{
		case RenderMessage:
		{
			reinterpret_cast<Splash*>(GetWindowLongPtrW(handle, GWLP_USERDATA))->Render();
			return 0;
		}
		case WM_CLOSE:
		{
			HWND	appWindow			= handle;
			DWORD	foregroundProcessId	= 0;

			GetWindowThreadProcessId(GetForegroundWindow(), &foregroundProcessId);
			EnumWindows(FindAppWindow, reinterpret_cast<LPARAM>(&appWindow));

			if (appWindow != handle && foregroundProcessId != GetCurrentProcessId())
			{
				SetForegroundWindow(appWindow);
			}

			DestroyWindow(handle);
			return 0;
		}
		case WM_DESTROY:
		{
			PostQuitMessage(0);
			return 0;
		}
	}

	return DefWindowProcW(handle, message, wParam, lParam);
}

void Splash::Run()
{
	ULONG_PTR						token = 0;
	Gdiplus::GdiplusStartupInput	startup;

	SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
	Gdiplus::GdiplusStartup(&token, &startup, nullptr);

	if (overlay.SplashSize > 0)
	{
		IStream* stream	= SHCreateMemStream(overlay.Splash, overlay.SplashSize);
		image			= Gdiplus::Bitmap::FromStream(stream);
		stream->Release();

		if (image->GetLastStatus() != Gdiplus::Ok)
		{
			delete image;
			image = nullptr;
		}
	}

	POINT		cursor	= {};
	MONITORINFO	monitor	= { sizeof(MONITORINFO) };
	UINT		dpiX	= 96;
	UINT		dpiY	= 96;

	GetCursorPos(&cursor);
	HMONITOR handle = MonitorFromPoint(cursor, MONITOR_DEFAULTTOPRIMARY);
	GetMonitorInfoW(handle, &monitor);
	GetDpiForMonitor(handle, MDT_EFFECTIVE_DPI, &dpiX, &dpiY);

	int imageWidth	= image != nullptr ? static_cast<int>(image->GetWidth()) : 480;
	int imageHeight	= image != nullptr ? static_cast<int>(image->GetHeight()) : 0;

	scale	= dpiX / 96.0f;
	width	= static_cast<int>(imageWidth * scale);
	height	= static_cast<int>((imageHeight + 84) * scale);

	WNDCLASSEXW windowClass		= { sizeof(WNDCLASSEXW) };
	windowClass.lpfnWndProc		= WindowProc;
	windowClass.hInstance		= GetModuleHandleW(nullptr);
	windowClass.hCursor			= LoadCursorW(nullptr, IDC_APPSTARTING);
	windowClass.lpszClassName	= SplashClassName;
	RegisterClassExW(&windowClass);

	int x = monitor.rcWork.left + (monitor.rcWork.right - monitor.rcWork.left - width) / 2;
	int y = monitor.rcWork.top + (monitor.rcWork.bottom - monitor.rcWork.top - height) / 2;

	HWND created = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, SplashClassName, overlay.Name.c_str(), WS_POPUP, x, y, width, height, nullptr, nullptr, windowClass.hInstance, nullptr);

	if (created != nullptr)
	{
		window = created;
		SetWindowLongPtrW(created, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
		Render();
		ShowWindow(created, SW_SHOWNOACTIVATE);
	}

	SetEvent(ready);

	MSG message;
	while (created != nullptr && GetMessageW(&message, nullptr, 0, 0) > 0)
	{
		TranslateMessage(&message);
		DispatchMessageW(&message);
	}

	delete image;
	Gdiplus::GdiplusShutdown(token);
}

void Splash::Render()
{
	AcquireSRWLockShared(&lock);
	std::wstring	currentStatus		= status;
	double			currentProgress	= progress;
	ReleaseSRWLockShared(&lock);

	BITMAPINFO info					= {};
	info.bmiHeader.biSize			= sizeof(BITMAPINFOHEADER);
	info.bmiHeader.biWidth			= width;
	info.bmiHeader.biHeight			= -height;
	info.bmiHeader.biPlanes			= 1;
	info.bmiHeader.biBitCount		= 32;
	info.bmiHeader.biCompression	= BI_RGB;

	void*	bits	= nullptr;
	HDC		screen	= GetDC(nullptr);
	HDC		memory	= CreateCompatibleDC(screen);
	HBITMAP	bitmap	= CreateDIBSection(screen, &info, DIB_RGB_COLORS, &bits, nullptr, 0);

	int padding	= static_cast<int>(16.0f * scale);
	int bandTop	= height - static_cast<int>(84.0f * scale);

	{
		Gdiplus::Bitmap		surface(width, height, width * 4, PixelFormat32bppPARGB, static_cast<BYTE*>(bits));
		Gdiplus::Graphics	graphics(&surface);

		graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
		graphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
		graphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHalf);

		float					radius		= overlay.CornerRadius * scale;
		Gdiplus::GraphicsPath	outline;
		Gdiplus::GraphicsPath	edge;

		AddOutline(outline, Gdiplus::RectF(0.0f, 0.0f, static_cast<float>(width), static_cast<float>(height)), radius);
		AddOutline(edge, Gdiplus::RectF(0.5f, 0.5f, width - 1.0f, height - 1.0f), radius);

		Gdiplus::SolidBrush	background(Gdiplus::Color(overlay.BackgroundColor));
		Gdiplus::Pen		border(Gdiplus::Color(overlay.BorderColor), 1.0f);
		graphics.FillPath(&background, &outline);
		graphics.SetClip(&outline);

		if (image != nullptr)
		{
			graphics.DrawImage(image, 0.0f, 0.0f, static_cast<float>(width), static_cast<float>(bandTop));
		}

		if (currentProgress >= 0.0)
		{
			float				barLeft		= static_cast<float>(padding);
			float				barTop		= height - padding - 3.0f * scale;
			float				barWidth	= width - padding * 2.0f;
			Gdiplus::SolidBrush	track(Gdiplus::Color((overlay.SecondaryColor & 0x00FFFFFF) | 0x40000000));
			Gdiplus::SolidBrush	fill(Gdiplus::Color(overlay.AccentColor));

			graphics.FillRectangle(&track, barLeft, barTop, barWidth, 3.0f * scale);
			graphics.FillRectangle(&fill, barLeft, barTop, barWidth * static_cast<float>(std::clamp(currentProgress, 0.0, 1.0)), 3.0f * scale);
		}

		graphics.ResetClip();

		if ((overlay.BorderColor >> 24) != 0)
		{
			graphics.DrawPath(&border, &edge);
		}
	}

	RECT			titleArea	= { padding, bandTop + padding, width - padding, bandTop + padding + static_cast<int>(22.0f * scale) };
	RECT			statusArea	= { padding, titleArea.bottom + static_cast<int>(4.0f * scale), width - padding, titleArea.bottom + static_cast<int>(22.0f * scale) };
	HFONT			nameFont	= CreateFontW(-static_cast<int>(15.0f * scale), 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, overlay.FontFamily.c_str());
	HFONT			textFont	= CreateFontW(-static_cast<int>(12.0f * scale), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, overlay.FontFamily.c_str());
	HGDIOBJ			previous	= SelectObject(memory, bitmap);
	HGDIOBJ			font		= GetCurrentObject(memory, OBJ_FONT);
	POINT			source		= {};
	SIZE			size		= { width, height };
	BLENDFUNCTION	blend		= { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA };

	SetBkMode(memory, TRANSPARENT);
	DrawLabel(memory, overlay.Name, nameFont, overlay.ForegroundColor, titleArea, DT_LEFT | DT_VCENTER);
	DrawLabel(memory, overlay.Version, textFont, overlay.SecondaryColor, titleArea, DT_RIGHT | DT_VCENTER);
	DrawLabel(memory, currentStatus, textFont, overlay.SecondaryColor, statusArea, DT_LEFT | DT_VCENTER);
	GdiFlush();

	RestoreAlpha(bits, width, titleArea, overlay.BackgroundColor & 0xFF000000);
	RestoreAlpha(bits, width, statusArea, overlay.BackgroundColor & 0xFF000000);

	UpdateLayeredWindow(window, screen, nullptr, &size, memory, &source, 0, &blend, ULW_ALPHA);

	SelectObject(memory, font);
	SelectObject(memory, previous);
	DeleteObject(nameFont);
	DeleteObject(textFont);
	DeleteObject(bitmap);
	DeleteDC(memory);
	ReleaseDC(nullptr, screen);
}
