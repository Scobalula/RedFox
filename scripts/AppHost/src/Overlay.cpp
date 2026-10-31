#include "Overlay.h"

#include <windows.h>
#include <algorithm>
#include <cstring>

static const char OverlayMagic[8] = { 'R', 'F', 'X', 'A', 'P', 'P', '0', '1' };

class OverlayReader
{
private:
	const uint8_t* position;
	const uint8_t* end;

public:
	OverlayReader(const uint8_t* data, uint64_t size) : position(data), end(data + size)
	{
	}

	bool Read(void* destination, uint64_t size)
	{
		if (static_cast<uint64_t>(end - position) < size)
		{
			return false;
		}

		memcpy(destination, position, size);
		position += size;
		return true;
	}

	bool Skip(const uint8_t*& start, uint64_t size)
	{
		if (static_cast<uint64_t>(end - position) < size)
		{
			return false;
		}

		start = position;
		position += size;
		return true;
	}

	bool ReadString(std::wstring& value)
	{
		uint16_t length = 0;

		if (!Read(&length, sizeof(length)))
		{
			return false;
		}

		value.resize(length);
		return Read(value.data(), length * sizeof(wchar_t));
	}
};

static const IMAGE_NT_HEADERS64* GetNtHeaders(const uint8_t* image)
{
	auto dosHeader = reinterpret_cast<const IMAGE_DOS_HEADER*>(image);
	return reinterpret_cast<const IMAGE_NT_HEADERS64*>(image + dosHeader->e_lfanew);
}

bool ReadOverlay(const uint8_t* image, uint64_t imageSize, Overlay& overlay)
{
	auto ntHeaders	= GetNtHeaders(image);
	auto sections	= IMAGE_FIRST_SECTION(ntHeaders);
	uint64_t start	= 0;

	for (WORD i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++)
	{
		start = std::max<uint64_t>(start, static_cast<uint64_t>(sections[i].PointerToRawData) + sections[i].SizeOfRawData);
	}

	if (start >= imageSize)
	{
		return false;
	}

	OverlayReader reader(image + start, imageSize - start);
	char magic[sizeof(OverlayMagic)];

	if (!reader.Read(magic, sizeof(magic)) || memcmp(magic, OverlayMagic, sizeof(magic)) != 0)
	{
		return false;
	}

	return reader.Read(&overlay.Mode, sizeof(overlay.Mode))
		&& reader.ReadString(overlay.Name)
		&& reader.ReadString(overlay.Version)
		&& reader.ReadString(overlay.Entry)
		&& reader.ReadString(overlay.FrameworkName)
		&& reader.ReadString(overlay.FrameworkVersion)
		&& reader.Read(&overlay.SplashSize, sizeof(overlay.SplashSize))
		&& reader.Skip(overlay.Splash, overlay.SplashSize)
		&& reader.ReadString(overlay.FontFamily)
		&& reader.Read(&overlay.BackgroundColor, sizeof(overlay.BackgroundColor))
		&& reader.Read(&overlay.ForegroundColor, sizeof(overlay.ForegroundColor))
		&& reader.Read(&overlay.SecondaryColor, sizeof(overlay.SecondaryColor))
		&& reader.Read(&overlay.AccentColor, sizeof(overlay.AccentColor))
		&& reader.Read(&overlay.BorderColor, sizeof(overlay.BorderColor))
		&& reader.Read(&overlay.CornerRadius, sizeof(overlay.CornerRadius))
		&& reader.ReadString(overlay.PayloadId)
		&& reader.Read(&overlay.UncompressedSize, sizeof(overlay.UncompressedSize))
		&& reader.Read(&overlay.FileCount, sizeof(overlay.FileCount))
		&& reader.Read(&overlay.PayloadSize, sizeof(overlay.PayloadSize))
		&& reader.Skip(overlay.Payload, overlay.PayloadSize);
}

bool IsConsoleSubsystem(const uint8_t* image)
{
	return GetNtHeaders(image)->OptionalHeader.Subsystem == IMAGE_SUBSYSTEM_WINDOWS_CUI;
}
