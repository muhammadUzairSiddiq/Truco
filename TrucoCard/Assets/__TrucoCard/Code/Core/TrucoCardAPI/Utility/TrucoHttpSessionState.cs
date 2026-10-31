using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

/// <summary>Name-keyed cookies for the client's single configured API session.</summary>
public sealed class TrucoSessionCookieJar
{
    sealed class Cookie { public string name, value; public DateTimeOffset? expires; }
    readonly Dictionary<string, Cookie> _cookies = new Dictionary<string, Cookie>(StringComparer.OrdinalIgnoreCase);

    public string Value(string name) => _cookies.TryGetValue(name, out var cookie) ? cookie.value : null;
    public string Header
    {
        get
        {
            var parts = new List<string>();
            foreach (var cookie in _cookies.Values) parts.Add(cookie.name + "=" + cookie.value);
            return string.Join("; ", parts);
        }
    }
    public void Clear() => _cookies.Clear();

    public List<string> Prune(DateTimeOffset now)
    {
        var expired = new List<string>();
        foreach (var pair in _cookies)
            if (pair.Value.expires.HasValue && pair.Value.expires.Value <= now) expired.Add(pair.Key);
        foreach (var name in expired) _cookies.Remove(name);
        return expired;
    }

    public List<string> Merge(string header, DateTimeOffset now)
    {
        var changed = Prune(now);
        if (string.IsNullOrWhiteSpace(header)) return changed;
        foreach (string raw in Split(header))
        {
            var parts = raw.Split(';');
            int equal = parts[0].IndexOf('=');
            if (equal <= 0) continue;
            string name = parts[0].Substring(0, equal).Trim();
            string value = parts[0].Substring(equal + 1).Trim();
            if (!ValidName(name) || HasControl(value)) continue;
            DateTimeOffset? expires = null;
            long? maxAge = null;
            for (int index = 1; index < parts.Length; index++)
            {
                int separator = parts[index].IndexOf('=');
                if (separator < 0) continue;
                string attribute = parts[index].Substring(0, separator).Trim();
                string argument = parts[index].Substring(separator + 1).Trim();
                if (attribute.Equals("Max-Age", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds)) maxAge = seconds;
                else if (attribute.Equals("Expires", StringComparison.OrdinalIgnoreCase)
                    && DateTimeOffset.TryParse(argument, CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var date)) expires = date;
            }
            if (maxAge.HasValue)
            {
                if (maxAge.Value <= 0) expires = now;
                else
                {
                    try { expires = now.AddSeconds(maxAge.Value); }
                    catch (ArgumentOutOfRangeException) { expires = DateTimeOffset.MaxValue; }
                }
            }
            changed.Add(name);
            if (expires.HasValue && expires.Value <= now) _cookies.Remove(name);
            else _cookies[name] = new Cookie { name = name, value = value, expires = expires };
        }
        return changed;
    }

    static IEnumerable<string> Split(string header)
    {
        int start = 0;
        bool quoted = false;
        for (int index = 0; index < header.Length; index++)
        {
            char value = header[index];
            if (value == '"') quoted = !quoted;
            if (quoted) continue;
            bool boundary = value == '\r' || value == '\n';
            if (value == ',')
            {
                int next = index + 1;
                while (next < header.Length && char.IsWhiteSpace(header[next])) next++;
                int name = next;
                while (next < header.Length && NameCharacter(header[next])) next++;
                while (next < header.Length && header[next] == ' ') next++;
                boundary = next > name && next < header.Length && header[next] == '=';
            }
            if (!boundary) continue;
            if (index > start) yield return header.Substring(start, index - start).Trim();
            start = index + 1;
        }
        if (start < header.Length) yield return header.Substring(start).Trim();
    }
    static bool NameCharacter(char value) => value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z'
        || value >= '0' && value <= '9' || "!#$%&'*+-.^_`|~".IndexOf(value) >= 0;
    static bool ValidName(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (char item in value) if (!NameCharacter(item)) return false;
        return true;
    }
    static bool HasControl(string value)
    {
        foreach (char item in value) if (char.IsControl(item)) return true;
        return false;
    }
}

/// <summary>One refresh operation per active session; obsolete completions cannot revive a logged-out session.</summary>
public sealed class TrucoSessionRefreshGate
{
    readonly object _sync = new object();
    int _generation = -1;
    TaskCompletionSource<bool> _pending;

    public Task<bool> Run(int generation, Func<Task<bool>> refresh, Func<int> currentGeneration)
    {
        TaskCompletionSource<bool> completion;
        lock (_sync)
        {
            if (_generation == generation && _pending != null) return _pending.Task;
            _generation = generation;
            completion = new TaskCompletionSource<bool>();
            _pending = completion;
        }
        _ = Complete(generation, refresh, currentGeneration, completion);
        return completion.Task;
    }

    public void Reset()
    {
        TaskCompletionSource<bool> obsolete;
        lock (_sync) { obsolete = _pending; _pending = null; _generation = -1; }
        obsolete?.TrySetResult(false);
    }

    async Task Complete(int generation, Func<Task<bool>> refresh, Func<int> currentGeneration, TaskCompletionSource<bool> completion)
    {
        bool refreshed = false;
        try { refreshed = await refresh() && generation == currentGeneration(); }
        catch { refreshed = false; }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_pending, completion)) { _pending = null; _generation = -1; }
            }
            completion.TrySetResult(refreshed);
        }
    }
}
