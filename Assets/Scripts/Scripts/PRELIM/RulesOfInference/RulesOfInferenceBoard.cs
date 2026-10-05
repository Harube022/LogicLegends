// using System.Collections.Generic;
// using System.Linq;
// using TMPro;
// using UnityEngine;

// namespace LogicLegends.Inference
// {
//     /// <summary>Builds the drag puzzle inside the existing altar board and font style.</summary>
//     public sealed class RulesOfInferenceBoard : MonoBehaviour
//     {
//         public RectTransform premises;
//         public RectTransform wordBank;
//         public RectTransform ruleChoices;
//         public RectTransform dragLayer;
//         public TMP_FontAsset font;
//         public ConclusionReveal conclusion;
//         public RuleOfInferencePuzzle Puzzle { get; private set; }
//         public IReadOnlyList<DropZone> Zones => zones;
//         public IReadOnlyList<DraggableWord> Words => words;
//         public bool Solved { get; private set; }
//         readonly List<DropZone> zones = new List<DropZone>();
//         readonly List<DraggableWord> words = new List<DraggableWord>();
//         public bool PremisesCompleted { get; private set; }

//         public void Build(InferenceQuestion question, InferenceRule[] rules)
//         {
//             Clear(premises); Clear(wordBank); Clear(ruleChoices);
//             // A word may have been dragged over the board when the player exited.
//             Clear(dragLayer); zones.Clear(); words.Clear();
//             Puzzle = new RuleOfInferencePuzzle(question); Solved = false; PremisesCompleted = false;
//             wordBank.gameObject.SetActive(true);
//             conclusion.ResetReveal();
//             BuildPremises(); BuildWords();
//             ruleChoices.gameObject.SetActive(false);
//             UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
//         }

//         public bool CheckPremises() => ArgumentValidator.PremisesMatch(Puzzle, zones.Select(z => z.Value).ToArray());

//         public void LockPremises()
//         {
//             PremisesCompleted = true;
//             foreach (var zone in zones) zone.Lock();
//             foreach (var word in words) word.Locked = true;
//             wordBank.gameObject.SetActive(false);
//         }

//         public void Solve()
//         {
//             if (Solved) return;
//             Solved = true;
//             LockPremises(); conclusion.Reveal(Puzzle.Conclusion);
//         }

//         void BuildPremises()
//         {
//             foreach (var parts in Puzzle.Premises)
//             {
//                 float available = premises.rect.width > 100 ? premises.rect.width : 1120;
//                 float remaining = available;
//                 var row = Row();
//                 // Wrap individual printed words while keeping each gray blank intact.
//                 var displayParts = parts.SelectMany(part => part.IsBlank ? new[] { part } :
//                     part.Text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Select(t => new PuzzlePart(t)));
//                 foreach (var part in displayParts)
//                 {
//                     float width;
//                     TextMeshProUGUI label = null;
//                     if (part.IsBlank) width = 110;
//                     else
//                     {
//                         label = InferenceBoardUI.Text(row, part.Text, font, 30);
//                         width = label.GetPreferredValues(part.Text).x + 2;
//                     }
//                     if (width + 6 > remaining && remaining < available)
//                     {
//                         row = Row(); remaining = available;
//                         if (label != null) label.transform.SetParent(row, false);
//                     }
//                     if (part.IsBlank)
//                     {
//                         var rect = InferenceBoardUI.Rect("WordBlank", row);
//                         rect.gameObject.AddComponent<UnityEngine.UI.Image>();
//                         var zone = rect.gameObject.AddComponent<DropZone>(); zones.Add(zone);
//                         var placeholder = InferenceBoardUI.Text(rect, "_____", font, 25);
//                         InferenceBoardUI.Stretch(placeholder.rectTransform, 4);
//                         placeholder.alignment = TextAlignmentOptions.Center;
//                         Size(rect.gameObject, width, 58);
//                     }
//                     else Size(label.gameObject, width, 58);
//                     remaining -= width + 6;
//                 }
//                 Size(InferenceBoardUI.Rect("SentenceGap", premises).gameObject, 0, 18);
//             }
//         }

//         RectTransform Row()
//         {
//             var row = InferenceBoardUI.Rect("PremiseLine", premises);
//             var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
//             layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleLeft;
//             layout.childControlWidth = layout.childControlHeight = true;
//             layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
//             Size(row.gameObject, 0, 58); return row;
//         }

//         void BuildWords()
//         {
//             // Fix overlapping draggables by ensuring a LayoutGroup exists
//             if (wordBank.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
//             {
//                 var layout = wordBank.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
//                 layout.cellSize = new Vector2(110, 58);
//                 layout.spacing = new Vector2(10, 10);
//                 layout.childAlignment = TextAnchor.UpperCenter;
//             }

//             // Only the exact words required by this puzzle, including repeated occurrences.
//             var bank = new List<string>(Puzzle.CorrectPlacements);
//             var random = new System.Random(System.Guid.NewGuid().GetHashCode());
//             for (int i = bank.Count - 1; i > 0; i--)
//             { int j = random.Next(i + 1); string swap = bank[i]; bank[i] = bank[j]; bank[j] = swap; }
            
//             foreach (string value in bank)
//             {
//                 var home = InferenceBoardUI.Rect("WordHome", wordBank);
//                 var tile = InferenceBoardUI.Rect("WordTile", home);
//                 tile.sizeDelta = new Vector2(110, 58);
//                 tile.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.16f, 0.31f, 0.33f);
//                 tile.gameObject.AddComponent<CanvasGroup>();
//                 var text = InferenceBoardUI.Text(tile, value, font, 24);
//                 InferenceBoardUI.Stretch(text.rectTransform, 4); text.alignment = TextAlignmentOptions.Center;
//                 var word = tile.gameObject.AddComponent<DraggableWord>();
//                 word.Configure(value, home, dragLayer); word.ReturnToBank(); words.Add(word);
//             }
//         }

//         static void Size(GameObject obj, float width, float height)
//         {
//             var size = obj.GetComponent<UnityEngine.UI.LayoutElement>();
//             if (size == null) size = obj.AddComponent<UnityEngine.UI.LayoutElement>();
//             if (width > 0) { size.preferredWidth = width; size.minWidth = width; }
//             size.preferredHeight = height; size.minHeight = height;
//             size.flexibleHeight = 0;
//         }
//         static void Clear(Transform parent)
//         {
//             foreach (Transform child in parent.Cast<Transform>().ToArray())
//             {
//                 child.gameObject.SetActive(false);
//                 if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
//             }
//         }
//     }
// }


using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.Inference
{
    public sealed class RulesOfInferenceBoard : MonoBehaviour
    {
        public RectTransform premises;
        public RectTransform wordBank;
        public RectTransform ruleChoices;
        public RectTransform dragLayer;
        public TMP_FontAsset font;
        public ConclusionReveal conclusion;
        public RuleOfInferencePuzzle Puzzle { get; private set; }
        public IReadOnlyList<DropZone> Zones => zones;
        public IReadOnlyList<DraggableWord> Words => words;
        public bool Solved { get; private set; }
        readonly List<DropZone> zones = new List<DropZone>();
        readonly List<DraggableWord> words = new List<DraggableWord>();
        public bool PremisesCompleted { get; private set; }

        public void Build(InferenceQuestion question, InferenceRule[] rules)
        {
            Clear(premises); Clear(wordBank); Clear(ruleChoices);
            Clear(dragLayer); zones.Clear(); words.Clear();
            
            // Ensure the drag layer NEVER accidentally blocks drops
            var dlGroup = dragLayer.GetComponent<CanvasGroup>();
            if (dlGroup == null) dlGroup = dragLayer.gameObject.AddComponent<CanvasGroup>();
            dlGroup.blocksRaycasts = false; 

            Puzzle = new RuleOfInferencePuzzle(question); Solved = false; PremisesCompleted = false;
            wordBank.gameObject.SetActive(true);
            conclusion.ResetReveal();
            BuildPremises(); BuildWords();
            ruleChoices.gameObject.SetActive(false);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        public bool CheckPremises() => ArgumentValidator.PremisesMatch(Puzzle, zones.Select(z => z.Value).ToArray());

        public void LockPremises()
        {
            PremisesCompleted = true;
            foreach (var zone in zones) zone.Lock();
            foreach (var word in words) word.Locked = true;
            wordBank.gameObject.SetActive(false);
        }

        public void Solve()
        {
            if (Solved) return;
            Solved = true;
            LockPremises(); conclusion.Reveal(Puzzle.Conclusion);
        }

        void BuildPremises()
        {
            foreach (var parts in Puzzle.Premises)
            {
                float available = premises.rect.width > 100 ? premises.rect.width : 1120;
                float remaining = available;
                var row = Row();
                
                var displayParts = parts.SelectMany(part => part.IsBlank ? new[] { part } :
                    part.Text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Select(t => new PuzzlePart(t)));
                
                foreach (var part in displayParts)
                {
                    float width;
                    TextMeshProUGUI label = null;
                    if (part.IsBlank) width = 110;
                    else
                    {
                        label = InferenceBoardUI.Text(row, part.Text, font, 30);
                        width = label.GetPreferredValues(part.Text).x + 2;
                    }
                    if (width + 6 > remaining && remaining < available)
                    {
                        row = Row(); remaining = available;
                        if (label != null) label.transform.SetParent(row, false);
                    }
                    if (part.IsBlank)
                    {
                        var rect = InferenceBoardUI.Rect("WordBlank", row);
                        var img = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                        img.raycastTarget = true; // explicitly ensure it can receive drops
                        
                        var zone = rect.gameObject.AddComponent<DropZone>(); zones.Add(zone);
                        var placeholder = InferenceBoardUI.Text(rect, "_____", font, 25);
                        InferenceBoardUI.Stretch(placeholder.rectTransform, 4);
                        placeholder.alignment = TextAlignmentOptions.Center;
                        Size(rect.gameObject, width, 58);
                    }
                    else Size(label.gameObject, width, 58);
                    remaining -= width + 6;
                }
                Size(InferenceBoardUI.Rect("SentenceGap", premises).gameObject, 0, 18);
            }
        }

        RectTransform Row()
        {
            var row = InferenceBoardUI.Rect("PremiseLine", premises);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            Size(row.gameObject, 0, 58); return row;
        }

        void BuildWords()
        {
            if (wordBank.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
            {
                var layout = wordBank.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                layout.cellSize = new Vector2(110, 58);
                layout.spacing = new Vector2(10, 10);
                layout.childAlignment = TextAnchor.UpperCenter;
            }

            var bank = new List<string>(Puzzle.CorrectPlacements);
            var random = new System.Random(System.Guid.NewGuid().GetHashCode());
            for (int i = bank.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); string swap = bank[i]; bank[i] = bank[j]; bank[j] = swap; }
            
            foreach (string value in bank)
            {
                var home = InferenceBoardUI.Rect("WordHome", wordBank);
                var tile = InferenceBoardUI.Rect("WordTile", home);
                tile.sizeDelta = new Vector2(110, 58);
                var img = tile.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.16f, 0.31f, 0.33f);
                img.raycastTarget = true; // explicitly ensure it can be dragged
                
                tile.gameObject.AddComponent<CanvasGroup>();
                var text = InferenceBoardUI.Text(tile, value, font, 24);
                InferenceBoardUI.Stretch(text.rectTransform, 4); text.alignment = TextAlignmentOptions.Center;
                var word = tile.gameObject.AddComponent<DraggableWord>();
                word.Configure(value, home, dragLayer); word.ReturnToBank(); words.Add(word);
            }
        }

        static void Size(GameObject obj, float width, float height)
        {
            var size = obj.GetComponent<UnityEngine.UI.LayoutElement>();
            if (size == null) size = obj.AddComponent<UnityEngine.UI.LayoutElement>();
            if (width > 0) { size.preferredWidth = width; size.minWidth = width; }
            size.preferredHeight = height; size.minHeight = height;
            size.flexibleHeight = 0;
        }
        static void Clear(Transform parent)
        {
            foreach (Transform child in parent.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
        }
    }
}