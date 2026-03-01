namespace Leavio.Models;

/// <summary>All editable text and icon content for the homepage. Used for get/set from SystemSettings.</summary>
public class HomePageContent
{
    // Hero
    public string HeroTitle { get; set; } = "Hello, world!";
    public string HeroSubtitle { get; set; } = "Welcome to your new app.";
    public string HeroCtaPrimary { get; set; } = "Get in touch";
    public string HeroCtaPrimaryUrl { get; set; } = "/contact";
    public string HeroCtaSecondary { get; set; } = "Learn more";
    public string HeroCtaSecondaryUrl { get; set; } = "/aboutUs";

    // Features section
    public string FeaturesTitle { get; set; } = "What we offer";
    public string FeaturesLead { get; set; } = "Everything you need in one place.";
    public string Feature1Icon { get; set; } = "◆";
    public string Feature1Title { get; set; } = "Easy to use";
    public string Feature1Desc { get; set; } = "Simple, intuitive interface so you can get started in minutes.";
    public string Feature2Icon { get; set; } = "◇";
    public string Feature2Title { get; set; } = "Secure & reliable";
    public string Feature2Desc { get; set; } = "Your data is protected with industry-standard security.";
    public string Feature3Icon { get; set; } = "○";
    public string Feature3Title { get; set; } = "Support when you need it";
    public string Feature3Desc { get; set; } = "Our team is here to help you succeed.";

    // About
    public string AboutTitle { get; set; } = "About us";
    public string AboutText { get; set; } = "We build tools that make your work easier. Leavio is designed to help teams and individuals stay organized, collaborate better, and get more done. Whether you're managing projects or just getting started, we're here to support you every step of the way.";
    public string AboutButtonText { get; set; } = "Read our story";
    public string AboutButtonUrl { get; set; } = "/aboutUs";

    // Stats
    public string Stat1Number { get; set; } = "500+";
    public string Stat1Label { get; set; } = "Happy users";
    public string Stat2Number { get; set; } = "24/7";
    public string Stat2Label { get; set; } = "Support";
    public string Stat3Number { get; set; } = "99%";
    public string Stat3Label { get; set; } = "Uptime";

    // Testimonials
    public string TestimonialsTitle { get; set; } = "What people say";
    public string Testimonial1Text { get; set; } = "\"Leavio has completely changed how we work. Simple, fast, and exactly what we needed.\"";
    public string Testimonial1Author { get; set; } = "— Sarah M.";
    public string Testimonial2Text { get; set; } = "\"The best tool we've adopted this year. Our team is more productive than ever.\"";
    public string Testimonial2Author { get; set; } = "— James K.";

    // CTA
    public string CtaTitle { get; set; } = "Ready to get started?";
    public string CtaText { get; set; } = "Join us today and see the difference.";
    public string CtaPrimaryText { get; set; } = "Sign up free";
    public string CtaPrimaryUrl { get; set; } = "/register";
    public string CtaSecondaryText { get; set; } = "Contact us";
    public string CtaSecondaryUrl { get; set; } = "/contact";
}
