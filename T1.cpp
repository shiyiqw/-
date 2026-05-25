#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <string>
#include <vector>
#include <cstdint>
#include <algorithm>
#include "MinHook.h"

extern "C" __declspec(selectany) IMAGE_DOS_HEADER __ImageBase;

// ==============================
//  最小 IL2CPP / Unity 结构声明
// ==============================

struct System_String_o;
struct Utage_AssetFileUtage_o;
struct System_IO_BinaryReader_o;
struct AdvSystemSaveData_o;

struct System_String_Fields
{
    int32_t _stringLength;
    wchar_t _firstChar;
};

struct System_String_o
{
    void* klass;
    void* monitor;
    System_String_Fields fields;
};

struct AdvSystemSaveData_o
{
    void* klass;
    void* monitor;
};

struct System_IO_BinaryReader_o
{
    void* klass;
    void* monitor;
};

using il2cpp_string_new_utf16_t = System_String_o* (*)(const wchar_t*, int);
static il2cpp_string_new_utf16_t g_il2cpp_string_new_utf16 = nullptr;
static HMODULE g_gameAssembly = nullptr;
static HANDLE g_logFile = INVALID_HANDLE_VALUE;
static const wchar_t* kFallbackVoicePath = L"starveling/sound/voice/方知宥/21_方知宥_20";
static const char* kModPlotMapId = "map_my_01";
static const wchar_t* kPrimaryAllowedScenarioSheet = L"记-第一回";

static void InitLog()
{
    if (g_logFile != INVALID_HANDLE_VALUE) return;

    wchar_t dllPath[MAX_PATH]{};
    GetModuleFileNameW((HMODULE)&__ImageBase, dllPath, MAX_PATH);

    std::wstring path = dllPath;
    auto pos = path.find_last_of(L"\\/");
    std::wstring logPath = (pos == std::wstring::npos)
        ? L"voice_hook.log"
        : (path.substr(0, pos + 1) + L"voice_hook.log");

    g_logFile = CreateFileW(
        logPath.c_str(),
        FILE_APPEND_DATA,
        FILE_SHARE_READ,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr
    );
}

static void Log(const std::wstring& s)
{
    InitLog();
    if (g_logFile == INVALID_HANDLE_VALUE) return;

    std::wstring line = s + L"\r\n";
    DWORD written = 0;
    WriteFile(g_logFile, line.c_str(), (DWORD)(line.size() * sizeof(wchar_t)), &written, nullptr);
}

static std::wstring ToWString(System_String_o* s)
{
    if (!s) return L"";
    return std::wstring(&s->fields._firstChar, s->fields._stringLength);
}

static bool HasLocalMark(const std::wstring& s)
{
    return s.find(L"L:/") != std::wstring::npos ||
           s.find(L"L:\\") != std::wstring::npos ||
           s.find(L"L:") != std::wstring::npos ||
           s.find(L"ext:") != std::wstring::npos;
}

static bool IsBlockedExternalVoicePath(const std::wstring& s)
{
    return HasLocalMark(s) ||
           s.find(L"Starveling/Sound/Voice/ShiyiAssets_List\\Voice\\") != std::wstring::npos ||
           s.find(L"Starveling/Sound/Voice/ShiyiAssets_List/Voice/") != std::wstring::npos;
}

static std::wstring ToLowerCopy(const std::wstring& s)
{
    std::wstring out = s;
    std::transform(out.begin(), out.end(), out.begin(), [](wchar_t ch) {
        return (wchar_t)towlower(ch);
    });
    return out;
}

static const std::vector<std::wstring>& GetScenarioSheetNames()
{
    static const std::vector<std::wstring> kNames = {
        L"记-第一回", L"忆-第一回",
        L"记-第二回", L"忆-第二回",
        L"记-第三回", L"忆-第三回",
        L"记-第四回", L"忆-第四回",
        L"记-第五回", L"忆-第五回",
        L"记-第六回", L"忆-第六回",
        L"记-第七回", L"忆-第七回",
        L"记-第八回", L"忆-第八回",
        L"记-第九回", L"忆-第九回",
        L"记-第十回", L"忆-第十回",
        L"记-第十一回", L"忆-第十一回",
        L"记-第十二回", L"忆-第十二回",
        L"记-第十三回", L"忆-第十三回",
        L"记-第十四回", L"忆-第十四回",
        L"记-第十五回",
        L"记-第十六回",
        L"记-第十七回",
        L"记-第十八回一", L"记-第十八回二", L"记-第十八回三", L"记-第十八回四",
        L"记-第十九回一", L"记-第十九回二", L"记-第十九回三",
        L"死亡结局",
        L"结局-寻闯", L"结局-出家", L"结局-远舟", L"结局-新朝",
        L"结局-哭庙", L"结局-通海", L"结局-烬志", L"结局-殉情",
        L"聊斋", L"红楼", L"番外"
    };
    return kNames;
}

static bool IsAllowedScenarioSheet(const std::wstring& sheetName)
{
    return sheetName == kPrimaryAllowedScenarioSheet;
}

static bool TryRewriteScenarioSheetPath(const std::wstring& input, std::wstring& output, std::wstring& hitSheet)
{
    const auto& sheetNames = GetScenarioSheetNames();
    for (const auto& sheet : sheetNames)
    {
        size_t pos = input.find(sheet);
        if (pos == std::wstring::npos)
            continue;

        hitSheet = sheet;
        if (IsAllowedScenarioSheet(sheet))
            return false;

        output = input;
        output.replace(pos, sheet.size(), kPrimaryAllowedScenarioSheet);
        return true;
    }
    return false;
}

static std::wstring StripLocalMark(const std::wstring& s)
{
    size_t pos = s.find(L"ext:");
    size_t len = 4;

    if (pos == std::wstring::npos)
    {
        pos = s.find(L"L:/");
        len = 3;
    }
    if (pos == std::wstring::npos)
    {
        pos = s.find(L"L:\\");
        len = 3;
    }
    if (pos == std::wstring::npos)
    {
        pos = s.find(L"L:");
        len = 2;
    }
    if (pos == std::wstring::npos)
        return s;

    std::wstring out = s.substr(0, pos) + s.substr(pos + len);
    while (!out.empty() && (out[0] == L'/' || out[0] == L'\\'))
        out.erase(out.begin());
    return out;
}

static System_String_o* NewString(const std::wstring& s)
{
    if (!g_il2cpp_string_new_utf16) return nullptr;
    return g_il2cpp_string_new_utf16(s.c_str(), (int)s.size());
}

using ParseLoadPath_t = System_String_o* (*)(Utage_AssetFileUtage_o* __this, void* method);
using LoadResource_t  = void (*)(Utage_AssetFileUtage_o* __this, System_String_o* loadPath, void* onComplete, void* onFailed, void* method);
using ReadBinary_t    = void (*)(AdvSystemSaveData_o* __this, System_IO_BinaryReader_o* reader, void* method);

static ParseLoadPath_t g_origParseLoadPath = nullptr;
static LoadResource_t  g_origLoadResource = nullptr;
static ReadBinary_t    g_origReadBinary = nullptr;

static uintptr_t RVA(uintptr_t rva)
{
    return reinterpret_cast<uintptr_t>(g_gameAssembly) + rva;
}

// ==============================
//  PlotMap 注入骨架
//  说明：这里先做“放行原函数 + 定位宿主 + 记录日志”的稳定版，
//  真正往 plotMapData 尾部追加结构时，需要你后续继续补充
//  AdvPlotMapSaveData 的真实容器布局与元素布局。
// ==============================

static constexpr ptrdiff_t kOffsetPlotMapData = 0x60;
static constexpr ptrdiff_t kOffsetMainThemeIndex = 0x84;

static void* GetPlotMapDataHost(AdvSystemSaveData_o* instance)
{
    if (!instance) return nullptr;
    return *reinterpret_cast<void**>(reinterpret_cast<uint8_t*>(instance) + kOffsetPlotMapData);
}

static int GetMainThemeIndex(AdvSystemSaveData_o* instance)
{
    if (!instance) return -1;
    return *reinterpret_cast<int*>(reinterpret_cast<uint8_t*>(instance) + kOffsetMainThemeIndex);
}

static void TryInjectPlotMapStub(AdvSystemSaveData_o* instance)
{
    void* plotMapData = GetPlotMapDataHost(instance);
    int mainThemeIndex = GetMainThemeIndex(instance);

    wchar_t buffer[512]{};
    wsprintfW(
        buffer,
        L"[ReadBinary] post-call stub | plotMapData=%p | mainThemeIndex=%d | pendingMapId=%S",
        plotMapData,
        mainThemeIndex,
        kModPlotMapId
    );
    Log(buffer);

    if (!plotMapData)
    {
        Log(L"[ReadBinary] plotMapData is null, skip inject");
        return;
    }

    // 这里故意先不直接写内存：
    // 1. 你已经确认宿主偏移，但 AdvPlotMapSaveData 内部真实容器（List/Dictionary/Array）
    //    还需要进一步明确，否则容易脏写；
    // 2. 先把 hook 链路和时机跑通，再根据下一轮逆向补充 AppendPlotMapState() 即可。
}

static System_String_o* Hook_ParseLoadPath(Utage_AssetFileUtage_o* __this, void* method)
{
    auto ret = g_origParseLoadPath(__this, method);
    std::wstring oldPath = ToWString(ret);

    if (HasLocalMark(oldPath))
    {
        std::wstring newPath = StripLocalMark(oldPath);
        Log(L"[ParseLoadPath] external path redirected to fallback | old=" + oldPath + L" | normalized=" + newPath + L" | fallback=" + std::wstring(kFallbackVoicePath));

        auto fallback = NewString(kFallbackVoicePath);
        return fallback ? fallback : ret;
    }

    std::wstring rewritten;
    std::wstring hitSheet;
    if (TryRewriteScenarioSheetPath(oldPath, rewritten, hitSheet))
    {
        Log(L"[ParseLoadPath] scenario sheet redirected | hit=" + hitSheet + L" | old=" + oldPath + L" | new=" + rewritten);
        auto replacement = NewString(rewritten);
        return replacement ? replacement : ret;
    }

    return ret;
}

static void Hook_LoadResource(Utage_AssetFileUtage_o* __this, System_String_o* loadPath, void* onComplete, void* onFailed, void* method)
{
    std::wstring oldPath = ToWString(loadPath);
    if (IsBlockedExternalVoicePath(oldPath))
    {
        std::wstring newPath = StripLocalMark(oldPath);
        Log(L"[LoadResource] external path redirected to fallback | old=" + oldPath + L" | normalized=" + newPath + L" | fallback=" + std::wstring(kFallbackVoicePath));

        auto fallback = NewString(kFallbackVoicePath);
        g_origLoadResource(__this, fallback ? fallback : loadPath, onComplete, onFailed, method);
        return;
    }

    std::wstring rewritten;
    std::wstring hitSheet;
    if (TryRewriteScenarioSheetPath(oldPath, rewritten, hitSheet))
    {
        Log(L"[LoadResource] scenario sheet redirected | hit=" + hitSheet + L" | old=" + oldPath + L" | new=" + rewritten);
        auto replacement = NewString(rewritten);
        g_origLoadResource(__this, replacement ? replacement : loadPath, onComplete, onFailed, method);
        return;
    }

    g_origLoadResource(__this, loadPath, onComplete, onFailed, method);
}

static void Hook_ReadBinary(AdvSystemSaveData_o* __this, System_IO_BinaryReader_o* reader, void* method)
{
    g_origReadBinary(__this, reader, method);
    TryInjectPlotMapStub(__this);
}

static bool InitApi()
{
    g_gameAssembly = GetModuleHandleW(L"GameAssembly.dll");
    if (!g_gameAssembly)
    {
        Log(L"[Init] GameAssembly.dll not found");
        return false;
    }

    g_il2cpp_string_new_utf16 = reinterpret_cast<il2cpp_string_new_utf16_t>(
        GetProcAddress(g_gameAssembly, "il2cpp_string_new_utf16")
    );

    if (!g_il2cpp_string_new_utf16)
    {
        Log(L"[Init] il2cpp_string_new_utf16 not found");
        return false;
    }

    return true;
}

static bool InstallHooks()
{
    if (MH_Initialize() != MH_OK)
    {
        Log(L"[Hook] MH_Initialize failed");
        return false;
    }

    void* parseAddr = reinterpret_cast<void*>(RVA(0x3BCE40));
    void* loadResAddr = reinterpret_cast<void*>(RVA(0x3C3910));
    void* readBinaryAddr = reinterpret_cast<void*>(RVA(0x4FAB50));

    if (MH_CreateHook(loadResAddr, &Hook_LoadResource, reinterpret_cast<void**>(&g_origLoadResource)) != MH_OK)
    {
        Log(L"[Hook] Create LoadResource failed");
        return false;
    }
    if (MH_EnableHook(loadResAddr) != MH_OK)
    {
        Log(L"[Hook] Enable LoadResource failed");
        return false;
    }
    Log(L"[Hook] LoadResource enabled");

    if (MH_CreateHook(parseAddr, &Hook_ParseLoadPath, reinterpret_cast<void**>(&g_origParseLoadPath)) == MH_OK)
    {
        if (MH_EnableHook(parseAddr) == MH_OK)
            Log(L"[Hook] ParseLoadPath enabled");
        else
            Log(L"[Hook] ParseLoadPath enable failed");
    }
    else
    {
        Log(L"[Hook] ParseLoadPath create failed");
    }

    if (MH_CreateHook(readBinaryAddr, &Hook_ReadBinary, reinterpret_cast<void**>(&g_origReadBinary)) == MH_OK)
    {
        if (MH_EnableHook(readBinaryAddr) == MH_OK)
            Log(L"[Hook] AdvSystemSaveData.ReadBinary enabled");
        else
            Log(L"[Hook] AdvSystemSaveData.ReadBinary enable failed");
    }
    else
    {
        Log(L"[Hook] AdvSystemSaveData.ReadBinary create failed");
    }

    return true;
}

static DWORD WINAPI MainThread(LPVOID)
{
    Log(L"[Init] DLL loaded");

    for (int i = 0; i < 200; ++i)
    {
        g_gameAssembly = GetModuleHandleW(L"GameAssembly.dll");
        if (g_gameAssembly) break;
        Sleep(100);
    }

    if (!InitApi())
    {
        Log(L"[Init] InitApi failed");
        return 0;
    }

    if (!InstallHooks())
    {
        Log(L"[Init] InstallHooks failed");
        return 0;
    }

    Log(L"[Init] all ok");
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);
        CreateThread(nullptr, 0, MainThread, nullptr, 0, nullptr);
    }
    return TRUE;
}
