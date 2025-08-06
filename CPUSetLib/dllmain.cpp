#include <Windows.h>


BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD ul_reason_for_call,
                      LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
        case DLL_THREAD_ATTACH:
        case DLL_THREAD_DETACH:
        case DLL_PROCESS_DETACH:
            break;
    }
    return TRUE;
}


struct ProcessInfo {
    HANDLE hProcess;
    wchar_t* Name;
    wchar_t* ImagePathName;
    FILETIME CreationTime;
};


extern "C" __declspec(dllexport) bool OpenProcessWithInfo([in] DWORD pid, [out] ProcessInfo* pInfo, DWORD stringSizes) {
    pInfo->hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_LIMITED_INFORMATION, false, pid);
    if (!pInfo->hProcess)
        return false;
    
    BOOL result = QueryFullProcessImageNameW(pInfo->hProcess, 0, pInfo->ImagePathName, &stringSizes);
    if (!result)
        return false;


}
