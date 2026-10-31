#pragma once

#include <cstdint>
#include <string>

enum class AppHostMode : uint32_t
{
	Packed		= 0,
	SplashOnly	= 1,
};

struct Overlay
{
	AppHostMode		Mode;
	std::wstring	Name;
	std::wstring	Version;
	std::wstring	Entry;
	std::wstring	FrameworkName;
	std::wstring	FrameworkVersion;
	const uint8_t*	Splash;
	uint32_t		SplashSize;
	std::wstring	FontFamily;
	uint32_t		BackgroundColor;
	uint32_t		ForegroundColor;
	uint32_t		SecondaryColor;
	uint32_t		AccentColor;
	uint32_t		BorderColor;
	uint32_t		CornerRadius;
	std::wstring	PayloadId;
	uint64_t		UncompressedSize;
	uint32_t		FileCount;
	const uint8_t*	Payload;
	uint64_t		PayloadSize;
};

bool ReadOverlay(const uint8_t* image, uint64_t imageSize, Overlay& overlay);
bool IsConsoleSubsystem(const uint8_t* image);
