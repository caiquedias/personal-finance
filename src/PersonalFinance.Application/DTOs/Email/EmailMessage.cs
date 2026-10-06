namespace PersonalFinance.Application.DTOs.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody);
