using HaarWaveletLab.Theory;

namespace HaarWaveletLab.UI;

/// <summary>Формирование размеченного текста в RichTextBox.</summary>
public static class RichText
{
    public static void SetAnalysis(RichTextBox box, int order, string text)
    {
        box.SuspendLayout();
        try
        {
            box.Clear();
            Append(box, $"Порядок {order}. Результаты эксперимента\n\n", Theme.SectionFont, Theme.AccentDark);

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                if (line == "ТЕКУЩИЙ ЭКСПЕРИМЕНТ")
                    Append(box, line + "\n", Theme.UiFontBold, Theme.AccentDark);
                else
                    Append(box, line + "\n\n", Theme.TheoryBody, Theme.Text);
            }

            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ScrollToCaret();
        }
        finally
        {
            box.ResumeLayout();
        }
    }

    public static void RenderTheory(RichTextBox box)
    {
        box.SuspendLayout();
        try
        {
            box.Clear();
            Append(box, "Вейвлет-преобразование Хаара\n", Theme.HeroFont, Theme.Header);
            Append(box, "Учебный раздел: теория, формулы и контрольные вопросы к лабораторной работе №3.\n\n",
                Theme.SmallFont, Theme.Muted);

            foreach (var section in LabTheory.Sections)
            {
                Append(box, section.Title + "\n", Theme.TheoryHeading, Theme.AccentDark);
                foreach (string paragraph in section.Paragraphs)
                    Append(box, paragraph + "\n\n", Theme.TheoryBody, Theme.Text);
            }

            Append(box, "Контрольные вопросы\n", Theme.TheoryHeading, Theme.AccentDark);
            foreach (var (question, answer) in LabTheory.ControlQuestions)
            {
                Append(box, question + "\n", Theme.TheorySubHeading, Theme.Text);
                Append(box, answer + "\n\n", Theme.TheoryBody, Theme.Muted);
            }

            Append(box, "Структура программы\n", Theme.TheoryHeading, Theme.AccentDark);
            foreach (var (file, responsibility) in LabTheory.Structure)
            {
                Append(box, file, Theme.TheorySubHeading, Theme.Text);
                Append(box, " — " + responsibility + "\n", Theme.TheoryBody, Theme.Muted);
            }

            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ScrollToCaret();
        }
        finally
        {
            box.ResumeLayout();
        }
    }

    private static void Append(RichTextBox box, string text, Font font, Color color)
    {
        int start = box.TextLength;
        box.AppendText(text);
        box.Select(start, text.Length);
        box.SelectionFont = font;
        box.SelectionColor = color;
        box.Select(box.TextLength, 0);
    }
}
