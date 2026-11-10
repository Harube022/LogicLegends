# Trial Answers — SHOW Preview

This file previews the question and answer content shown by the green **SHOW** button after a trial is completed. The game records the real question and the player's submitted answer during that run. Several trials select question variants at runtime, so the examples below show the output format with representative questions. Example wrong player answers are samples; the game displays what the player actually submitted. Each recorded attempt is listed, so a question can appear more than once after retries or repeated submissions.

In the game, correct answers are green and incorrect player answers are red. Trial 2 uses two aligned, borderless truth tables per expression, with `p`, `q`, and the expression result columns.

## Trial 1 — Propositional Logic

**Question**  
It is only true when both statements are true.

**Correct answer**  
Conjunction

**Sample player answer**  
Disjunction *(shown red if selected)*

## Trial 2 — Truth Table

**Question**  
Challenge 1 (Easy), Column 2: `P ∨ Q` — rows `TT, TF, FT, FF`.

**Correct table**

```text
p  q  P ∨ Q
T  T    T
T  F    T
F  T    T
F  F    F
```

**Sample player table**

```text
p  q  P ∨ Q
T  T    T
T  F    T
F  T    T
F  F    T
```

The last player value would be red because it differs from the correct value. The actual expression and both tables come from the column the player submitted.

## Trial 3 — Rules of Inference

The review uses the same worded premises as the gameplay board. Words the player had to fill appear as blanks in the question; it does not show the symbolic form such as `(p → q) ∧ r`.

**Question**  
Premises: `_____ (the seed is planted), then (the flower grows) and the sun is shining.` ⟹ ?

**Correct answer**  
Premises: `If (the seed is planted), then (the flower grows) and the sun is shining.`; Rule: Simplification; Conclusion: `if (the seed is planted), then (the flower grows)`.

**Sample player answer**  
Wrong rule crystal: Constructive Dilemma *(shown red if that was the player's submitted mistake)*

When the player completes this challenge correctly, **Your answer** is also shown in green and contains the completed worded premises, selected rule, and revealed conclusion. The five challenges use different worded statements and blanks; SHOW lists the prompts and attempts actually recorded in that run.

## Trial 4 — Formal vs. Informal Proofs

**Question**  
Identify Proof Styles — place each proof on the correct altar.

**Correct answer**  
Formal Scroll: Formal; Informal Scroll: Informal.

**Sample player answer**  
Formal Scroll: Formal; Informal Scroll: Formal *(the second classification would be red)*

## Trial 5 — Direct and Indirect Proofs

**Question**  
If `n²` is even, then `n` is even. An argument assumes `n` is odd and derives that `n²` is odd. Is it a direct or indirect proof?

**Correct answer**  
Contrapositive (an indirect proof).

**Sample player answer**  
Direct *(shown red if selected)*

## Trial 6 — Mathematical Induction

**Question**  
Verify the base case for `S(n) = 1 + 2 + … + n = n(n + 1)/2`. Substitute `n = 1`.

**Correct answer**  
Base: `S(1) = 1 = (1 + 1)/2`.

**Sample player answer**  
Base: `S(1) = 1 = 0` *(shown red if selected)*

## Trial 7 — Sets

**Question**  
For `A = {1}` and `B = {}`, place each element in the matching Venn region.

**Correct answer**  
`1 = A − B` (`A_ONLY`).

**Sample player answer**  
`1 = OUTSIDE` *(shown red if placed outside)*

## Trial 8 — Functions and Relations

**Question**  
Build the relation by connecting `1 → A`, `2 → B`, `3 → C`, and `4 → D`.

**Correct answer**  
`1 → A, 2 → B, 3 → C, 4 → D`.

**Sample player answer**  
`1 → A, 2 → B, 3 → D, 4 → C` *(the differing pairs would be red in the answer review)*

## Trial 9 — Graphs

**Question**  
Travel from Vertex A to Vertex B using the connecting edge.

**Correct answer**  
`A → B`.

**Sample player answer**  
`B → A` *(shown red because the route must begin at A and end at B)*

## Trial 10 — Trees

**Question**  
What is the depth of node F?

**Correct answer**  
`3` (three edges from the root to F in this example tree).

**Sample player answer**  
`2` *(shown red if selected)*

## What SHOW displays in a real run

For each trial, SHOW lists the prompts encountered during that player's attempt, the game's expected answer, and the player's submitted answer. The sample wrong answers above only illustrate the red styling. Trial 2 records each full truth-table column submission, not the house-door quiz answers. Trial 3 lists the worded premise challenge attempts; rule-crystal pickup quiz questions are excluded from its SHOW list and accuracy.
