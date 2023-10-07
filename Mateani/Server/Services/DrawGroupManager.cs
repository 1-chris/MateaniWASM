using System.Collections.Concurrent;

namespace Mateani.Server.Services;

public class DrawGroupManager
{
    public ConcurrentDictionary<string, HashSet<string>> Groups { get; set; } = new();

    public void AddToGroup(string connectionId, string groupName)
    {
        Groups.AddOrUpdate(groupName, _ => new HashSet<string> { connectionId }, (_, set) =>
        {
            set.Add(connectionId);
            return set;
        });
    }

    public void RemoveFromGroup(string connectionId, string groupName)
    {
        if (Groups.TryGetValue(groupName, out var connections))
        {
            connections.Remove(connectionId);

            if (connections.Count == 0)
            {
                Groups.TryRemove(groupName, out _);
            }
        }
    }

    public IEnumerable<string> GetGroups() => Groups.Keys;
}