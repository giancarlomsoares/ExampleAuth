using ExampleAuth.Server.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ExampleAuth.Server.Services;

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender<ApplicationUser>
{
    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Confirme seu email - ExampleAuth";

        var body = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="font-family: system-ui, sans-serif; background: #0f172a; color: #e2e8f0; padding: 2rem;">
                    <div style="background: #1e293b; padding: 2rem; border-radius: 8px; max-width: 420px; margin: 0 auto;">
                        <h1 style="font-size: 1.25rem; margin-top: 0;">Confirme seu email</h1>
                        <p>Clique no botão abaixo para confirmar seu email no ExampleAuth.</p>
                        <p>
                            <a href="{confirmationLink}" style="display: inline-block; padding: 0.6rem 1.2rem; border-radius: 4px; background: #eab308; color: #0f172a; font-weight: 600; text-decoration: none;">
                                Confirmar email
                            </a>
                        </p>
                        <p style="font-size: 0.8rem; color: #94a3b8;">Se o botão não funcionar, copie e cole este link no navegador:<br />{confirmationLink}</p>
                    </div>
                </div>
                """,
            TextBody = $"Confirme seu email no ExampleAuth acessando o link: {confirmationLink}",
        };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        await client.AuthenticateAsync(smtp.Username, smtp.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Redefinição de senha - ExampleAuth";

        var body = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="font-family: system-ui, sans-serif; background: #0f172a; color: #e2e8f0; padding: 2rem;">
                    <div style="background: #1e293b; padding: 2rem; border-radius: 8px; max-width: 420px; margin: 0 auto;">
                        <h1 style="font-size: 1.25rem; margin-top: 0;">Redefinição de senha</h1>
                        <p>Clique no botão abaixo para definir uma nova senha no ExampleAuth. Este link expira em 1 hora.</p>
                        <p>
                            <a href="{resetLink}" style="display: inline-block; padding: 0.6rem 1.2rem; border-radius: 4px; background: #eab308; color: #0f172a; font-weight: 600; text-decoration: none;">
                                Redefinir senha
                            </a>
                        </p>
                        <p style="font-size: 0.8rem; color: #94a3b8;">Se você não solicitou essa redefinição, ignore este email — sua senha atual continua válida.</p>
                        <p style="font-size: 0.8rem; color: #94a3b8;">Se o botão não funcionar, copie e cole este link no navegador:<br />{resetLink}</p>
                    </div>
                </div>
                """,
            TextBody = $"Redefina sua senha no ExampleAuth acessando o link (válido por 1 hora): {resetLink}",
        };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        await client.AuthenticateAsync(smtp.Username, smtp.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        throw new NotSupportedException("Password reset via email is not implemented.");
}
