# LogicGarden study books

The existing `StudyShelfZone` topic cards open the existing `StudyLibraryController` reader. The saved LogicGarden scene disables raycasts on the fully transparent `Gameplay_Interface` background; its child controls retain their own raycasts. The reader uses a nested modal Canvas above the HUD, within the existing Canvas and EventSystem.

Topic-card positions and Cinemachine camera transforms are unchanged. The catalog contains 3 / 2 / 3 / 2 topics across PRELIM / MIDTERMS / PRE-FINALS / FINALS.

## Lesson content

`Assets/Resources/StudyLibrary/<topic>.json` maps each book page to a rendered original slide and its exact deck/slide attribution. The 240 pages include every slide from the eleven supplied decks, preserving equations, tables, examples, diagrams, original wording, and sequence. Week 3 and Week 4 are combined in Rules of Inference. Induction remains on the requested PRE-FINALS shelf while retaining its actual Midterm Weeks 8–9 source attribution.

The reader displays two source slides per spread. Click either page for an enlarged view; click it again to return. Previous/Next turn the spread and close enlargement. BACK closes the reader and returns to the focused shelf. Only the visible spread's textures are retained; closing or changing spreads releases them.

To regenerate from the source documents on Windows with PowerPoint installed:

```powershell
./Tools/StudyLibrary/Export-Lessons.ps1 -SourceDirectory 'C:/path/to/DiscreetMath'
```

The exporter opens source documents read-only and disables PPTM macro execution. It does not change the source decks.

## Checks

Run `validate_lessons.py` with Python and Pillow to verify every source slide has exactly one corresponding page, each topic uses its own resources, image files are intact, and deck order is preserved:

```powershell
python Tools/StudyLibrary/validate_lessons.py 'C:/path/to/DiscreetMath'
```

In Unity Play mode, approach a shelf, wait for its camera to settle, and choose **Logic Legends > Validate Focused Study Shelf**. This exercises the current EventSystem raycast results and pointer handlers for every topic card in that shelf, every source page, Previous/Next (including disabled boundaries), page enlargement, Back, and reopening a different topic. It deliberately fails if another UI object blocks a click. Repeat for all four shelves.

For a visual check, click the two MIDTERMS cards separately, turn to their teaching/example slides, enlarge a page, and return with BACK. Repeat with Propositional Logic, Sets, and Graphs. Leaving the shelf should restore the existing gameplay camera and cursor state.
