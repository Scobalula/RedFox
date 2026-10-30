#include "Extractor.h"

#include <windows.h>
#include <shlobj.h>
#include <brotli/decode.h>
#include <algorithm>
#include <filesystem>
#include <vector>

namespace fs = std::filesystem;

class PayloadReader
{
private:
	BrotliDecoderState*	decoder		= nullptr;
	const uint8_t*		input;
	const uint8_t*		end;
	size_t				chunkSize	= 0;
	uint64_t			position	= 0;

public:
	PayloadReader(const Overlay& overlay) : input(overlay.Payload), end(overlay.Payload + overlay.PayloadSize)
	{
	}

	~PayloadReader()
	{
		BrotliDecoderDestroyInstance(decoder);
	}

	uint64_t GetPosition() const
	{
		return position;
	}

	bool Read(void* destination, size_t size)
	{
		auto	output		= static_cast<uint8_t*>(destination);
		size_t	available	= size;

		while (available > 0)
		{
			if (decoder == nullptr && !NextChunk())
			{
				return false;
			}

			auto result = BrotliDecoderDecompressStream(decoder, &chunkSize, &input, &available, &output, nullptr);

			if (result == BROTLI_DECODER_RESULT_SUCCESS && chunkSize == 0)
			{
				BrotliDecoderDestroyInstance(decoder);
				decoder = nullptr;
			}
			else if (result != BROTLI_DECODER_RESULT_NEEDS_MORE_OUTPUT)
			{
				return false;
			}
		}

		position += size;
		return true;
	}

private:
	bool NextChunk()
	{
		uint32_t size = 0;

		if (static_cast<size_t>(end - input) < sizeof(size))
		{
			return false;
		}

		memcpy(&size, input, sizeof(size));
		input += sizeof(size);

		if (static_cast<size_t>(end - input) < size)
		{
			return false;
		}

		chunkSize	= size;
		decoder		= BrotliDecoderCreateInstance(nullptr, nullptr, nullptr);
		return true;
	}
};

static bool WriteEntry(PayloadReader& reader, HANDLE file, uint64_t length, std::vector<uint8_t>& buffer, const Overlay& overlay, const std::function<void(double)>& progress)
{
	while (length > 0)
	{
		DWORD count		= static_cast<DWORD>(std::min<uint64_t>(length, buffer.size()));
		DWORD written	= 0;

		if (!reader.Read(buffer.data(), count) || !WriteFile(file, buffer.data(), count, &written, nullptr) || written != count)
		{
			return false;
		}

		length -= count;
		progress(static_cast<double>(reader.GetPosition()) / overlay.UncompressedSize);
	}

	return true;
}

static bool ExtractPayload(const Overlay& overlay, const fs::path& directory, const std::function<void(double)>& progress, std::wstring& error)
{
	PayloadReader			reader(overlay);
	std::vector<uint8_t>	buffer(1 << 20);

	for (uint32_t i = 0; i < overlay.FileCount; i++)
	{
		uint16_t		pathLength	= 0;
		uint64_t		length		= 0;
		std::wstring	path;

		if (!reader.Read(&pathLength, sizeof(pathLength)))
		{
			error = L"The packed payload is corrupt.";
			return false;
		}

		path.resize(pathLength);

		if (!reader.Read(path.data(), pathLength * sizeof(wchar_t)) || !reader.Read(&length, sizeof(length)))
		{
			error = L"The packed payload is corrupt.";
			return false;
		}

		auto			filePath = directory / path;
		std::error_code	code;

		fs::create_directories(filePath.parent_path(), code);

		HANDLE file = CreateFileW(filePath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);

		if (file == INVALID_HANDLE_VALUE)
		{
			error = L"Failed to create " + filePath.wstring() + L" (error " + std::to_wstring(GetLastError()) + L").";
			return false;
		}

		bool written = WriteEntry(reader, file, length, buffer, overlay, progress);
		CloseHandle(file);

		if (!written)
		{
			error = L"Failed to extract " + filePath.wstring() + L".";
			return false;
		}
	}

	return true;
}

static std::wstring GetPathId(std::wstring path)
{
	uint64_t	hash	= 14695981039346656037ull;
	wchar_t		id[17]	= {};

	CharLowerBuffW(path.data(), static_cast<DWORD>(path.size()));

	for (wchar_t character : path)
	{
		hash = (hash ^ character) * 1099511628211ull;
	}

	swprintf_s(id, L"%016llx", hash);
	return id;
}

static void RemoveOldVersions(const fs::path& root, const std::wstring& payloadId)
{
	std::vector<fs::path>	directories;
	std::error_code			code;

	for (const auto& entry : fs::directory_iterator(root, code))
	{
		if (!entry.path().filename().wstring().starts_with(payloadId))
		{
			directories.push_back(entry.path());
		}
	}

	for (const auto& directory : directories)
	{
		auto removal = directory;

		if (directory.filename().wstring().find(L".delete") == std::wstring::npos)
		{
			removal += L".delete" + std::to_wstring(GetCurrentProcessId());

			if (!MoveFileExW(directory.c_str(), removal.c_str(), 0))
			{
				continue;
			}
		}

		fs::remove_all(removal, code);
	}
}

bool PrepareAppDirectory(const std::wstring& hostPath, const Overlay& overlay, const std::function<void(double)>& progress, std::wstring& appDirectory, std::wstring& error)
{
	PWSTR localAppData = nullptr;

	if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &localAppData)))
	{
		error = L"Failed to locate the local application data folder.";
		return false;
	}

	auto root	= fs::path(localAppData) / overlay.Name / L"App" / GetPathId(hostPath);
	auto target	= root / overlay.PayloadId;
	CoTaskMemFree(localAppData);

	std::error_code code;

	if (!fs::exists(target, code))
	{
		auto staging = root / (overlay.PayloadId + L".tmp" + std::to_wstring(GetCurrentProcessId()));

		fs::remove_all(staging, code);
		fs::create_directories(staging, code);

		if (!ExtractPayload(overlay, staging, progress, error))
		{
			fs::remove_all(staging, code);
			return false;
		}

		if (!MoveFileExW(staging.c_str(), target.c_str(), 0))
		{
			fs::remove_all(staging, code);

			if (!fs::exists(target, code))
			{
				error = L"Failed to move the extracted files to " + target.wstring() + L".";
				return false;
			}
		}
	}

	RemoveOldVersions(root, overlay.PayloadId);
	appDirectory = target.wstring();
	return true;
}
