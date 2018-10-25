#pragma once

//=============================================================
//  Header files section
//=============================================================

#include "stdafx.h"

#define TRACE_LOGGER            Logger :: s_pTraceLogger

#ifndef DEBUG 
#define LOG_TRACE(...)
#define OPEN_LOG(logger, path)
#define CLOSE_LOG(logger)
#else
#define LOG_TRACE(...)          if (TRACE_LOGGER != NULL) { TRACE_LOGGER->Trace(__VA_ARGS__); }

#define OPEN_LOG(logger, path)                                                  \
{                                                                               \
    logger = new Logger(path);                                                  \
}

#define CLOSE_LOG(logger)                                                       \
{                                                                               \
    if (logger != NULL)                                                         \
    {                                                                           \
        logger->Close();                                                        \
        delete(logger);                                                         \
        logger = NULL;                                                          \
    }                                                                           \
}

//=============================================================
//  Logger class declaration
//=============================================================
class Logger
{
public:
	Logger(wchar_t * logFileName);
	~Logger();

	void                                Open();
	void                                Close();
	void                                Flush();

	void __cdecl                        Trace(wchar_t * format, ...);
	void                                TraceString(wchar_t * lpsz);

	void                                Print();

	static Logger * s_pTraceLogger;
	static Logger * s_pErrorLogger;

private:
	struct _iobuf * /* FILE */ m_logFileStream;
};

#endif // DEBUG