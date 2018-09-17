//=============================================================
//  Header files section
//=============================================================
#include "stdafx.h"

#if DEBUG 
#include <stdio.h>

 // Static variables for logging errors and tracing information
Logger * Logger::s_pTraceLogger = NULL;

//=============================================================
//  Logger class definition
//=============================================================

Logger::Logger(wchar_t * logFileName)
{
	m_logFileStream = NULL;
	_wfopen_s(&m_logFileStream, logFileName, L"w+");
	assert(m_logFileStream != NULL);
}

Logger :: ~Logger()
{
	if (m_logFileStream != NULL)
		Close();
}

/* Logger.Close
 * -----------------------------------------------------------------------------
 *
 */
void Logger::Close()
{
	if (m_logFileStream != NULL)
	{
		fflush(m_logFileStream);
		fclose(m_logFileStream);
	}
	m_logFileStream = NULL;
}

/* Logger.Flush
 * -----------------------------------------------------------------------------
 *
 */
void Logger::Flush()
{
	if (m_logFileStream != NULL)
		fflush(m_logFileStream);
}

/* Logger.Trace
 * -----------------------------------------------------------------------------
 *
 */
void Logger::Trace(wchar_t * format, ...)
{
	if (m_logFileStream != NULL)
	{
		va_list             arglist;
		va_start(arglist, format);

		vfwprintf(m_logFileStream, format, arglist);
		va_end(arglist);

		// Always flush
#ifdef DEBUG
		Flush();
#endif 
	}
}

/* Logger.TraceString
 * -----------------------------------------------------------------------------
 *
 */
void Logger::TraceString(wchar_t * lpsz)
{
	if (m_logFileStream != NULL)
		fputws(lpsz, m_logFileStream);
}

#endif // DEBUG