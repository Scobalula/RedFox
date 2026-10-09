cmake_minimum_required(VERSION 3.19)

project(GDeflateNative LANGUAGES C)

if(NOT LIBDEFLATE_SOURCE)
    message(FATAL_ERROR "LIBDEFLATE_SOURCE must point to the DirectStorage libdeflate submodule.")
endif()

set(LIBDEFLATE_LIBRARY_SOURCES
    "${LIBDEFLATE_SOURCE}/lib/adler32.c"
    "${LIBDEFLATE_SOURCE}/lib/crc32.c"
    "${LIBDEFLATE_SOURCE}/lib/deflate_compress.c"
    "${LIBDEFLATE_SOURCE}/lib/deflate_decompress.c"
    "${LIBDEFLATE_SOURCE}/lib/gdeflate_compress.c"
    "${LIBDEFLATE_SOURCE}/lib/gdeflate_decompress.c"
    "${LIBDEFLATE_SOURCE}/lib/gzip_compress.c"
    "${LIBDEFLATE_SOURCE}/lib/gzip_decompress.c"
    "${LIBDEFLATE_SOURCE}/lib/utils.c"
    "${LIBDEFLATE_SOURCE}/lib/zlib_compress.c"
    "${LIBDEFLATE_SOURCE}/lib/zlib_decompress.c"
    "${LIBDEFLATE_SOURCE}/lib/x86/cpu_features.c"
)

if(MSVC)
    add_compile_options(/wd4244 /wd4127 /wd4267 /wd4100 /wd4245 /wd4456 /wd4018 /wd4146 /wd4310)
else()
    add_compile_options(-Wno-conversion -Wno-unused-parameter -Wno-shadow)
endif()

add_library(gdeflate SHARED ${LIBDEFLATE_LIBRARY_SOURCES})
target_include_directories(gdeflate PRIVATE "${LIBDEFLATE_SOURCE}" "${LIBDEFLATE_SOURCE}/lib")
target_compile_definitions(gdeflate PRIVATE LIBDEFLATE_DLL)
set_target_properties(gdeflate PROPERTIES OUTPUT_NAME libgdeflate)
