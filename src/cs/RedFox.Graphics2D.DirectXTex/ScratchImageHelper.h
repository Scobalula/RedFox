// ------------------------------------------------------------------------
// RedFox - My Utility Library
// Copyright(c) 2018 Philip/Scobalula
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// ------------------------------------------------------------------------
// File: ScratchImageHelper.h
// Author: Philip/Scobalula
// Description: Helpers for working with DirectXTex ScratchImage
#pragma once

using namespace System;
using namespace System::Runtime::InteropServices;

namespace RedFox
{
    namespace Graphics2D
    {
        namespace DirectXTex
        {
            /// <summary>
            /// Helpers for working with DirectXTex ScratchImage
            /// </summary>
            private ref class InteropUtility abstract sealed
            {
            public:
                /// <summary>
                /// Converts a managed string to a standard C++ string
                /// </summary>
                /// <param name="stringInput">Input Managed String</param>
                /// <param name="stringOutput">Output C++ String</param>
                static void ToStdString(String^ stringInput, std::string& stringOutput)
                {
                    const char* result = (const char*)(Marshal::StringToHGlobalAnsi(stringInput)).ToPointer();
                    stringOutput = result;
                    Marshal::FreeHGlobal(IntPtr((void*)result));
                }

                /// <summary>
                /// Converts a managed string to a standard wide C++ string
                /// </summary>
                /// <param name="stringInput">Input Managed String</param>
                /// <param name="stringOutput">Output wide C++ String</param>
                static void ToStdWString(String^ stringInput, std::wstring& stringOutput)
                {
                    const wchar_t* result = (const wchar_t*)(Marshal::StringToHGlobalUni(stringInput)).ToPointer();
                    stringOutput = result;
                    Marshal::FreeHGlobal(IntPtr((void*)result));
                }

                /// <summary>
                /// Gets the output format for the given extension, if unrecognized, DDS is returned
                /// </summary>
                /// <param name="extension">Extension to check</param>
                static ScratchImageFileFormat GetImageFormatForExtension(String^ extension)
                {
                    // DDS Files
                    if (extension->Equals(".dds", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::DDS;
                    // PNG Files
                    if (extension->Equals(".png", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::PNG;
                    // BMP Files
                    if (extension->Equals(".bmp", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::BMP;
                    // Targa Files
                    if (extension->Equals(".tga", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::TGA;
                    // JPG Files
                    if (extension->Equals(".jpg", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::JPG;
                    // JPG Files
                    if (extension->Equals(".jpeg", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::JPG;
                    // TIFF Files
                    if (extension->Equals(".tif", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::TIF;
                    // TIFF Files
                    if (extension->Equals(".tiff", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::TIF;
                    // EXR Files
                    if (extension->Equals(".exr", StringComparison::CurrentCultureIgnoreCase))
                        return ScratchImageFileFormat::EXR;
                    // Return DDS by default
                    return ScratchImageFileFormat::DDS;
                }
            };
        }
    }
}