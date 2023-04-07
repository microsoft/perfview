/***
* banned.h - list of Microsoft Security Development Lifecycle (SDL) banned APIs
*
* Purpose:
*       This include file contains a list of banned APIs which should not be used in new code and 
*       removed from legacy code over time.
*
* History
* 01-Jan-2006 - mikehow - Initial Version
* 22-Apr-2008 - mikehow	- Updated to SDL 4.1, commented out recommendations and added memcpy
* 26-Jan-2009 - mikehow - Updated to SDL 5.0, made the list sane, added SDL compliance levels
* 10-Feb-2009 - mikehow - Updated based on feedback from MS Office
* 12-May-2009 - jpardue - Added wmemcpy
* 08-Jul-2009 - mikehow - Fixed header #ifndef/#endif logic, made the SDL recommended compliance level name more obvious
* 05-Nov-2009 - mikehow	- Added vsnprintf (ANSI version of _vsnprintf)
* 01-Jan-2010 - mikehow - Added better strsafe integration, now the following works:
*							#include "strsafe.h"
*							#include "banned.h"
* 04-Jun-2010 - mikehow - Small "#if" bug fix
* 16-Jun-2011 - mikehow	- Added the two _CRT_SECURE_xxxxx macros
* 07-Jul-2011 - mikehow - Bugfix when using recommended banned functions and StrSafe. Locally surpressed C4005 warnings
* 15-Jun-2012 - bryans  - Moved lstrlen to required banned; removed strlen, wcslen, _mbslen, _mbstrlen, StrLen from recommended banned
*						   
*
***/

#ifndef _INC_BANNED
#	define _INC_BANNED

#	if defined(_MSC_VER)
#		pragma once

// Flip the 'auto-migrate' functionality in VC++
// Some functions, such as strcpy() are changed to safer functions by the compiler
// More info: http://blogs.msdn.com/b/sdl/archive/2010/02/16/vc-2010-and-memcpy.aspx
#ifndef _SDL_DONT_AUTO_FIX

#	pragma warning(push)
#	pragma warning(disable: 4005)

	// strcpy etc
#	define _CRT_SECURE_CPP_OVERLOAD_STANDARD_NAMES			(1)

	// memcpy etc
#	define _CRT_SECURE_CPP_OVERLOAD_STANDARD_NAMES_MEMORY	(1)

#	pragma warning(pop)

#endif

		// SDL 6.0, 6.1, 6.5 and 7.0 Requirements
#		if defined(_STRSAFE_H_INCLUDED_) && !defined(STRSAFE_NO_DEPRECATE)

			// Only deprecate what's not already deprecated by StrSafe
#			pragma deprecated (_mbscpy, _mbccpy)
#			pragma deprecated (strcatA, strcatW, _mbscat, StrCatBuff, StrCatBuffA, StrCatBuffW, StrCatChainW, _tccat, _mbccat)
#			pragma deprecated (strncpy, wcsncpy, _tcsncpy, _mbsncpy, _mbsnbcpy, StrCpyN, StrCpyNA, StrCpyNW, StrNCpy, strcpynA, StrNCpyA, StrNCpyW, lstrcpyn, lstrcpynA, lstrcpynW)
#			pragma deprecated (strncat, wcsncat, _tcsncat, _mbsncat, _mbsnbcat, lstrncat, lstrcatnA, lstrcatnW, lstrcatn)
#			pragma deprecated (IsBadWritePtr, IsBadHugeWritePtr, IsBadReadPtr, IsBadHugeReadPtr, IsBadCodePtr, IsBadStringPtr)
#			pragma deprecated (memcpy, RtlCopyMemory, CopyMemory, wmemcpy)
#			pragma deprecated (lstrlen)

#		else
			// StrSafe not loaded, so deprecate everything!
#			pragma deprecated (strcpy, strcpyA, strcpyW, wcscpy, _tcscpy, _mbscpy, StrCpy, StrCpyA, StrCpyW, lstrcpy, lstrcpyA, lstrcpyW, _tccpy, _mbccpy, _ftcscpy)
#			pragma deprecated (strcat, strcatA, strcatW, wcscat, _tcscat, _mbscat, StrCat, StrCatA, StrCatW, lstrcat, lstrcatA, lstrcatW, StrCatBuff, StrCatBuffA, StrCatBuffW, StrCatChainW, _tccat, _mbccat, _ftcscat)
#			pragma deprecated (sprintfW, sprintfA, wsprintf, wsprintfW, wsprintfA, sprintf, swprintf, _stprintf)
#			pragma deprecated (wvsprintf, wvsprintfA, wvsprintfW, vsprintf, _vstprintf, vswprintf)
#			pragma deprecated (strncpy, wcsncpy, _tcsncpy, _mbsncpy, _mbsnbcpy, StrCpyN, StrCpyNA, StrCpyNW, StrNCpy, strcpynA, StrNCpyA, StrNCpyW, lstrcpyn, lstrcpynA, lstrcpynW)
#			pragma deprecated (strncat, wcsncat, _tcsncat, _mbsncat, _mbsnbcat, StrCatN, StrCatNA, StrCatNW, StrNCat, StrNCatA, StrNCatW, lstrncat, lstrcatnA, lstrcatnW, lstrcatn)
#			pragma deprecated (gets, _getts, _gettws)

			// Commented out functions are used in the latest version of the Windows SDK, so we can't mark them deprecated here
			// or the additional SDL checks we have enabled will convert the warnings to errors.  Uncomment these once the usages are removed.
#			pragma deprecated (/*IsBadWritePtr, IsBadHugeWritePtr, IsBadReadPtr, IsBadHugeReadPtr, IsBadCodePtr,*/ IsBadStringPtr)
#			pragma deprecated (/*memcpy, RtlCopyMemory, */ CopyMemory, wmemcpy)
// #			pragma deprecated (lstrlen)
#		endif //defined(_STRSAFE_H_INCLUDED_) && !defined(STRSAFE_NO_DEPRECATE)

		// SDL 6.0, 6.1, 6.5 and 7.0 Recommendations
#		if defined(_SDL_BANNED_RECOMMENDED)
#			if defined(_STRSAFE_H_INCLUDED_) && !defined(STRSAFE_NO_DEPRECATE)
				// Only deprecate what's not already deprecated by StrSafe
#				pragma deprecated (wnsprintf, wnsprintfA, wnsprintfW)
#				pragma deprecated (vsnprintf, wvnsprintf, wvnsprintfA, wvnsprintfW)
#				pragma deprecated (strtok, _tcstok, wcstok, _mbstok)
#				pragma deprecated (makepath, _tmakepath,  _makepath, _wmakepath)
#				pragma deprecated (_splitpath, _tsplitpath, _wsplitpath)
#				pragma deprecated (scanf, wscanf, _tscanf, sscanf, swscanf, _stscanf, snscanf, snwscanf, _sntscanf)
#				pragma deprecated (_itoa, _itow, _i64toa, _i64tow, _ui64toa, _ui64tot, _ui64tow, _ultoa, _ultot, _ultow)
#				pragma deprecated (CharToOem, CharToOemA, CharToOemW, OemToChar, OemToCharA, OemToCharW, CharToOemBuffA, CharToOemBuffW)
#				pragma deprecated (alloca, _alloca)
#				pragma deprecated (ChangeWindowMessageFilter)
#			else
				// StrSafe not loaded, so deprecate everything!
#				pragma deprecated (wnsprintf, wnsprintfA, wnsprintfW, _snwprintf, _snprintf, _sntprintf)
#				pragma deprecated (_vsnprintf, vsnprintf, _vsnwprintf, _vsntprintf, wvnsprintf, wvnsprintfA, wvnsprintfW)
#				pragma deprecated (strtok, _tcstok, wcstok, _mbstok)
#				pragma deprecated (makepath, _tmakepath,  _makepath, _wmakepath)
#				pragma deprecated (_splitpath, _tsplitpath, _wsplitpath)
#				pragma deprecated (scanf, wscanf, _tscanf, sscanf, swscanf, _stscanf, snscanf, snwscanf, _sntscanf)
#				pragma deprecated (_itoa, _itow, _i64toa, _i64tow, _ui64toa, _ui64tot, _ui64tow, _ultoa, _ultot, _ultow)
#				pragma deprecated (CharToOem, CharToOemA, CharToOemW, OemToChar, OemToCharA, OemToCharW, CharToOemBuffA, CharToOemBuffW)
#				pragma deprecated (alloca, _alloca)
#				pragma deprecated (ChangeWindowMessageFilter)
#			endif // StrSafe
#		endif // SDL recommended

#	endif // _MSC_VER_

#endif  // _INC_BANNED 

