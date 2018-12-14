
    Stack walking flags can be specified either directly on the command line or in a file:
    
        xperf -on base -stackwalk ThreadCreate+ProcessCreate
        xperf -on base -stackwalk ThreadCreate -stackwalk ProcessCreate
        xperf -on base -stackwalk @stack.txt
        xperf -on base -stackwalk 0x0501
    
    Custom stack walking flags can be specified in format: 0xmmnn, where mm is event group and nn is event type.
    
    The stack walking flag file may contain any number of stack walking flags per line,
    separated by spaces, plus ("+") signs, or on new lines:
    
        ThreadCreate ProcessCreate
        DiskReadInit+DiskWriteInit+DiskFlushInit
        CSwitch
    
    The file may also contain empty lines or comments prefixed by "!".
    
    The following is a list of recognized stack walking flags:

    NT Kernel Logger provider:
        ProcessCreate                  PagefaultHard
        ProcessDelete                  PagefaultAV
        ImageLoad                      VirtualAlloc
        ImageUnload                    VirtualFree
        ThreadCreate                   PagefileMappedSectionCreate
        ThreadDelete                   PagefileMappedSectionDelete
        CSwitch                        PagefileMappedSectionObjectCreate
        ReadyThread                    PagefileMappedSectionObjectDelete
        ThreadSetPriority              PageAccess
        ThreadSetBasePriority          PageRelease
        ThreadSetIdealProcessor        PageAccessEx
        ThreadSetUserIdealProcessor    PageRemovedfromWorkingSet
        KernelQueueEnqueue             PageRangeAccess
        KernelQueueDequeue             PageRangeRelease
        Mark                           PagefileBackedImageMapping
        SyscallEnter                   ContiguousMemoryGeneration
        SyscallExit                    HeapRangeCreate
        Profile                        HeapRangeReserve
        ProfileSetInterval             HeapRangeRelease
        TimerSetPeriodic               HeapRangeDestroy
        TimerSetOneShot                AlpcSendMessage
        PmcInterrupt                   AlpcReceiveMessage
        CacheFlush                     AlpcWaitForReply
        DpcEnqueue                     AlpcWaitForNewMessage
        DpcExecute                     AlpcUnwait
        ShouldYield                    AlpcConnectRequest
        DiskReadInit                   AlpcConnectSuccess
        DiskWriteInit                  AlpcConnectFail
        DiskFlushInit                  AlpcClosePort
        FileCreate                     ThreadPoolCallbackEnqueue
        FileCleanup                    ThreadPoolCallbackDequeue
        FileClose                      ThreadPoolCallbackStart
        FileRead                       ThreadPoolCallbackStop
        FileWrite                      ThreadPoolCallbackCancel
        FileSetInformation             ThreadPoolCreate
        FileDelete                     ThreadPoolClose
        FileRename                     ThreadPoolSetMinThreads
        FileDirEnum                    ThreadPoolSetMaxThreads
        FileFlush                      PowerSetPowerAction
        FileQueryInformation           PowerSetPowerActionReturn
        FileFSCTL                      PowerSetDevicesState
        FileDirNotify                  PowerSetDevicesStateReturn
        FileOpEnd                      PowerDeviceNotify
        MapFile                        PowerDeviceNotifyComplete
        UnMapFile                      PowerSessionCallout
        MiniFilterPreOpInit            PowerSessionCalloutReturn
        MiniFilterPostOpInit           PowerPreSleep
        SplitIO                        PowerPostSleep
        RegQueryKey                    PowerPerfStateChange
        RegEnumerateKey                PowerIdleStateChange
        RegEnumerateValueKey           PowerThermalConstraint
        RegDeleteKey                   ExecutiveResource
        RegCreateKey                   CcWorkitemEnqueue
        RegOpenKey                     CcWorkitemDequeue
        RegSetValue                    CcWorkitemComplete
        RegDeleteValue                 CcReadAhead
        RegQueryValue                  CcWriteBehind
        RegQueryMultipleValue          CcLazyWriteScan
        RegSetInformation              CcCanIWriteFail
        RegFlush                       CcFlushCache
        RegKcbCreate                   CcFlushSection
        RegKcbDelete                   PoolAlloc
        RegVirtualize                  PoolAllocSession
        RegCloseKey                    PoolFree
        RegHiveInit                    PoolFreeSession
        RegHiveDestroy                 HandleCreate
        RegHiveLink                    HandleClose
        RegHiveDirty                   HandleDuplicate
        HardFault                      ObjectCreate
        PagefaultTransition            ObjectDelete
        PagefaultDemandZero            ObjectReference
        PagefaultCopyOnWrite           ObjectDeReference
        PagefaultGuard


    Other system providers:
        HeapCreate                     HeapFree
        HeapAlloc                      HeapDestroy
        HeapRealloc
