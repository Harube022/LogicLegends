using System;
using System.Collections.Generic;
using UnityEngine;

// Edit the questions and answer indices in the asset's Inspector. One asset is
// shared by all six house doors; the controller deals a different question to each.
[CreateAssetMenu(fileName = "TruthTableDoorQuestions", menuName = "Logic Legends/Truth Table Door Questions")]
public class TruthTableDoorQuestionBank : ScriptableObject
{
    [Serializable]
    public class Question
    {
        [TextArea(2, 4)] public string prompt;
        public string[] choices = new string[4];
        [Range(0, 3)] public int correctChoice;
    }

    public List<Question> questions = new List<Question>();
}
