//=============================================================
//  Header files includes section
//=============================================================

#pragma once

#include <assert.h>

// Discover banned API usage
#include "banned.h"

// Generic logging 
#include "Logger.h"

// CorProfilerTracer is the class that interfaces to the .NET Profiling API
// See CorProfilerTracer.cpp for definition of Profiler GUID etc 
#include "CorProfilerTracer.h"

//=============================================================
//  Constants section
//=============================================================

#define INVALID_TRACEHANDLE_VALUE       ((TRACEHANDLE)INVALID_HANDLE_VALUE)

//=============================================================
//  Macros section
//=============================================================

#define LOGHR(CALL)                                                             \
{                                                                               \
    LOG_TRACE(L"Error code: [HR]    %d[0x%x]\n", hr, hr   );                    \
    LOG_TRACE(L"    @ " L#CALL L"\n");                                          \
    goto exit;                                                                  \
}

#define LOGEC(ec, CALL)                                                         \
{                                                                               \
    LPVOID lpMsgBuf;                                                            \
    FormatMessage(                                                              \
        FORMAT_MESSAGE_ALLOCATE_BUFFER |                                        \
        FORMAT_MESSAGE_FROM_SYSTEM |                                            \
        FORMAT_MESSAGE_IGNORE_INSERTS,                                          \
        NULL,                                                                   \
        ec,                                                                     \
        MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),                              \
        (LPTSTR) &lpMsgBuf,                                                     \
        0,                                                                      \
        NULL );                                                                 \
    LOG_TRACE(L"Error code: [EC]    %u : %s\n", ec, lpMsgBuf);                  \
    LocalFree(lpMsgBuf);                                                        \
    hr = HRESULT_FROM_WIN32(ec);                                                \
    LOGHR(CALL);                                                                \
}

#define CALL_N_LOGHRONCOND( CALL, COND )                                        \
{                                                                               \
    hr = CALL;                                                                  \
    if (COND)                                                                   \
    {                                                                           \
        LOGHR(CALL);                                                            \
    }                                                                           \
}

#define CALL_N_LOGECONCOND( CALL, COND )                                        \
{                                                                               \
    DWORD ec = CALL;                                                            \
    if (COND)                                                                   \
    {                                                                           \
        LOGEC(ec, CALL);                                                        \
    }                                                                           \
}

#define CALL_N_LOGLASTECONCOND( CALL, COND )                                    \
{                                                                               \
    if ((CALL) COND)                                                            \
    {                                                                           \
        DWORD ec = GetLastError();                                              \
        LOGEC(ec, CALL);                                                        \
    }                                                                           \
}

#define CALL_N_LOGONBADHR( CALL )       CALL_N_LOGHRONCOND(     CALL, FAILED(hr) )
#define CALL_N_LOGONNOTOK( CALL )       CALL_N_LOGHRONCOND(     CALL, hr != S_OK )
#define CALL_N_LOGONBADEC( CALL )       CALL_N_LOGECONCOND(     CALL, ec != ERROR_SUCCESS )
#define CALL_N_LOGONNULL( CALL )        CALL_N_LOGLASTECONCOND( CALL, == NULL )
