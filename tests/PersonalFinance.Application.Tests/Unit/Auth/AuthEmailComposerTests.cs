using FluentAssertions;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.Services.Auth;
using System.Text.RegularExpressions;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// AuthEmailComposer (#404): link só com e-mail no fragmento (código NUNCA na URL), e-mail URL-encoded no
/// link e HTML-encoded no corpo, barra final da base normalizada.
/// </summary>
public class AuthEmailComposerTests
{
    private const string Code = "482913";

    private static AuthEmailComposer Build(string baseUrl = "https://app.example.com") =>
        new(new AppOptions { FrontendBaseUrl = baseUrl });

    private static IEnumerable<string> Hrefs(string html) =>
        Regex.Matches(html, "href=\"([^\"]*)\"", RegexOptions.IgnoreCase).Select(m => m.Groups[1].Value);

    // ── Reset de senha ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Reset: destinatário é o e-mail, assunto preenchido e corpo contém o código")]
    public void ComposePasswordReset_ShouldAddressUserAndShowCode()
    {
        var message = Build().ComposePasswordReset("ana@x.com", Code, 10);

        message.To.Should().Be("ana@x.com");
        message.Subject.Should().NotBeNullOrWhiteSpace();
        message.HtmlBody.Should().Contain(Code);
    }

    [Fact(DisplayName = "Reset: link aponta para /reset-password com o e-mail URL-encoded no fragmento")]
    public void ComposePasswordReset_ShouldBuildFragmentLink()
    {
        var message = Build().ComposePasswordReset("a+b@x.com", Code, 10);

        message.HtmlBody.Should().Contain("https://app.example.com/reset-password#email=a%2Bb%40x.com");
    }

    [Fact(DisplayName = "Reset: o código nunca aparece em nenhum link do corpo")]
    public void ComposePasswordReset_CodeMustNotAppearInAnyLink()
    {
        var message = Build().ComposePasswordReset("ana@x.com", Code, 10);

        Hrefs(message.HtmlBody).Should().NotBeEmpty().And.OnlyContain(h => !h.Contains(Code));
        message.Subject.Should().NotContain(Code);
    }

    [Fact(DisplayName = "Reset: barra final da base do frontend é normalizada (sem //)")]
    public void ComposePasswordReset_TrailingSlash_ShouldBeNormalized()
    {
        var message = Build("https://app.example.com/").ComposePasswordReset("ana@x.com", Code, 10);

        message.HtmlBody.Should().Contain("https://app.example.com/reset-password#email=ana%40x.com");
        message.HtmlBody.Should().NotContain("example.com//reset-password");
    }

    [Fact(DisplayName = "Reset: e-mail é HTML-encoded no corpo (sem HTML injetado)")]
    public void ComposePasswordReset_ShouldHtmlEncodeEmailInBody()
    {
        var message = Build().ComposePasswordReset("<b>x</b>&y@x.com", Code, 10);

        message.HtmlBody.Should().NotContain("<b>x</b>");
        message.HtmlBody.Should().Contain("&lt;b&gt;x&lt;/b&gt;&amp;y@x.com");
    }

    [Fact(DisplayName = "Reset: href não contém '&' nem '<' crus vindos do e-mail")]
    public void ComposePasswordReset_HrefMustBeEscaped()
    {
        var message = Build().ComposePasswordReset("a&b\"c@x.com", Code, 10);

        Hrefs(message.HtmlBody).Should().OnlyContain(h => !h.Contains('&') && !h.Contains('<'));
    }

    [Fact(DisplayName = "Reset: corpo informa o tempo de validade do código")]
    public void ComposePasswordReset_ShouldMentionTtl()
    {
        Build().ComposePasswordReset("ana@x.com", Code, 15).HtmlBody.Should().Contain("15");
    }

    // ── Verificação de e-mail ─────────────────────────────────────────────────

    [Fact(DisplayName = "Verificação: link aponta para /confirm-email com o e-mail no fragmento e o código só no corpo")]
    public void ComposeEmailVerification_ShouldBuildConfirmLinkWithoutCode()
    {
        var message = Build().ComposeEmailVerification("ana@x.com", Code, 10);

        message.To.Should().Be("ana@x.com");
        message.HtmlBody.Should().Contain("https://app.example.com/confirm-email#email=ana%40x.com");
        message.HtmlBody.Should().Contain(Code);
        Hrefs(message.HtmlBody).Should().OnlyContain(h => !h.Contains(Code));
        message.HtmlBody.Should().NotContain("/reset-password");
    }

    [Fact(DisplayName = "Verificação: barra final normalizada e e-mail HTML-encoded")]
    public void ComposeEmailVerification_ShouldNormalizeAndEncode()
    {
        var message = Build("https://app.example.com/").ComposeEmailVerification("<i>@x.com", Code, 10);

        message.HtmlBody.Should().NotContain("example.com//confirm-email");
        message.HtmlBody.Should().NotContain("<i>@x.com");
        message.HtmlBody.Should().Contain("&lt;i&gt;@x.com");
    }

    [Fact(DisplayName = "Assuntos de reset e de verificação são diferentes")]
    public void Subjects_ShouldDifferByPurpose()
    {
        var sut = Build();

        sut.ComposePasswordReset("ana@x.com", Code, 10).Subject
            .Should().NotBe(sut.ComposeEmailVerification("ana@x.com", Code, 10).Subject);
    }
}
