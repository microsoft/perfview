#pragma once

// Headers needed for CLR Profiling
#include <cor.h>
#include <corprof.h>
#pragma warning(push)
#pragma warning(disable: 4458) // The .NET Framework SDK header shadows a member named Size.
#include <corhlpr.h>
#pragma warning(pop)

#include <unordered_map> 

class ClassInfo;
class ModuleInfo;

// ==========================================================================
// CorProfileTracer is the main routine that implemented that .NET Profiler
// API and responds by generating ETW events.   Basically it implemented a
// bunch of callback functions that simply log the cooresponding ETW event 
//
// Because this class's COM GUID is passed to the .Net Runtime (either in
// the COR_PROFILER environment variable or in the AttachProfiler API
// the runtime will create one of these objects and immediately call the
// Initialize or InitializeForAttach callback.  It is in these routines
// where the real initializaiton occurs.  
//
// See comment in the beginning of CorProfilerTracer.cpp for more 
class CorProfilerTracer : public ICorProfilerCallback3
{
public:
	CorProfilerTracer();
	~CorProfilerTracer();

	// IUnknown interface implementation
	STDMETHODIMP_(ULONG) AddRef() { return InterlockedIncrement(&m_refCount); }
	STDMETHODIMP_(ULONG) Release() { auto ret = InterlockedDecrement(&m_refCount); if (ret <= 0) delete(this); return ret; }
	STDMETHODIMP QueryInterface(REFIID riid, void **ppInterface);

	// ICorProfilerCallback interface implementation
	STDMETHODIMP Initialize(IUnknown * pICorProfilerInfoUnk) { return InitializeForAttach(pICorProfilerInfoUnk, NULL, static_cast<UINT>(-1)); }
	STDMETHODIMP Shutdown();

	// ICorProfilerCallback3
	HRESULT STDMETHODCALLTYPE InitializeForAttach(
		/* [in] */ IUnknown *pCorProfilerInfoUnk,
		/* [in] */ void *pvClientData,
		/* [in] */ UINT cbClientData);
	HRESULT STDMETHODCALLTYPE ProfilerAttachComplete(void) { return S_OK; };
	HRESULT STDMETHODCALLTYPE ProfilerDetachSucceeded(void)
	{
		LOG_TRACE(L"ProfilerDetachSucceeded\n");
		return Shutdown();
	}

	STDMETHODIMP AppDomainCreationStarted(AppDomainID) { return S_OK; };
	STDMETHODIMP AppDomainCreationFinished(AppDomainID, HRESULT) { return S_OK; };
	STDMETHODIMP AppDomainShutdownStarted(AppDomainID) { return S_OK; };
	STDMETHODIMP AppDomainShutdownFinished(AppDomainID, HRESULT) { return S_OK; };
	STDMETHODIMP AssemblyLoadStarted(AssemblyID) { return S_OK; };
	STDMETHODIMP AssemblyLoadFinished(AssemblyID, HRESULT) { return S_OK; };
	STDMETHODIMP AssemblyUnloadStarted(AssemblyID) { return S_OK; };
	STDMETHODIMP AssemblyUnloadFinished(AssemblyID, HRESULT) { return S_OK; };
	STDMETHODIMP ModuleLoadStarted(ModuleID) { return S_OK; };
	STDMETHODIMP ModuleLoadFinished(ModuleID, HRESULT) { return S_OK; };
	STDMETHODIMP ModuleUnloadStarted(ModuleID) { return S_OK; };
	STDMETHODIMP ModuleUnloadFinished(ModuleID, HRESULT) { return S_OK; };
	STDMETHODIMP ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID assemblyId);
	STDMETHODIMP ClassLoadStarted(ClassID) { return S_OK; };
	STDMETHODIMP ClassLoadFinished(ClassID, HRESULT) { return S_OK; };
	STDMETHODIMP ClassUnloadStarted(ClassID) { return S_OK; };
	STDMETHODIMP ClassUnloadFinished(ClassID, HRESULT) { return S_OK; };
	STDMETHODIMP FunctionUnloadStarted(FunctionID) { return S_OK; };
	STDMETHODIMP JITCompilationStarted(FunctionID, BOOL) { return S_OK; };
	STDMETHODIMP JITCompilationFinished(FunctionID, HRESULT, BOOL) { return S_OK; };
	STDMETHODIMP JITCachedFunctionSearchStarted(FunctionID, BOOL *) { return S_OK; };
	STDMETHODIMP JITCachedFunctionSearchFinished(FunctionID, COR_PRF_JIT_CACHE) { return S_OK; };
	STDMETHODIMP JITFunctionPitched(FunctionID) { return S_OK; };
	STDMETHODIMP JITInlining(FunctionID, FunctionID, BOOL *) { return S_OK; };
	STDMETHODIMP ThreadCreated(ThreadID) { return S_OK; };
	STDMETHODIMP ThreadDestroyed(ThreadID) { return S_OK; };
	STDMETHODIMP ThreadAssignedToOSThread(ThreadID, ULONG) { return S_OK; };
	STDMETHODIMP RemotingClientInvocationStarted() { return S_OK; };
	STDMETHODIMP RemotingClientSendingMessage(GUID *, BOOL) { return S_OK; };
	STDMETHODIMP RemotingClientReceivingReply(GUID *, BOOL) { return S_OK; };
	STDMETHODIMP RemotingClientInvocationFinished() { return S_OK; };
	STDMETHODIMP RemotingServerReceivingMessage(GUID *, BOOL) { return S_OK; };
	STDMETHODIMP RemotingServerInvocationStarted() { return S_OK; };
	STDMETHODIMP RemotingServerInvocationReturned() { return S_OK; };
	STDMETHODIMP RemotingServerSendingReply(GUID *, BOOL) { return S_OK; };
	STDMETHODIMP UnmanagedToManagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) { return S_OK; };
	STDMETHODIMP ManagedToUnmanagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) { return S_OK; };
	STDMETHODIMP RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON) { return S_OK; };
	STDMETHODIMP RuntimeSuspendFinished() { return S_OK; };
	STDMETHODIMP RuntimeSuspendAborted() { return S_OK; };
	STDMETHODIMP RuntimeResumeStarted() { return S_OK; };
	STDMETHODIMP RuntimeResumeFinished() { return S_OK; };
	STDMETHODIMP RuntimeThreadSuspended(ThreadID) { return S_OK; };
	STDMETHODIMP RuntimeThreadResumed(ThreadID) { return S_OK; };
	STDMETHODIMP MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]);
	STDMETHODIMP ObjectAllocated(ObjectID objectId, ClassID classId);
	STDMETHODIMP ObjectsAllocatedByClass(ULONG, ClassID[], ULONG[]) { return S_OK; };
	STDMETHODIMP ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[]);
	STDMETHODIMP RootReferences(ULONG, ObjectID[]) { return S_OK; }
	STDMETHODIMP ExceptionThrown(ObjectID) { return S_OK; };
	STDMETHODIMP ExceptionSearchFunctionEnter(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionSearchFunctionLeave() { return S_OK; };
	STDMETHODIMP ExceptionSearchFilterEnter(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionSearchFilterLeave() { return S_OK; };
	STDMETHODIMP ExceptionSearchCatcherFound(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionOSHandlerEnter(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionOSHandlerLeave(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionUnwindFunctionEnter(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionUnwindFunctionLeave() { return S_OK; };
	STDMETHODIMP ExceptionUnwindFinallyEnter(FunctionID) { return S_OK; };
	STDMETHODIMP ExceptionUnwindFinallyLeave() { return S_OK; };
	STDMETHODIMP ExceptionCatcherEnter(FunctionID, ObjectID) { return S_OK; };
	STDMETHODIMP ExceptionCatcherLeave() { return S_OK; };
	STDMETHODIMP COMClassicVTableCreated(ClassID, REFGUID, void *, ULONG) { return S_OK; };
	STDMETHODIMP COMClassicVTableDestroyed(ClassID, REFGUID, void *) { return S_OK; };
	STDMETHODIMP ExceptionCLRCatcherFound(void) { return S_OK; };
	STDMETHODIMP ExceptionCLRCatcherExecute(void) { return S_OK; };

	// End of ICorProfilerCallback interface implementation

	// ICorProfilerCallback2 interface implementation

	STDMETHODIMP ThreadNameChanged(ThreadID, ULONG, WCHAR*) { return S_OK; };
	STDMETHODIMP GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason);
	STDMETHODIMP SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]);

	STDMETHODIMP GarbageCollectionFinished(void);
	STDMETHODIMP FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID);
	STDMETHODIMP RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]);
	STDMETHODIMP HandleCreated(GCHandleID handleId, ObjectID initialObjectId);
	STDMETHODIMP HandleDestroyed(GCHandleID handleId);

	void DoETWCommand(ULONG IsEnabled, UCHAR Level, ULONGLONG MatchAnyKeywords, struct _EVENT_FILTER_DESCRIPTOR* filterData);
private: // Methods
	ClassInfo* GetClassInfo(ClassID classId);
	ModuleInfo* GetModuleInfo(ModuleID moduleId);
	void ClearTables();
	void DumpClassInfo();
	static DWORD WINAPI ForceGCBody(LPVOID lpParameter);
	void ForceGC();

private: // Fields
	CRITICAL_SECTION          m_lock;

	LONG                      m_refCount;

	bool                      m_profilerLoadedAtStartup;
	bool					  m_forcingGC;
	bool					  m_detaching;
	bool                      m_sentManifest;
	ULONGLONG                 m_currentKeywords;

	// handle to query the runtime about stuff. 
	struct ICorProfilerInfo3* m_info;
	// If we have sampling turned on, m_curAllocSize tells you how close to the sampleSize we are.  
	int					     m_curAllocSize;
	// Do we have smart sampling that does sampling per type after a certain number of instances are collected.  
	bool					 m_smartSampling;
	int						 m_gcCount;

	// We want to cache the information (e.g. name, token, ...) on classes and modules.  
	std::unordered_map<ClassID, ClassInfo*> m_classInfo;
	std::unordered_map<ModuleID, ModuleInfo*> m_moduleInfo;
};
