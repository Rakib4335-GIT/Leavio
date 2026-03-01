namespace Leavio.Models;

/// <summary>Single value card for the About Us Values section.</summary>
public class ValueCardItem
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>All editable text for the About Us page. Stored in SystemSettings under AboutUs_* keys.</summary>
public class AboutUsPageContent
{
    // Hero
    public string Title { get; set; } = "About us";
    public string Lead { get; set; } = "We build tools that help teams and individuals do their best work.";

    // Our story
    public string StoryTitle { get; set; } = "Our story";
    public string StoryText { get; set; } = "Leavio was founded with a simple idea: make it easier for people to stay organized and collaborate. We've grown from a small team to a platform that thousands rely on every day. Our focus has always been on clarity, simplicity, and reliability—so you can focus on what matters most.";

    // Values (dynamic list of cards)
    public string ValuesTitle { get; set; } = "What we believe";
    public List<ValueCardItem> Values { get; set; } = new()
    {
        new ValueCardItem { Title = "Simplicity", Text = "We keep things clear and easy to use, so you spend less time learning and more time doing." },
        new ValueCardItem { Title = "Reliability", Text = "Your data and workflows are secure and available when you need them." },
        new ValueCardItem { Title = "People first", Text = "We design for real users and listen to feedback to improve every day." }
    };

    // CTA
    public string CtaTitle { get; set; } = "Ready to get started?";
    public string CtaText { get; set; } = "Join us and see how we can help you work better.";
    public string CtaButtonText { get; set; } = "Contact us";
    public string CtaButtonUrl { get; set; } = "/contact";
}
