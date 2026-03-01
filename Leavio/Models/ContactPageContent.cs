namespace Leavio.Models;

/// <summary>One social link with icon, name, and URL. Used in ContactPageContent.SocialLinks.</summary>
public class SocialLinkItem
{
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>All editable text for the contact page. Stored in SystemSettings under Contact_* keys.</summary>
public class ContactPageContent
{
    // Page
    public string Title { get; set; } = "Contact us";
    public string Lead { get; set; } = "We'd love to hear from you.";

    // Left column: contact info (main heading = SideTitle, intro = SideText)
    public string SideTitle { get; set; } = "Get in Touch";
    public string SubHeading { get; set; } = "I'd like to hear from you!";
    public string SideText { get; set; } = "If you have any inquiries or just want to say hi, please use the contact form!";
    public string EmailAddress { get; set; } = "";
    /// <summary>Social links (icon, name, URL each). Stored as JSON in Contact_SocialLinks.</summary>
    public List<SocialLinkItem> SocialLinks { get; set; } = new List<SocialLinkItem>();

    // Form labels
    public string LabelFirstName { get; set; } = "First Name";
    public string LabelLastName { get; set; } = "Last Name";
    public string LabelEmail { get; set; } = "Email";
    public string LabelMessage { get; set; } = "Message";
    public string SubmitButtonText { get; set; } = "Send";

    // Legacy (kept for backward compat, not shown in new layout)
    public string LabelName { get; set; } = "Name";
    public string LabelSubject { get; set; } = "Subject";

    // After submit
    public string SuccessMessage { get; set; } = "Thank you for your message. We'll get back to you soon.";
}
