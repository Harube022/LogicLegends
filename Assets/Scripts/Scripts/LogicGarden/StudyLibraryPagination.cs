using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Measures semantic lesson blocks at the reader's fixed font size. Never shrinks text to fit.</summary>
public static class StudyLibraryPagination
{
    public const float BodySize = 36;
    public const float LineSpacing = 3;
    public const float ParagraphSpacing = 10;

    public static StudyLessonPage[] Build(StudyBookDefinition book, TMP_Text measure)
    {
        var pages = new List<StudyLessonPage>();
        pages.Add(new StudyLessonPage { cover = true, text =
            "<size=38>Discrete Structures 1</size>\n\n<size=50><b>" + TopicTitle(book.title) + "</b></size>" });
        string pending = "";
        float width = measure.rectTransform.rect.width;
        float height = measure.rectTransform.rect.height - 6; // SDF ascender/descender tolerance.
        bool Fits(string text) => measure.GetPreferredValues(text,width,Mathf.Infinity).y <= height;
        void Flush()
        {
            if (pending.Length == 0) return;
            pages.Add(new StudyLessonPage { text = pending }); pending = "";
        }
        foreach (var block in book.lessonBlocks)
        {
            if (!string.IsNullOrEmpty(block.diagram))
            {
                Flush(); pages.Add(new StudyLessonPage { diagram=block.diagram,text=block.text,source=block.heading }); continue;
            }
            string heading = "<size=44><b>" + block.heading + "</b></size>";
            string unit = heading + "\n\n" + block.text;
            if (Fits(unit))
            {
                string combined = pending.Length == 0 ? unit : pending + "\n\n" + unit;
                if (!Fits(combined)) Flush();
                pending = pending.Length == 0 ? unit : pending + "\n\n" + unit;
                continue;
            }
            // Split only at authored paragraph boundaries. Formula groups, tables
            // and worked algebra steps stay intact; repeat their heading as needed.
            Flush(); pending = heading;
            foreach (var paragraph in block.text.Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Fits(heading+"\n\n"+paragraph))
                    throw new InvalidOperationException("Study block needs a smaller semantic section: "+book.topicId+" / "+block.heading);
                if (!Fits(pending+"\n\n"+paragraph)) { Flush(); pending=heading; }
                pending += "\n\n" + paragraph;
            }
        }
        Flush(); return pages.ToArray();
    }

    static string TopicTitle(string title)
    {
        string value=System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.ToLowerInvariant());
        if(value.Length<=22)return value;
        // Deliberately balance long cover titles instead of filling an entire
        // line to its final glyph or reducing their type size.
        int split=-1;
        for(int i=0;i<value.Length;i++)
            if(value[i]==' '&&(split<0||Mathf.Abs(i-value.Length/2)<Mathf.Abs(split-value.Length/2)))split=i;
        return split<0?value:value.Substring(0,split)+"\n"+value.Substring(split+1);
    }
}
