cmake_minimum_required(VERSION 3.28)

set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
set(CMAKE_POLICY_DEFAULT_CMP0091 NEW)

project(RedFoxAppHost LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(DOTNET_HOST_PACK "" CACHE PATH "Microsoft.NETCore.App.Host.win-x64 native directory containing libnethost.lib")

if(NOT EXISTS "${DOTNET_HOST_PACK}/libnethost.lib")
    message(FATAL_ERROR "DOTNET_HOST_PACK must point at a directory containing libnethost.lib")
endif()

include(FetchContent)

set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(BROTLI_DISABLE_TESTS ON CACHE BOOL "" FORCE)
set(BROTLI_BUILD_TOOLS OFF CACHE BOOL "" FORCE)

FetchContent_Declare(brotli GIT_REPOSITORY https://github.com/google/brotli.git GIT_TAG v1.1.0 GIT_SHALLOW TRUE EXCLUDE_FROM_ALL)
FetchContent_MakeAvailable(brotli)

add_executable(RedFoxAppHost WIN32
    src/Main.cpp
    src/Overlay.cpp
    src/Extractor.cpp
    src/Splash.cpp
    src/Host.cpp)

target_include_directories(RedFoxAppHost PRIVATE "${DOTNET_HOST_PACK}")
target_compile_definitions(RedFoxAppHost PRIVATE UNICODE _UNICODE NOMINMAX NETHOST_USE_AS_STATIC)
target_compile_options(RedFoxAppHost PRIVATE /W4 /WX /O1 /GS /utf-8)
target_link_options(RedFoxAppHost PRIVATE /MANIFEST:NO)
target_link_libraries(RedFoxAppHost PRIVATE brotlidec brotlicommon "${DOTNET_HOST_PACK}/libnethost.lib" gdiplus shcore shlwapi shell32 ole32)
