# VipList

`VipList.dll` owns one server-synchronized VIP list for all mods.

## Using it from another mod

Add `VipList.dll` as an assembly reference, declare the dependency, and query exact
platform IDs through the API:

```csharp
[BepInDependency(VipList.VipListPlugin.PluginGuid)]
public class MyPlugin : BaseUnityPlugin
{
    private bool HasVipAccess()
        => VipList.VipListApi.IsLocalPlayerVip();
}
```

For a known player ID, call `VipListApi.IsVip(platformUserId)` instead.

The server owns `Detalhes.VipList.cfg`. On its first run, it imports the old
`Detalhes.Deadheim.cfg` VIP value automatically. `VipList` accepts IDs separated by spaces,
commas, semicolons, pipes, or new lines. Matching is exact and case-insensitive.

Use `VipListApi.Changed` if a mod caches VIP-dependent state and needs to refresh it
when the synchronized configuration changes.
