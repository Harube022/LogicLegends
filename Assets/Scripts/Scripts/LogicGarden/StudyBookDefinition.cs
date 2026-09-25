using System;

[Serializable]
public class StudyBookDefinition
{
    public string topicId;
    public StudyLessonPage[] lessonPages;
    public int PageCount => lessonPages != null && lessonPages.Length > 0 ? lessonPages.Length : (pages == null ? 0 : pages.Length);
    public string title;
    public string preview;
    public string content;
    public string source;
    public string[] pages;

    public StudyBookDefinition(string title, string preview, string content, string source)
    {
        this.title = title;
        this.preview = preview;
        this.content = content;
        this.source = source;
        pages = BuildPages(content);
    }

    private static string[] BuildPages(string sourceContent)
    {
        if (string.IsNullOrWhiteSpace(sourceContent))
            return new[] { "No study content is available for this book." };

        string[] sections = sourceContent.Split(
            new[] { "\n\n" },
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < sections.Length; i++)
            sections[i] = sections[i].Trim();

        return sections;
    }
}

[Serializable]
public class StudyLesson
{
    public StudyLessonPage[] pages;
}

[Serializable]
public class StudyLessonPage
{
    public string image;
    public string diagram;
    public string text;
    public string source;
}
