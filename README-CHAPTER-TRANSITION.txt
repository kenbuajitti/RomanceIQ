RomanceIQ — Ensemble picture during chapter transitions
October 22, 2026

APPLY TO YOUR EXISTING ROMANCEIQ GITHUB REPOSITORY
================================================
This is a small overlay patch, NOT a replacement Unity project.
It is based on RomanceIQ-FamilyRate-Style-Patch.zip (with Family Rate-style
full-width scenes, color-matched captions and text-only << < > >> arrows).

Contains only:
  Assets/Scripts/RomanceIQ/RomanceIQOnline.cs
  Assets/Scripts/RomanceIQ/RomanceIQController.cs
  Assets/Resources/RomanceIQ/VisualNovel/NextChapterEnsemble.png
  Assets/Resources/RomanceIQ/VisualNovel/NextChapterEnsemble.png.meta

Changes:
  - Replaces the plain 'WRITING YOUR NEXT CHAPTER' status page used while
    waiting for the shared Supabase story service with the approved ensemble.
  - Includes the exact caption in the artwork; does not overlay duplicate text.
  - Aspect FitInParent shows ALL the art, including the caption; plum bands fill
    extra space rather than cropping faces.
  - Loads while retrieving existing cached paths or generating fresh ones,
    including the handoff after decision 3 and later online decisions.
  - Keeps a MENU button accessible during loading.
  - A fast cached response still displays the screen for at least 0.8 seconds.
  - Returns to the normal scene/choices, or standard retry/error page.

INSTALL
1. Commit any current Unity project changes in GitHub Desktop first.
2. Close Unity, unzip this patch into a temporary folder.
3. In GitHub Desktop, choose Repository > Show in Explorer.
4. Copy this patch's Assets folder into the ROOT of your RomanceIQ repository.
5. Choose MERGE/REPLACE when prompted, never delete your existing Assets folder.
6. Reopen Unity and wait for imports, including NextChapterEnsemble.png.
7. Test the story from PLAY: the ensemble should appear at the transition
   after the third authored choice and every later online story fetch.
8. Check MENU during the wait, that the caption isn't cropped, and that
   the << < > >> reader controls return afterward.
9. If everything works, Commit and Push origin in GitHub Desktop.
10. Rebuild WebGL and re-upload to itch.io. GitHub alone doesn't update itch.io.

Untouched: analytics, Supabase function, SQL schema, persistence, story content,
character portraits, existing backgrounds, reader navigation/layout scripts.

Code was statically checked here, but a Unity Editor compile/play test and
WebGL build have NOT been run in this environment.
