// Readme.cs
//
// The OSExtensions.DLL is a DLL that contains a small number of extensions
// to the operating system that allow it to do certain ETW operations.  
//
// However this DLL is implemented using private OS APIs, and as such should
// really be considered part of the operating system (until such time as
// the OS provide the functionality in public APIs).
//
// To discourage taking dependencies on these internal details we do not 
// provide the source code for this DLL in the open source repo. 
//
// IF YOU SIMPLY WANT TO BUILD PERFIVEW YOU DO NOT NEED TO BUILD OSExtensions!
// A binary copy of this DLL is included in the TraceEvent\OSExtensions.  
//
//*************************************************************************** 
// However we don't want this source code to be lost.  So we check it in
// with the rest of the code but in an encrypted form for only those few
// OS developers who may need to update this interface.   These people 
// should have access to the password needed to unencrpt the file.    
//
// As part of the build process for OSExtension.dll, we run the command 'syncEncrypted.exe'.
// This command keeps a encrypted and unencrypted version of a a file  in sync.
// Currently it is run on this pair
//
//  OSExtensions.cs   <-->  OSExtesions.cs.crypt
//
// Using a password file 'password.txt'  
//
// Thus if the password.txt exists and OSExtesions.cs.crypt exist, it will
// unencrypt it to OSExtesions.cs.   If OSExtesions.cs is newer, it will
// be reencrypted to OSExtesions.cs.crypt.   
//
// Thus the build 'does the right thing'.   
// 
// The .gitignore will ignore both the OSExtensions.cs as well as the password.txt
// so by default neither of these will be checked in.  Thus you can check in
// normally and it will do the 'right thing' of only checking in the encrypted
// file.  
//
// If you try to build OSExtensions from a 'clean' sync of the repo, you will
// get an error indicating that the password.txt file is not present.   You need
// to create this file with the correct password, at which point the build should
// just work, and you can operate normally.  
