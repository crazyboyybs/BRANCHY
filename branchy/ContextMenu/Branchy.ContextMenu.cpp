#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <appmodel.h>

#include <shellapi.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <shobjidl_core.h>
#include <strsafe.h>
#include <wrl.h>
#include <wrl/module.h>

#include <array>
#include <cstring>
#include <cwchar>
#include <string>

#include "resource.h"

using Microsoft::WRL::ClassicCom;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::InProc;
using Microsoft::WRL::Module;
using Microsoft::WRL::RuntimeClass;
using Microsoft::WRL::RuntimeClassFlags;

namespace
{
constexpr wchar_t kRepositoryArgumentPrefix[] = L"--repository=";
constexpr wchar_t kCanonicalVerb[] = L"branchy.open";

HMODULE g_module = nullptr;

HRESULT LoadLocalizedString(UINT resourceId, PWSTR* value)
{
    if (value == nullptr)
    {
        return E_POINTER;
    }

    *value = nullptr;
    std::array<wchar_t, 256> buffer{};
    int length = LoadStringW(g_module, resourceId, buffer.data(),
                             static_cast<int>(buffer.size()));
    if (length > 0)
    {
        return SHStrDupW(buffer.data(), value);
    }

    const DWORD error = GetLastError();
    return HRESULT_FROM_WIN32(
        error == ERROR_SUCCESS ? ERROR_RESOURCE_NAME_NOT_FOUND : error);
}

HRESULT GetFolderPath(IShellItemArray* items, std::wstring& folderPath)
{
    if (items == nullptr)
    {
        return E_INVALIDARG;
    }

    ComPtr<IShellItem> item;
    HRESULT result = items->GetItemAt(0, &item);
    if (FAILED(result))
    {
        return result;
    }

    PWSTR rawPath = nullptr;
    result = item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath);
    if (SUCCEEDED(result))
    {
        folderPath.assign(rawPath);
    }
    CoTaskMemFree(rawPath);
    return result;
}

HRESULT GetFolderPath(IUnknown* site, std::wstring& folderPath)
{
    if (site == nullptr)
    {
        return E_INVALIDARG;
    }

    ComPtr<IFolderView> folderView;
    HRESULT result = IUnknown_QueryService(
        site,
        SID_SFolderView,
        IID_PPV_ARGS(&folderView));
    if (FAILED(result))
    {
        return result;
    }

    ComPtr<IPersistFolder2> persistFolder;
    result = folderView->GetFolder(IID_PPV_ARGS(&persistFolder));
    if (FAILED(result))
    {
        return result;
    }

    PIDLIST_ABSOLUTE folderIdList = nullptr;
    result = persistFolder->GetCurFolder(&folderIdList);
    if (FAILED(result))
    {
        return result;
    }

    std::array<wchar_t, 32768> path{};
    BOOL converted = SHGetPathFromIDListEx(
        folderIdList,
        path.data(),
        static_cast<DWORD>(path.size()),
        GPFIDL_DEFAULT);
    CoTaskMemFree(folderIdList);
    if (!converted)
    {
        const DWORD error = GetLastError();
        return HRESULT_FROM_WIN32(
            error == ERROR_SUCCESS ? ERROR_PATH_NOT_FOUND : error);
    }

    folderPath.assign(path.data());
    return S_OK;
}

HRESULT ResolveFolderPath(
    IShellItemArray* items,
    IUnknown* site,
    std::wstring& folderPath)
{
    HRESULT result = GetFolderPath(items, folderPath);
    return SUCCEEDED(result) ? result : GetFolderPath(site, folderPath);
}

bool PathsEqual(PCWSTR first, PCWSTR second)
{
    if (first == nullptr || second == nullptr)
    {
        return false;
    }

    std::wstring normalizedFirst(first);
    std::wstring normalizedSecond(second);
    while (normalizedFirst.size() > 3 &&
           (normalizedFirst.back() == L'\\' || normalizedFirst.back() == L'/'))
    {
        normalizedFirst.pop_back();
    }
    while (normalizedSecond.size() > 3 &&
           (normalizedSecond.back() == L'\\' || normalizedSecond.back() == L'/'))
    {
        normalizedSecond.pop_back();
    }

    return CompareStringOrdinal(
               normalizedFirst.c_str(),
               -1,
               normalizedSecond.c_str(),
               -1,
               TRUE) == CSTR_EQUAL;
}

bool IsKnownFolderPath(const std::wstring& path, REFKNOWNFOLDERID folderId)
{
    PWSTR knownFolderPath = nullptr;
    HRESULT result = SHGetKnownFolderPath(
        folderId,
        KF_FLAG_DONT_VERIFY,
        nullptr,
        &knownFolderPath);
    bool matches = SUCCEEDED(result) && PathsEqual(path.c_str(), knownFolderPath);
    CoTaskMemFree(knownFolderPath);
    return matches;
}

bool IsDesktopFolder(const std::wstring& path)
{
    return IsKnownFolderPath(path, FOLDERID_Desktop) ||
           IsKnownFolderPath(path, FOLDERID_PublicDesktop);
}

HRESULT ActivateApplicationForFolder(const std::wstring& folderPath)
{
    if (folderPath.empty())
    {
        return E_INVALIDARG;
    }

    // Resolve identity from this DLL's installed package, including its publisher.
    std::array<wchar_t, 32768> modulePath{};
    DWORD length = GetModuleFileNameW(g_module, modulePath.data(),
        static_cast<DWORD>(modulePath.size()));
    if (length == 0 || length >= modulePath.size())
    {
        return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
    }
    std::wstring packagePath(modulePath.data());
    for (int index = 0; index < 2; ++index)
    {
        const auto separator = packagePath.find_last_of(L"\\/");
        if (separator == std::wstring::npos) return E_UNEXPECTED;
        packagePath.resize(separator);
    }
    const auto separator = packagePath.find_last_of(L"\\/");
    std::wstring packageName = packagePath.substr(separator + 1);
    std::array<wchar_t, PACKAGE_FAMILY_NAME_MAX_LENGTH + 1> familyName{};
    UINT32 familyLength = static_cast<UINT32>(familyName.size());
    LONG identityResult = PackageFamilyNameFromFullName(packageName.c_str(),
        &familyLength, familyName.data());
    if (identityResult != ERROR_SUCCESS) return HRESULT_FROM_WIN32(identityResult);
    std::wstring applicationId(familyName.data());
    applicationId.append(L"!App");

    ComPtr<IApplicationActivationManager> activationManager;
    HRESULT result = CoCreateInstance(
        CLSID_ApplicationActivationManager,
        nullptr,
        CLSCTX_LOCAL_SERVER,
        IID_PPV_ARGS(&activationManager));
    if (FAILED(result))
    {
        return result;
    }

    std::wstring arguments(kRepositoryArgumentPrefix);
    arguments.append(folderPath);
    // The grant is consumed by the immediate packaged-app activation and expires
    // on the next foreground-changing user input.
    AllowSetForegroundWindow(ASFW_ANY);
    DWORD processId = 0;
    result = activationManager->ActivateApplication(
        applicationId.c_str(),
        arguments.c_str(),
        AO_NONE,
        &processId);
    if (SUCCEEDED(result) && processId != 0)
    {
        AllowSetForegroundWindow(processId);
    }

    return result;
}

HRESULT GetModuleIconReference(PWSTR* iconReference)
{
    if (iconReference == nullptr)
    {
        return E_POINTER;
    }

    *iconReference = nullptr;
    std::array<wchar_t, 32768> modulePath{};
    DWORD length = GetModuleFileNameW(
        g_module,
        modulePath.data(),
        static_cast<DWORD>(modulePath.size()));
    if (length == 0 || length >= modulePath.size())
    {
        const DWORD error = GetLastError();
        return HRESULT_FROM_WIN32(
            error == ERROR_SUCCESS ? ERROR_INSUFFICIENT_BUFFER : error);
    }

    DWORD appsUseLightTheme = 1;
    DWORD valueSize = sizeof(appsUseLightTheme);
    RegGetValueW(
        HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
        L"AppsUseLightTheme",
        RRF_RT_REG_DWORD,
        nullptr,
        &appsUseLightTheme,
        &valueSize);

    const UINT iconResource = appsUseLightTheme != 0
        ? IDI_BRANCHY_DARK
        : IDI_BRANCHY_LIGHT;
    std::wstring reference(modulePath.data(), length);
    reference.append(L",-");
    reference.append(std::to_wstring(iconResource));
    return SHStrDupW(reference.c_str(), iconReference);
}

class StgMedium final
{
public:
    STGMEDIUM value{};

    ~StgMedium()
    {
        if (value.tymed != TYMED_NULL)
        {
            ReleaseStgMedium(&value);
        }
    }
};
} // namespace

class __declspec(uuid("270AA4B9-EDB6-4CB3-9A7D-5FDC6777F821"))
    ModernExplorerCommand final
    : public RuntimeClass<
          RuntimeClassFlags<ClassicCom>,
          IExplorerCommand,
          IObjectWithSite>
{
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        return LoadLocalizedString(IDS_OPEN_BRANCHY, title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        return GetModuleIconReference(icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
    {
        return LoadLocalizedString(IDS_OPEN_BRANCHY, tooltip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* canonicalName) override
    {
        if (canonicalName == nullptr)
        {
            return E_POINTER;
        }

        *canonicalName = __uuidof(ModernExplorerCommand);
        return S_OK;
    }

    IFACEMETHODIMP GetState(
        IShellItemArray* items,
        BOOL,
        EXPCMDSTATE* state) override
    {
        if (state == nullptr)
        {
            return E_POINTER;
        }

        std::wstring folderPath;
        HRESULT result = ResolveFolderPath(items, _site.Get(), folderPath);
        *state = SUCCEEDED(result) && IsDesktopFolder(folderPath)
            ? ECS_HIDDEN
            : ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        std::wstring folderPath;
        HRESULT result = ResolveFolderPath(items, _site.Get(), folderPath);
        if (FAILED(result) || IsDesktopFolder(folderPath))
        {
            return FAILED(result) ? result : E_ACCESSDENIED;
        }

        return ActivateApplicationForFolder(folderPath);
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (flags == nullptr)
        {
            return E_POINTER;
        }

        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        if (commands == nullptr)
        {
            return E_POINTER;
        }

        *commands = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP SetSite(IUnknown* site) override
    {
        _site = site;
        return S_OK;
    }

    IFACEMETHODIMP GetSite(REFIID interfaceId, void** site) override
    {
        if (site == nullptr)
        {
            return E_POINTER;
        }

        *site = nullptr;
        return _site ? _site.CopyTo(interfaceId, site) : E_FAIL;
    }

private:
    ComPtr<IUnknown> _site;
};

class __declspec(uuid("D0F9EDEE-9883-48FA-842B-6EBDA63D9AB4"))
    ClassicContextMenu final
    : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IShellExtInit, IContextMenu>
{
public:
    IFACEMETHODIMP Initialize(
        PCIDLIST_ABSOLUTE folder,
        IDataObject* dataObject,
        HKEY) override
    {
        _folderPath.clear();
        if (dataObject != nullptr)
        {
            FORMATETC format{
                static_cast<CLIPFORMAT>(CF_HDROP),
                nullptr,
                DVASPECT_CONTENT,
                -1,
                TYMED_HGLOBAL,
            };
            StgMedium medium;
            HRESULT result = dataObject->GetData(&format, &medium.value);
            if (SUCCEEDED(result))
            {
                HDROP drop = static_cast<HDROP>(medium.value.hGlobal);
                if (drop != nullptr)
                {
                    UINT length = DragQueryFileW(drop, 0, nullptr, 0);
                    std::wstring path(length + 1, L'\0');
                    if (length > 0 && DragQueryFileW(drop, 0, path.data(), length + 1) > 0)
                    {
                        path.resize(length);
                        _folderPath = std::move(path);
                    }
                }
            }
        }

        if (_folderPath.empty() && folder != nullptr)
        {
            std::array<wchar_t, 32768> path{};
            if (SHGetPathFromIDListEx(
                    folder,
                    path.data(),
                    static_cast<DWORD>(path.size()),
                    GPFIDL_DEFAULT))
            {
                _folderPath.assign(path.data());
            }
        }

        return _folderPath.empty() ? E_FAIL : S_OK;
    }

    IFACEMETHODIMP QueryContextMenu(
        HMENU menu,
        UINT indexMenu,
        UINT commandIdFirst,
        UINT,
        UINT flags) override
    {
        if ((flags & CMF_DEFAULTONLY) != 0 ||
            _folderPath.empty() ||
            IsDesktopFolder(_folderPath))
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        PWSTR title = nullptr;
        HRESULT result = LoadLocalizedString(IDS_OPEN_BRANCHY, &title);
        if (FAILED(result))
        {
            return result;
        }

        BOOL inserted = InsertMenuW(
            menu,
            indexMenu,
            MF_BYPOSITION | MF_STRING,
            commandIdFirst,
            title);
        CoTaskMemFree(title);
        if (inserted)
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }

        const DWORD error = GetLastError();
        return HRESULT_FROM_WIN32(
            error == ERROR_SUCCESS ? ERROR_FUNCTION_FAILED : error);
    }

    IFACEMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* commandInfo) override
    {
        if (commandInfo == nullptr)
        {
            return E_POINTER;
        }

        bool isCommand = HIWORD(commandInfo->lpVerb) == 0 &&
                         LOWORD(commandInfo->lpVerb) == 0;
        if (!isCommand && HIWORD(commandInfo->lpVerb) != 0)
        {
            isCommand = std::strcmp(
                reinterpret_cast<const char*>(commandInfo->lpVerb),
                "branchy.open") == 0;
        }

        if (!isCommand && (commandInfo->fMask & CMIC_MASK_UNICODE) != 0)
        {
            auto extended = reinterpret_cast<CMINVOKECOMMANDINFOEX*>(commandInfo);
            if (HIWORD(extended->lpVerbW) != 0)
            {
                isCommand = std::wcscmp(extended->lpVerbW, kCanonicalVerb) == 0;
            }
        }

        if (!isCommand)
        {
            return E_FAIL;
        }

        return IsDesktopFolder(_folderPath)
            ? E_ACCESSDENIED
            : ActivateApplicationForFolder(_folderPath);
    }

    IFACEMETHODIMP GetCommandString(
        UINT_PTR commandOffset,
        UINT type,
        UINT*,
        PSTR name,
        UINT maximumLength) override
    {
        if (commandOffset != 0 || name == nullptr || maximumLength == 0)
        {
            return E_INVALIDARG;
        }

        if (type == GCS_VERBW)
        {
            return StringCchCopyW(
                reinterpret_cast<PWSTR>(name),
                maximumLength,
                kCanonicalVerb);
        }

        if (type == GCS_VERBA)
        {
            return StringCchCopyA(name, maximumLength, "branchy.open");
        }

        if (type == GCS_HELPTEXTW)
        {
            PWSTR title = nullptr;
            HRESULT result = LoadLocalizedString(IDS_OPEN_BRANCHY, &title);
            if (SUCCEEDED(result))
            {
                result = StringCchCopyW(
                    reinterpret_cast<PWSTR>(name),
                    maximumLength,
                    title);
            }
            CoTaskMemFree(title);
            return result;
        }

        return E_NOTIMPL;
    }

private:
    std::wstring _folderPath;
};

CoCreatableClass(ModernExplorerCommand);
CoCreatableClass(ClassicContextMenu);

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID classId, REFIID interfaceId, void** object)
{
    return Module<InProc>::GetModule().GetClassObject(classId, interfaceId, object);
}
