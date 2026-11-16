# RomanceIQ Unity SceneFlow compile fix

Problem: CS0117 in Assets/Scripts/RomanceIQ/RomanceIQOnline.cs

Cause: A Phase A / missing-parent `RomanceIQOnline.cs` calls
`RomanceIQAnalytics.SceneFlow(int stage, string phase, string traceId, int durationMs, int httpStatus)`,
but the older `RomanceIQAnalytics.cs` source does not provide the method.

## Install
1. Back up your CURRENT `Assets/Scripts/RomanceIQ/RomanceIQAnalytics.cs`.
2. If your analytics class is still the original version, copy only
   `Assets/Scripts/RomanceIQ/RomanceIQAnalytics.cs` from this ZIP into the matching
   location in your Unity project, replacing the old .cs file.
   DO NOT replace `RomanceIQOnline.cs`, `story.json`, or any Supabase file.
3. Keep the existing `.cs.meta` file and Unity project folder.
4. Return to Unity and allow scripts to recompile. The five `CS0117` errors
   referencing `SceneFlow` should disappear.
5. Rebuild and test Stage 3->4 and Stage 4->5 after compilation.

If you've since customized your existing analytics class, do NOT replace
it wholesale. Instead insert the `SceneFlow` method below inside the existing
`RomanceIQAnalytics` class, directly after the `ChoiceMade` method.

## Analytics schema
`SceneFlow` is consent-gated and sends `romanceiq_scene_flow` with:
- `stage`: integer
- `phase`: string
- `trace_id`: string
- `duration_ms`: integer
- `http_status`: integer

Register the event with those property types in Unity Analytics if you want
the data to appear in dashboards. Schema registration is separate from fixing
Unity's C# compile errors.

## Method-only alternative
```csharp
public static void SceneFlow(int stage, string phase, string traceId,
    int durationMs, int httpStatus)
{
    if (!Allowed || !GetInstance().playing) return;
    Send("romanceiq_scene_flow", new CustomEvent("romanceiq_scene_flow")
    {
        { "stage", stage },
        { "phase", phase },
        { "trace_id", traceId ?? "" },
        { "duration_ms", Mathf.Max(0, durationMs) },
        { "http_status", Mathf.Max(0, httpStatus) }
    }, true);
}
```

This is a source-consistency repair; no live Unity build was compiled in the
working environment.
