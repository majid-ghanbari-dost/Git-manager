# Windows CI validation

The initial Windows runner exposed missing explicit `System.IO` imports in the WPF application. The source was corrected by adding `GlobalUsings.cs` with a global `System.IO` using.

The corrected commit is `0930ecb4ebd3956c808ca0e777ff750143f595ed`.

Acceptance remains pending until a Windows Actions run for the corrected branch head completes restore, build, tests, and publish successfully. A failed or stale run must not be treated as validation.
