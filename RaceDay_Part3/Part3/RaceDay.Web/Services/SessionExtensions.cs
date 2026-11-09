using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public static class SessionKeys
{
    public const string User = "User";
    public const string ApiCookie = "ApiCookie";
}

/// <summary>Helpers for keeping the signed-in user in the web session.</summary>
public static class SessionExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static void SetObject<T>(this ISession session, string key, T value) =>
        session.SetString(key, JsonSerializer.Serialize(value, Options));

    public static T? GetObject<T>(this ISession session, string key)
    {
        var json = session.GetString(key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, Options);
    }

    public static SessionUser? GetUser(this ISession session) => session.GetObject<SessionUser>(SessionKeys.User);
}
