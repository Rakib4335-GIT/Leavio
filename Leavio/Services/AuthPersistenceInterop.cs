using Blazored.SessionStorage;
using Microsoft.JSInterop;

namespace Leavio.Services;

/// <summary>Syncs auth state between sessionStorage and localStorage for Remember Me.</summary>
public static class AuthPersistenceInterop
{
    public static async Task PersistAuthToLocalAsync(IJSRuntime js, IReadOnlyDictionary<string, string> values)
    {
        foreach (var kv in values)
            await js.InvokeVoidAsync("localStorage.setItem", kv.Key, kv.Value);
    }

    public static async Task ClearPersistedAuthAsync(IJSRuntime js)
    {
        foreach (var key in AuthSessionKeys.All)
            await js.InvokeVoidAsync("localStorage.removeItem", key);
    }

    /// <summary>When session is empty but user chose Remember Me earlier, copy localStorage → sessionStorage.</summary>
    public static async Task RestoreSessionFromLocalIfNeededAsync(IJSRuntime js, ISessionStorageService session)
    {
        var sessionName = await session.GetItemAsync<string>(AuthSessionKeys.AdminName);
        if (!string.IsNullOrEmpty(sessionName))
            return;

        var localName = await js.InvokeAsync<string?>("localStorage.getItem", AuthSessionKeys.AdminName);
        if (string.IsNullOrWhiteSpace(localName))
            return;

        foreach (var key in AuthSessionKeys.All)
        {
            var v = await js.InvokeAsync<string?>("localStorage.getItem", key);
            if (v != null)
                await session.SetItemAsync(key, v);
        }
    }

    public static async Task<bool> HasPersistedAuthAsync(IJSRuntime js)
    {
        var id = await js.InvokeAsync<string?>("localStorage.getItem", AuthSessionKeys.AdminId);
        return !string.IsNullOrWhiteSpace(id);
    }

    public static async Task SyncRoleToLocalAsync(IJSRuntime js, string role)
    {
        if (!await HasPersistedAuthAsync(js))
            return;
        await js.InvokeVoidAsync("localStorage.setItem", AuthSessionKeys.AdminRole, role);
    }
}
