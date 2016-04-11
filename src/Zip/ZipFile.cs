///----------- ----------- ----------- ----------- ----------- -----------
/// <copyright file="WindowsRuntimeExtensions.cs" company="Microsoft">
///     Copyright (c) Microsoft Corporation.  All rights reserved.
/// </copyright>                               
///
/// <owner>GPaperin</owner>
///----------- ----------- ----------- ----------- ----------- -----------

using System.Diagnostics.Contracts;
using System.Text;


namespace System.IO.Compression {

public static class ZipFile {


    /// <summary>
    /// Opens a <code>ZipArchive</code> on the specified path for reading. The specified file is opened with <code>FileMode.Open</code>.
    /// </summary>
    /// 
    /// <exception cref="ArgumentException">archiveFileName is a zero-length string, contains only white space, or contains one
    ///                                     or more invalid characters as defined by InvalidPathChars.</exception>
    /// <exception cref="ArgumentNullException">archiveFileName is null.</exception>
    /// <exception cref="PathTooLongException">The specified archiveFileName exceeds the system-defined maximum length.
    ///                                        For example, on Windows-based platforms, paths must be less than 248 characters,
    ///                                        and file names must be less than 260 characters.</exception>
    /// <exception cref="DirectoryNotFoundException">The specified archiveFileName is invalid, (for example, it is on an unmapped drive).</exception>
    /// <exception cref="IOException">An unspecified I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">archiveFileName specified a directory.
    ///                                               -OR- The caller does not have the required permission.</exception>
    /// <exception cref="FileNotFoundException">The file specified in archiveFileName was not found.</exception>
    /// <exception cref="NotSupportedException">archiveFileName is in an invalid format. </exception>
    /// <exception cref="InvalidDataException">The specified file could not be interpreted as a Zip file.</exception>
    /// 
    /// <param name="archiveFileName">A string specifying the path on the filesystem to open the archive on. The path is permitted
    /// to specify relative or absolute path information. Relative path information is interpreted as relative to the current working directory.</param>
    public static ZipArchive OpenRead(String archiveFileName) {

        return Open(archiveFileName, ZipArchiveMode.Read);
    }


    /// <summary>
    /// Opens a <code>ZipArchive</code> on the specified <code>archiveFileName</code> in the specified <code>ZipArchiveMode</code> mode.
    /// </summary>
    /// 
    /// <exception cref="ArgumentException">archiveFileName is a zero-length string, contains only white space,
    ///                                     or contains one or more invalid characters as defined by InvalidPathChars.</exception>
    /// <exception cref="ArgumentNullException">path is null.</exception>
    /// <exception cref="PathTooLongException">The specified archiveFileName exceeds the system-defined maximum length.
    ///                                        For example, on Windows-based platforms, paths must be less than 248 characters,
    ///                                        and file names must be less than 260 characters.</exception>
    /// <exception cref="DirectoryNotFoundException">The specified archiveFileName is invalid, (for example, it is on an unmapped drive).</exception>
    /// <exception cref="IOException">An unspecified I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">archiveFileName specified a directory.
    ///                                               -OR- The caller does not have the required permission.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><code>mode</code> specified an invalid value.</exception>
    /// <exception cref="FileNotFoundException">The file specified in <code>archiveFileName</code> was not found. </exception>
    /// <exception cref="NotSupportedException"><code>archiveFileName</code> is in an invalid format.</exception>
    /// <exception cref="InvalidDataException">The specified file could not be interpreted as a Zip file.
    ///                                        -OR- <code>mode</code> is <code>Update</code> and an entry is missing from the archive or
    ///                                        is corrupt and cannot be read.
    ///                                        -OR- <code>mode</code> is <code>Update</code> and an entry is too large to fit into memory.</exception>
    ///                                        
    /// <param name="archiveFileName">A string specifying the path on the filesystem to open the archive on.
    /// The path is permitted to specify relative or absolute path information.
    /// Relative path information is interpreted as relative to the current working directory.</param>
    /// <param name="mode">See the description of the <code>ZipArchiveMode</code> enum.
    /// If <code>Read</code> is specified, the file is opened with <code>System.IO.FileMode.Open</code>, and will throw
    /// a <code>FileNotFoundException</code> if the file does not exist.
    /// If <code>Create</code> is specified, the file is opened with <code>System.IO.FileMode.CreateNew</code>, and will throw
    /// a <code>System.IO.IOException</code> if the file already exists.
    /// If <code>Update</code> is specified, the file is opened with <code>System.IO.FileMode.OpenOrCreate</code>.
    /// If the file exists and is a Zip file, its entries will become accessible, and may be modified, and new entries may be created.
    /// If the file exists and is not a Zip file, a <code>ZipArchiveException</code> will be thrown.
    /// If the file exists and is empty or does not exist, a new Zip file will be created.
    /// Note that creating a Zip file with the <code>ZipArchiveMode.Create</code> mode is more efficient when creating a new Zip file.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]  // See comment in the body.
    public static ZipArchive Open(String archiveFileName, ZipArchiveMode mode) {

        return Open(archiveFileName, mode, entryNameEncoding: null);
    }


    /// <summary>
    /// Opens a <code>ZipArchive</code> on the specified <code>archiveFileName</code> in the specified <code>ZipArchiveMode</code> mode.
    /// </summary>
    /// 
    /// <exception cref="ArgumentException">archiveFileName is a zero-length string, contains only white space,
    ///                                     or contains one or more invalid characters as defined by InvalidPathChars.</exception>
    /// <exception cref="ArgumentNullException">path is null.</exception>
    /// <exception cref="PathTooLongException">The specified archiveFileName exceeds the system-defined maximum length.
    ///                                        For example, on Windows-based platforms, paths must be less than 248 characters,
    ///                                        and file names must be less than 260 characters.</exception>
    /// <exception cref="DirectoryNotFoundException">The specified archiveFileName is invalid, (for example, it is on an unmapped drive).</exception>
    /// <exception cref="IOException">An unspecified I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">archiveFileName specified a directory.
    ///                                               -OR- The caller does not have the required permission.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><code>mode</code> specified an invalid value.</exception>
    /// <exception cref="FileNotFoundException">The file specified in <code>archiveFileName</code> was not found. </exception>
    /// <exception cref="NotSupportedException"><code>archiveFileName</code> is in an invalid format.</exception>
    /// <exception cref="InvalidDataException">The specified file could not be interpreted as a Zip file.
    ///                                        -OR- <code>mode</code> is <code>Update</code> and an entry is missing from the archive or
    ///                                        is corrupt and cannot be read.
    ///                                        -OR- <code>mode</code> is <code>Update</code> and an entry is too large to fit into memory.</exception>
    ///                                        
    /// <param name="archiveFileName">A string specifying the path on the filesystem to open the archive on.
    /// The path is permitted to specify relative or absolute path information.
    /// Relative path information is interpreted as relative to the current working directory.</param>
    /// <param name="mode">See the description of the <code>ZipArchiveMode</code> enum.
    /// If <code>Read</code> is specified, the file is opened with <code>System.IO.FileMode.Open</code>, and will throw
    /// a <code>FileNotFoundException</code> if the file does not exist.
    /// If <code>Create</code> is specified, the file is opened with <code>System.IO.FileMode.CreateNew</code>, and will throw
    /// a <code>System.IO.IOException</code> if the file already exists.
    /// If <code>Update</code> is specified, the file is opened with <code>System.IO.FileMode.OpenOrCreate</code>.
    /// If the file exists and is a Zip file, its entries will become accessible, and may be modified, and new entries may be created.
    /// If the file exists and is not a Zip file, a <code>ZipArchiveException</code> will be thrown.
    /// If the file exists and is empty or does not exist, a new Zip file will be created.
    /// Note that creating a Zip file with the <code>ZipArchiveMode.Create</code> mode is more efficient when creating a new Zip file.</param>
    /// <param name="entryNameEncoding">The encoding to use when reading or writing entry names in this ZipArchive.
    ///         ///     <para>NOTE: Specifying this parameter to values other than <c>null</c> is discouraged.
    ///         However, this may be necessary for interoperability with ZIP archive tools and libraries that do not correctly support
    ///         UTF-8 encoding for entry names.<br />
    ///         This value is used as follows:</para>
    ///     <para><strong>Reading (opening) ZIP archive files:</strong></para>       
    ///     <para>If <c>entryNameEncoding</c> is not specified (<c>== null</c>):</para>
    ///     <list>
    ///         <item>For entries where the language encoding flag (EFS) in the general purpose bit flag of the local file header is <em>not</em> set,
    ///         use the current system default code page (<c>Encoding.Default</c>) in order to decode the entry name.</item>
    ///         <item>For entries where the language encoding flag (EFS) in the general purpose bit flag of the local file header <em>is</em> set,
    ///         use UTF-8 (<c>Encoding.UTF8</c>) in order to decode the entry name.</item>
    ///     </list>
    ///     <para>If <c>entryNameEncoding</c> is specified (<c>!= null</c>):</para>
    ///     <list>
    ///         <item>For entries where the language encoding flag (EFS) in the general purpose bit flag of the local file header is <em>not</em> set,
    ///         use the specified <c>entryNameEncoding</c> in order to decode the entry name.</item>
    ///         <item>For entries where the language encoding flag (EFS) in the general purpose bit flag of the local file header <em>is</em> set,
    ///         use UTF-8 (<c>Encoding.UTF8</c>) in order to decode the entry name.</item>
    ///     </list>
    ///     <para><strong>Writing (saving) ZIP archive files:</strong></para>
    ///     <para>If <c>entryNameEncoding</c> is not specified (<c>== null</c>):</para>
    ///     <list>
    ///         <item>For entry names that contain characters outside the ASCII range,
    ///         the language encoding flag (EFS) will be set in the general purpose bit flag of the local file header,
    ///         and UTF-8 (<c>Encoding.UTF8</c>) will be used in order to encode the entry name into bytes.</item>
    ///         <item>For entry names that do not contain characters outside the ASCII range,
    ///         the language encoding flag (EFS) will not be set in the general purpose bit flag of the local file header,
    ///         and the current system default code page (<c>Encoding.Default</c>) will be used to encode the entry names into bytes.</item>
    ///     </list>
    ///     <para>If <c>entryNameEncoding</c> is specified (<c>!= null</c>):</para>
    ///     <list>
    ///         <item>The specified <c>entryNameEncoding</c> will always be used to encode the entry names into bytes.
    ///         The language encoding flag (EFS) in the general purpose bit flag of the local file header will be set if and only
    ///         if the specified <c>entryNameEncoding</c> is a UTF-8 encoding.</item>
    ///     </list>
    ///     <para>Note that Unicode encodings other than UTF-8 may not be currently used for the <c>entryNameEncoding</c>,
    ///     otherwise an <see cref="ArgumentException"/> is thrown.</para>
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]  // See comment in the body.
    public static ZipArchive Open(String archiveFileName, ZipArchiveMode mode, Encoding entryNameEncoding) {

        // Relies on File.Open for checking of archiveFileName
            
        FileMode fileMode;
        FileAccess access;
        FileShare fileShare;

        switch (mode) {

            case ZipArchiveMode.Read:
                fileMode = FileMode.Open;
                access = FileAccess.Read;
                fileShare = FileShare.Read;
                break;

            case ZipArchiveMode.Create:
                fileMode = FileMode.CreateNew;
                access = FileAccess.Write;
                fileShare = FileShare.None;
                break;

            case ZipArchiveMode.Update:
                fileMode = FileMode.OpenOrCreate;
                access = FileAccess.ReadWrite;
                fileShare = FileShare.None;
                break;

            default:
                throw new ArgumentOutOfRangeException("mode");
        }
        
        // Surpress CA2000: fs gets passed to the new ZipArchive, which stores it internally.
        // The stream will then be owned by the archive and be disposed when the archive is disposed.        
        // If the ctor completes without throwing, we know fs has been successfully stores in the archive;
        // If the ctor throws, we need to close it here.

        FileStream fs = null;

        try {

            fs = File.Open(archiveFileName, fileMode, access, fileShare);
            return new ZipArchive(fs, mode, leaveOpen: false, entryNameEncoding: entryNameEncoding);            

        } catch {

            if (fs != null)
                fs.Dispose();
            throw;
        }
    }
   
}  // class ZipFile

}  // namespace
