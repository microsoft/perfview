//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// Implements ICorDebugMetadataLocator interface
namespace Microsoft.Samples.Debugging.MetaDataLocator
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("7cef8ba9-2ef7-42bf-973f-4171474f87d9")]
    public interface ICorDebugMetaDataLocator //REM straighten this up later : IUnknown
    {
        void GetMetaData(
             [In, MarshalAs(UnmanagedType.LPWStr)] string imagePath,
             [In] uint dwImageTimeStamp,
             [In] uint dwImageSize,
             [In] uint cchPathBuffer,
             [Out] out uint pcchPathBuffer,
             [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] wszPathBuffer);
    }

    public sealed class CorDebugMetaDataLocator : ICorDebugMetaDataLocator
    {
        private string m_clrPath;
        private Guid IID_ICorDebugMetaDataLocator = new Guid("7cef8ba9-2ef7-42bf-973f-4171474f87d9");
        private const uint m_sizeOfWchar = 2;

        private struct HResultConsts
        {
            public const uint S_Ok = 0x00000000;
            public const uint E_Not_Sufficient_Buffer = 0x8007007A;
            public const uint E_Fail = 0x80004005;
        }

        public void GetMetaData(string imagePath,
                            uint dwImageTimeStamp,
                            uint dwImageSize,
                            uint cchPathBuffer,
                            out uint pcchPathBuffer,
                            char[] wszPathBuffer)
        {
            Trace.WriteLine("ICDMDL::GetMetaData called for " + imagePath + " with timestamp=" +
                dwImageTimeStamp + " and size=" + dwImageSize);

            string filePath = SearchPath + "\\" + imagePath.Substring(imagePath.LastIndexOf('\\'));

            bool fFileExist = false;

            // If we have a complete path to an existing file, just return it.
            // Otherwise, check the SearchPath for the file.
            if (File.Exists(imagePath))
            {
                filePath = imagePath;
                fFileExist = true;
            }

            if (!fFileExist && File.Exists(filePath))
            {
                fFileExist = true;
            }

            if (!fFileExist)
            {
                Trace.WriteLine("ICDMDL::GetMetaData could not find file.");
                throw new COMException("File not found", unchecked((int)0x80070002));
            }

            //TODO: implement symbol server lookups, other options.
            //TODO: Check that the file found has the correct timestamp and size.

            // Return number of chars, include the terminating NULL.
            pcchPathBuffer = (uint)filePath.Length + 1;

            if (pcchPathBuffer <= cchPathBuffer)
            {
                filePath.CopyTo(0, wszPathBuffer, 0, filePath.Length);
                wszPathBuffer[filePath.Length] = '\0';
            }
            else
            {
                Trace.WriteLine("ICDMDL::GetMetaData found file, but string buffer is too small to use.  " +
                    "Length given=" + pcchPathBuffer + " Length needed=" + cchPathBuffer + " Filename=\"" + filePath + "\"");
                throw new COMException("Buffer too small", unchecked((int)0x8007007A));
            }

            Trace.WriteLine("ICDMDL::GetMetaData found " + wszPathBuffer + "\"");
        }

        public CorDebugMetaDataLocator()
        {
            // Default to searching in current directory.
            // Could certainly have multiple search paths, use a symbol server, etc.
            SearchPath = ".\\";
        }

        public CorDebugMetaDataLocator(string searchPath)
        {
            SearchPath = searchPath;
        }

        public string SearchPath
        {
            get
            {
                return m_clrPath;
            }
            set
            {
                m_clrPath = value;
            }
        }
    }
}
