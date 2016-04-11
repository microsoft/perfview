#include "Stdafx.h"

// We use the same guid for the COM object CLSID as we do for the ETW provider.
// {6652970f-1756-5d8d-0805-e9aad152aa84}
const GUID g_CLSID_PROFILER = { 0x6652970f, 0x1756, 0x5d8d, { 0x08, 0x05, 0xe9, 0xaa, 0xd1, 0x52, 0xaa, 0x84 } };

// COM conventions need a class factory which actually make the object desired (in this case a CLR Profiler 
class CClassFactory : public IClassFactory
{
public:
    CClassFactory( ) { m_refCount = 0 ; }
    ULONG __stdcall  AddRef( ) { return InterlockedIncrement(&m_refCount);  } 
    ULONG __stdcall Release( ) { auto ret = InterlockedDecrement (&m_refCount); if (ret <= 0) delete(this); return ret; } 
    HRESULT __stdcall QueryInterface (REFIID  riid,void ** ppInterface );
    HRESULT __stdcall  LockServer(BOOL bLock) { return S_OK; } 
    HRESULT __stdcall  CreateInstance(IUnknown * pUnkOuter, REFIID riid, void** ppInterface);
private:
    long m_refCount ;
} ;

//=============================================================
//  Dll entry point definition, keeps linker happy. 
//=============================================================
// For VS 2012 they use main()
int main()
{
    return 0;
}

// For VS 2010 they use DllMain
BOOL WINAPI DllMain( 
    HINSTANCE   hInstance   , 
    DWORD       dwReason    , 
    LPVOID      lpReserved  )
{    
    switch ( dwReason )
    {
        case DLL_PROCESS_ATTACH:
            DisableThreadLibraryCalls( hInstance );     // Don't need the thread callbacks.  
            break;        
    } 
    return TRUE;
}




//=============================================================
STDAPI DllGetClassObject ( 
    REFCLSID    rclsid  ,
    REFIID      riid    ,
    LPVOID FAR  * ppv   )
{
    HRESULT hr = E_FAIL;
    if ( rclsid == g_CLSID_PROFILER )
    {
        // Create class factory.
        CClassFactory* pClassFactory = new CClassFactory;
        if ( pClassFactory == NULL )
            return E_OUTOFMEMORY ;
        hr = pClassFactory->QueryInterface (riid, ppv) ;
    }
    return ( hr ) ;
}

//=============================================================
STDAPI DllCanUnloadNow(void)
{
    return S_OK;
}

//=============================================================
//  Class factory definition
//=============================================================

HRESULT CClassFactory :: QueryInterface ( 
    REFIID  riid        ,
    void ** ppInterface )
{
    if ( IID_IUnknown == riid )
        *ppInterface = static_cast<IUnknown *> ( this ) ;
    else if ( IID_IClassFactory == riid )
        *ppInterface = static_cast<IClassFactory *> ( this ) ;
    else
    {
        *ppInterface = NULL ;
        return ( E_NOTIMPL ) ;
    }
    reinterpret_cast<IUnknown *>( *ppInterface )->AddRef ( ) ;
    return ( S_OK ) ;
}

HRESULT CClassFactory :: CreateInstance( 
        IUnknown * pUnkOuter   ,
        REFIID     riid        ,
        void **    ppInterface )
{
    if ( NULL != pUnkOuter )
        return ( CLASS_E_NOAGGREGATION ) ;

    CorProfilerTracer * pProfilerCallback = new CorProfilerTracer();
	if ( pProfilerCallback == NULL )
        return E_OUTOFMEMORY ;
	return pProfilerCallback->QueryInterface(riid, ppInterface);
}
