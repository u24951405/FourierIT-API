using System.Net;
using System.Net.Mail;
using System.Text;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.Extensions.Options;

namespace FourierIT_API.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendInvitationEmailAsync(string toEmail, string institutionName, string accessLink, DateTimeOffset expiresAt)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("SMTP host is not configured.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email address is required.", nameof(toEmail));

            if (string.IsNullOrWhiteSpace(accessLink))
                throw new ArgumentException("Access link is required.", nameof(accessLink));

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
                Subject = "DocuVault Secure Access Invitation",
                Body = BuildEmailHtmlBody(institutionName, accessLink, expiresAt),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("Importance", "High");
            message.Headers.Add("X-MSMail-Priority", "High");

            message.To.Add(toEmail);
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(BuildEmailPlainTextBody(institutionName, accessLink, expiresAt), null, "text/plain"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                UseDefaultCredentials = false,
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            await client.SendMailAsync(message);
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, DateTimeOffset expiresAt)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("SMTP host is not configured.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email address is required.", nameof(toEmail));

            if (string.IsNullOrWhiteSpace(resetLink))
                throw new ArgumentException("Reset link is required.", nameof(resetLink));

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
                Subject = "DocuVault Password Reset",
                Body = BuildPasswordResetEmailHtmlBody(resetLink, expiresAt),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("Importance", "High");
            message.Headers.Add("X-MSMail-Priority", "High");

            message.To.Add(toEmail);
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(BuildPasswordResetEmailPlainTextBody(resetLink, expiresAt), null, "text/plain"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                UseDefaultCredentials = false,
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            await client.SendMailAsync(message);
        }

        private static string BuildPasswordResetEmailPlainTextBody(string resetLink, DateTimeOffset expiresAt)
        {
            return $"Hello,\n\n" +
                   "We received a request to reset your DocuVault password. Use the link below to choose a new password:\n\n" +
                   $"{resetLink}\n\n" +
                   $"This link expires on {expiresAt:yyyy-MM-dd HH:mm}.\n\n" +
                   "If you did not request a password reset, you can safely ignore this message.\n\n" +
                   "Thank you,\n" +
                   "M5CS | DocuVault Security Team\n";
        }

        private static string BuildPasswordResetEmailHtmlBody(string resetLink, DateTimeOffset expiresAt)
        {
            var logoData = GetInlineLogoSvgBase64();
            return $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;background:#f3f4f6;margin:0;padding:0;\">" +
                   "<div style=\"max-width:680px;margin:0 auto;padding:32px 16px;\">" +
                   "<div style=\"background:#ffffff;border-radius:24px;box-shadow:0 24px 80px rgba(15,23,42,0.08);overflow:hidden;\">" +
                   "<div style=\"padding:32px 40px;background:#0f172a;color:#f8fafc;text-align:center;\">" +
                   $"<img src=\"data:image/svg+xml;base64,{logoData}\" alt=\"M5CS logo\" width=60 height=60 style=\"display:block;margin:0 auto 18px;\" />" +
                   "<p style=\"margin:0;font-size:14px;letter-spacing:0.16em;color:#94a3b8;text-transform:uppercase;\">Password reset request</p>" +
                   "<h1 style=\"margin:16px 0 0;font-size:30px;font-weight:700;line-height:1.1;\">Reset your password</h1>" +
                   "</div>" +
                   "<div style=\"padding:32px 40px;\">" +
                   "<p style=\"margin:0 0 24px;font-size:16px;color:#334155;\">Hello,<br/>We received a request to reset the password for your DocuVault account. Click the button below to choose a new password.</p>" +
                   $"<p style=\"margin:0 0 32px;text-align:center;\"><a href=\"{resetLink}\" style=\"display:inline-flex;align-items:center;justify-content:center;padding:14px 26px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;box-shadow:0 12px 30px rgba(37,99,235,0.18);\">Reset Password</a></p>" +
                   "<div style=\"padding:24px;background:#f8fafc;border-radius:18px;border:1px solid #e2e8f0;margin-bottom:32px;\">" +
                   "<p style=\"margin:0 0 12px;font-size:14px;font-weight:700;color:#0f172a;\">Link expiry</p>" +
                   $"<p style=\"margin:0;font-size:15px;color:#475569;\">This link expires on <strong>{expiresAt:yyyy-MM-dd HH:mm}</strong>.</p>" +
                   "</div>" +
                   "<p style=\"margin:0 0 18px;font-size:15px;color:#475569;\">If you did not request a password reset, please ignore this message or contact your administrator.</p>" +
                   "</div>" +
                   "<div style=\"padding:24px 40px 32px;border-top:1px solid #e2e8f0;background:#fff;display:flex;align-items:center;gap:16px;\">" +
                   $"<div style=\"width:56px;height:56px;border-radius:16px;background:linear-gradient(135deg,#2563eb,#22c55e);display:flex;align-items:center;justify-content:center;\">" +
                   $"<span style=\"font-size:20px;font-weight:800;color:#ffffff;font-family:Segoe UI,Arial,sans-serif;\">M5</span>" +
                   "</div>" +
                   "<div>" +
                   "<p style=\"margin:0;font-size:15px;font-weight:700;color:#0f172a;\">M5CS</p>" +
                   "<p style=\"margin:4px 0 0;font-size:13px;color:#64748b;\">Secure document exchange for institutions.</p>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</body></html>";
        }

        public async Task SendUserRegistrationOtpEmailAsync(string toEmail, string otpCode, DateTimeOffset expiresAt)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("SMTP host is not configured.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email address is required.", nameof(toEmail));

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
                Subject = "Welcome to DocuVault — verify your email",
                Body = BuildUserRegistrationOtpEmailHtmlBody(otpCode, expiresAt),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("Importance", "High");
            message.Headers.Add("X-MSMail-Priority", "High");

            message.To.Add(toEmail);
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(BuildUserRegistrationOtpEmailPlainTextBody(otpCode, expiresAt), null, "text/plain"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                UseDefaultCredentials = false,
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            await client.SendMailAsync(message);
        }

        public async Task SendInstitutionOtpEmailAsync(string toEmail, string institutionName, string otpCode, DateTimeOffset expiresAt)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("SMTP host is not configured.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email address is required.", nameof(toEmail));

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
                Subject = "Your institution portal access code",
                Body = BuildInstitutionOtpEmailHtmlBody(institutionName, otpCode, expiresAt),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("Importance", "High");
            message.Headers.Add("X-MSMail-Priority", "High");

            message.To.Add(toEmail);
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(BuildInstitutionOtpEmailPlainTextBody(institutionName, otpCode, expiresAt), null, "text/plain"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                UseDefaultCredentials = false,
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            await client.SendMailAsync(message);
        }

        public async Task SendOtpEmailAsync(string toEmail, string institutionName, string otpCode, DateTimeOffset expiresAt)
        {
            await SendInstitutionOtpEmailAsync(toEmail, institutionName, otpCode, expiresAt);
        }

        private static string BuildEmailPlainTextBody(string institutionName, string accessLink, DateTimeOffset expiresAt)
        {
            var expiryText = expiresAt == DateTimeOffset.MaxValue
                ? "This access link does not expire."
                : $"This link expires on {expiresAt:yyyy-MM-dd HH:mm}, so please access it promptly.";

            return $"Hello,\n\n" +
                   $"You have been invited to securely access DocuVault on behalf of {institutionName}.\n\n" +
                   "Please open the secure access portal below and follow the instructions to complete your login: \n" +
                   $"{accessLink}\n\n" +
                   $"{expiryText}\n\n" +
                   "This invitation email contains ONLY the secure access link. The one-time verification code (OTP) will be generated and sent separately to this email address after the link is opened.\n\n" +
                   "If you did not request this invitation, you may safely ignore this message or contact your administrator.\n\n" +
                   "Thank you,\n" +
                   "M5CS | DocuVault Security Team\n";
        }

        private static string BuildEmailHtmlBody(string institutionName, string accessLink, DateTimeOffset expiresAt)
        {
            var logoData = GetInlineLogoSvgBase64();
            return $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;background:#f3f4f6;margin:0;padding:0;\">" +
                   "<div style=\"max-width:680px;margin:0 auto;padding:32px 16px;\">" +
                   "<div style=\"background:#ffffff;border-radius:24px;box-shadow:0 24px 80px rgba(15,23,42,0.08);overflow:hidden;\">" +
                   "<div style=\"padding:32px 40px;background:#0f172a;color:#f8fafc;text-align:center;\">" +
                   $"<img src=\"data:image/svg+xml;base64,{logoData}\" alt=\"M5CS logo\" width=60 height=60 style=\"display:block;margin:0 auto 18px;\" />" +
                   "<p style=\"margin:0;font-size:14px;letter-spacing:0.16em;color:#94a3b8;text-transform:uppercase;\">Secure access invitation</p>" +
                   "<h1 style=\"margin:16px 0 0;font-size:30px;font-weight:700;line-height:1.1;\">Your secure DocuVault invitation is ready</h1>" +
                   "</div>" +
                   "<div style=\"padding:32px 40px;\">" +
                   $"<p style=\"margin:0 0 24px;font-size:16px;color:#334155;\">Hello,<br/>You have been invited to securely access DocuVault on behalf of <strong>{institutionName}</strong>. Please use the secure access link below to continue.</p>" +
                   $"<p style=\"margin:0 0 32px;text-align:center;\"><a href=\"{accessLink}\" style=\"display:inline-flex;align-items:center;justify-content:center;padding:14px 26px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;box-shadow:0 12px 30px rgba(37,99,235,0.18);\">Open Secure Access Portal</a></p>" +
                   $"<div style=\"padding:24px;background:#f8fafc;border-radius:18px;border:1px solid #e2e8f0;margin-bottom:32px;\">" +
                   $"<p style=\"margin:0 0 12px;font-size:14px;font-weight:700;color:#0f172a;\">Link expiry</p>" +
                   $"<p style=\"margin:0;font-size:15px;color:#475569;\">{(expiresAt == DateTimeOffset.MaxValue ? "This access link does not expire." : $"This link expires on <strong>{expiresAt:yyyy-MM-dd HH:mm}</strong>. Please open it before then.")}</p>" +
                   "</div>" +
                   $"<p style=\"margin:0 0 18px;font-size:15px;color:#475569;\">This invitation email contains ONLY your secure access link. The one-time verification code (OTP) will be generated and sent separately to this email address after the link is opened.</p>" +
                   $"<p style=\"margin:0;font-size:15px;color:#475569;\">If you did not request this invitation, please ignore this message or contact your administrator immediately.</p>" +
                   "</div>" +
                   "<div style=\"padding:24px 40px 32px;border-top:1px solid #e2e8f0;background:#fff;display:flex;align-items:center;gap:16px;\">" +
                   $"<div style=\"width:56px;height:56px;border-radius:16px;background:linear-gradient(135deg,#2563eb,#22c55e);display:flex;align-items:center;justify-content:center;\">" +
                   $"<span style=\"font-size:20px;font-weight:800;color:#ffffff;font-family:Segoe UI,Arial,sans-serif;\">M5</span>" +
                   $"</div>" +
                   "<div>" +
                   $"<p style=\"margin:0;font-size:15px;font-weight:700;color:#0f172a;\">M5CS</p>" +
                   $"<p style=\"margin:4px 0 0;font-size:13px;color:#64748b;\">Secure document exchange for institutions.</p>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</body></html>";
        }

        public Task SendEmailChangeOtpEmailAsync(string toEmail, string otpCode, DateTimeOffset expiresAt)
        {
            var text = "You asked to change the email address on your DocuVault account to this address.\n\n" +
                       $"Your verification code is: {otpCode}\n\n" +
                       $"This code will expire on {expiresAt:yyyy-MM-dd HH:mm}.\n\n" +
                       "If you did not request this change, you can ignore this message. Your email address will not change.\n\n" +
                       "Thank you,\n" +
                       "M5CS | DocuVault Security Team\n";

            var html = BuildSecurityEmailHtml(
                "Security",
                "Confirm your new email address",
                "Hello,<br/>You asked to change the email address on your DocuVault account to this address. Enter the code below to confirm the change.",
                $"<div style=\"padding:26px 24px;background:#eff6ff;border-radius:18px;border:1px solid #dbeafe;text-align:center;margin-bottom:32px;\">" +
                $"<p style=\"margin:0;font-size:32px;font-weight:800;color:#0f172a;letter-spacing:0.16em;\">{otpCode}</p>" +
                $"<p style=\"margin:8px 0 0;font-size:14px;color:#475569;\">This code expires on {expiresAt:yyyy-MM-dd HH:mm}.</p>" +
                "</div>",
                "If you did not request this change, you can ignore this message. Your email address will not change.");

            return SendSecurityEmailAsync(toEmail, "DocuVault — confirm your new email address", html, text);
        }

        public Task SendEmailChangedNoticeAsync(string oldEmail, string newEmail)
        {
            var text = "The email address on your DocuVault account was just changed.\n\n" +
                       $"New email address: {newEmail}\n\n" +
                       "From now on, sign-in codes and notifications will go to the new address.\n\n" +
                       "If you did not make this change, contact your administrator immediately.\n\n" +
                       "Thank you,\n" +
                       "M5CS | DocuVault Security Team\n";

            var html = BuildSecurityEmailHtml(
                "Security notice",
                "Your email address was changed",
                "Hello,<br/>The email address on your DocuVault account was just changed. From now on, codes and notifications will go to the new address.",
                "<div style=\"padding:18px 24px;background:#f8fafc;border-radius:18px;border:1px solid #e2e8f0;margin-bottom:32px;\">" +
                "<p style=\"margin:0;font-size:13px;color:#64748b;text-transform:uppercase;letter-spacing:0.08em;\">New email address</p>" +
                $"<p style=\"margin:6px 0 0;font-size:18px;font-weight:700;color:#0f172a;\">{WebUtility.HtmlEncode(newEmail)}</p>" +
                "</div>",
                "If you did not make this change, contact your administrator immediately.");

            return SendSecurityEmailAsync(oldEmail, "DocuVault — your email address was changed", html, text);
        }

        private async Task SendSecurityEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("SMTP host is not configured.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email address is required.", nameof(toEmail));

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("Importance", "High");
            message.Headers.Add("X-MSMail-Priority", "High");

            message.To.Add(toEmail);
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                UseDefaultCredentials = false,
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            await client.SendMailAsync(message);
        }

        // Same layout as the registration verification email.
        private static string BuildSecurityEmailHtml(string eyebrow, string title, string introHtml, string highlightHtml, string footnote)
        {
            var logoData = GetInlineLogoSvgBase64();
            return $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;background:#f3f4f6;margin:0;padding:0;\">" +
                   "<div style=\"max-width:680px;margin:0 auto;padding:32px 16px;\">" +
                   "<div style=\"background:#ffffff;border-radius:24px;box-shadow:0 24px 80px rgba(15,23,42,0.08);overflow:hidden;\">" +
                   "<div style=\"padding:32px 40px;background:#0f172a;color:#f8fafc;text-align:center;\">" +
                   $"<img src=\"data:image/svg+xml;base64,{logoData}\" alt=\"M5CS logo\" width=60 height=60 style=\"display:block;margin:0 auto 18px;\" />" +
                   $"<p style=\"margin:0;font-size:14px;letter-spacing:0.16em;color:#94a3b8;text-transform:uppercase;\">{eyebrow}</p>" +
                   $"<h1 style=\"margin:16px 0 0;font-size:30px;font-weight:700;line-height:1.1;\">{title}</h1>" +
                   "</div>" +
                   "<div style=\"padding:32px 40px;\">" +
                   $"<p style=\"margin:0 0 24px;font-size:16px;color:#334155;\">{introHtml}</p>" +
                   highlightHtml +
                   $"<p style=\"margin:0;font-size:15px;color:#475569;\">{footnote}</p>" +
                   "</div>" +
                   "<div style=\"padding:24px 40px 32px;border-top:1px solid #e2e8f0;background:#fff;display:flex;align-items:center;gap:16px;\">" +
                   "<div style=\"width:56px;height:56px;border-radius:16px;background:linear-gradient(135deg,#2563eb,#22c55e);display:flex;align-items:center;justify-content:center;\">" +
                   "<span style=\"font-size:20px;font-weight:800;color:#ffffff;font-family:Segoe UI,Arial,sans-serif;\">M5</span>" +
                   "</div>" +
                   "<div>" +
                   "<p style=\"margin:0;font-size:15px;font-weight:700;color:#0f172a;\">M5CS</p>" +
                   "<p style=\"margin:4px 0 0;font-size:13px;color:#64748b;\">Secure document exchange for institutions.</p>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</body></html>";
        }

        private static string BuildUserRegistrationOtpEmailPlainTextBody(string otpCode, DateTimeOffset expiresAt)
        {
            return $"Welcome to DocuVault!\n\n" +
                   "We’re glad you’re joining us. To complete your registration and verify your email, please use the code below.\n\n" +
                   $"Your verification code is: {otpCode}\n\n" +
                   $"This code will expire on {expiresAt:yyyy-MM-dd HH:mm}.\n\n" +
                   "If you did not create this account, please ignore this message and contact support.\n\n" +
                   "Thank you,\n" +
                   "M5CS | DocuVault Security Team\n";
        }

        private static string BuildUserRegistrationOtpEmailHtmlBody(string otpCode, DateTimeOffset expiresAt)
        {
            var logoData = GetInlineLogoSvgBase64();
            return $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;background:#f3f4f6;margin:0;padding:0;\">" +
                   "<div style=\"max-width:680px;margin:0 auto;padding:32px 16px;\">" +
                   "<div style=\"background:#ffffff;border-radius:24px;box-shadow:0 24px 80px rgba(15,23,42,0.08);overflow:hidden;\">" +
                   "<div style=\"padding:32px 40px;background:#0f172a;color:#f8fafc;text-align:center;\">" +
                   $"<img src=\"data:image/svg+xml;base64,{logoData}\" alt=\"M5CS logo\" width=60 height=60 style=\"display:block;margin:0 auto 18px;\" />" +
                   "<p style=\"margin:0;font-size:14px;letter-spacing:0.16em;color:#94a3b8;text-transform:uppercase;\">Welcome</p>" +
                   "<h1 style=\"margin:16px 0 0;font-size:30px;font-weight:700;line-height:1.1;\">Verify your email to complete registration</h1>" +
                   "</div>" +
                   "<div style=\"padding:32px 40px;\">" +
                   "<p style=\"margin:0 0 24px;font-size:16px;color:#334155;\">Hello,<br/>Welcome to DocuVault. Please verify your email address to complete your account registration and activate your profile.</p>" +
                   $"<div style=\"padding:26px 24px;background:#eff6ff;border-radius:18px;border:1px solid #dbeafe;text-align:center;margin-bottom:32px;\">" +
                   $"<p style=\"margin:0;font-size:32px;font-weight:800;color:#0f172a;letter-spacing:0.16em;\">{otpCode}</p>" +
                   $"<p style=\"margin:8px 0 0;font-size:14px;color:#475569;\">This code expires on {expiresAt:yyyy-MM-dd HH:mm}.</p>" +
                   "</div>" +
                   "<p style=\"margin:0 0 18px;font-size:15px;color:#475569;\">Use this one-time code to verify your email address.</p>" +
                   "<p style=\"margin:0;font-size:15px;color:#475569;\">If you did not create this account, please ignore this message or contact support.</p>" +
                   "</div>" +
                   "<div style=\"padding:24px 40px 32px;border-top:1px solid #e2e8f0;background:#fff;display:flex;align-items:center;gap:16px;\">" +
                   $"<div style=\"width:56px;height:56px;border-radius:16px;background:linear-gradient(135deg,#2563eb,#22c55e);display:flex;align-items:center;justify-content:center;\">" +
                   $"<span style=\"font-size:20px;font-weight:800;color:#ffffff;font-family:Segoe UI,Arial,sans-serif;\">M5</span>" +
                   "</div>" +
                   "<div>" +
                   $"<p style=\"margin:0;font-size:15px;font-weight:700;color:#0f172a;\">M5CS</p>" +
                   $"<p style=\"margin:4px 0 0;font-size:13px;color:#64748b;\">Secure document exchange for institutions.</p>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</body></html>";
        }

        private static string BuildInstitutionOtpEmailPlainTextBody(string institutionName, string otpCode, DateTimeOffset expiresAt)
        {
            return $"Hello,\n\n" +
                   $"You are attempting to access the {institutionName} institution portal.\n\n" +
                   "Use the one-time access code below to continue.\n\n" +
                   $"Your institution portal access code is: {otpCode}\n\n" +
                   $"This code will expire on {expiresAt:yyyy-MM-dd HH:mm}.\n\n" +
                   "Do not share this code with anyone.\n\n" +
                   "If you did not request this access code, please contact your administrator immediately.\n\n" +
                   "Thank you,\n" +
                   "M5CS | DocuVault Security Team\n";
        }

        private static string BuildInstitutionOtpEmailHtmlBody(string institutionName, string otpCode, DateTimeOffset expiresAt)
        {
            var logoData = GetInlineLogoSvgBase64();
            return $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;background:#f3f4f6;margin:0;padding:0;\">" +
                   "<div style=\"max-width:680px;margin:0 auto;padding:32px 16px;\">" +
                   "<div style=\"background:#ffffff;border-radius:24px;box-shadow:0 24px 80px rgba(15,23,42,0.08);overflow:hidden;\">" +
                   "<div style=\"padding:32px 40px;background:#0f172a;color:#f8fafc;text-align:center;\">" +
                   $"<img src=\"data:image/svg+xml;base64,{logoData}\" alt=\"M5CS logo\" width=60 height=60 style=\"display:block;margin:0 auto 18px;\" />" +
                   "<p style=\"margin:0;font-size:14px;letter-spacing:0.16em;color:#94a3b8;text-transform:uppercase;\">Institution access</p>" +
                   "<h1 style=\"margin:16px 0 0;font-size:30px;font-weight:700;line-height:1.1;\">Your institution portal access code</h1>" +
                   "</div>" +
                   "<div style=\"padding:32px 40px;\">" +
                   $"<p style=\"margin:0 0 24px;font-size:16px;color:#334155;\">Hello,<br/>You are requesting access to the <strong>{institutionName}</strong> institution portal. Use the one-time code below to continue securely.</p>" +
                   $"<div style=\"padding:26px 24px;background:#eff6ff;border-radius:18px;border:1px solid #dbeafe;text-align:center;margin-bottom:32px;\">" +
                   $"<p style=\"margin:0;font-size:32px;font-weight:800;color:#0f172a;letter-spacing:0.16em;\">{otpCode}</p>" +
                   $"<p style=\"margin:8px 0 0;font-size:14px;color:#475569;\">This code expires on {expiresAt:yyyy-MM-dd HH:mm}.</p>" +
                   "</div>" +
                   "<p style=\"margin:0 0 18px;font-size:15px;color:#475569;\">Do not share this code with anyone else.</p>" +
                   "<p style=\"margin:0;font-size:15px;color:#475569;\">If you did not request this access code, contact your institution administrator immediately.</p>" +
                   "</div>" +
                   "<div style=\"padding:24px 40px 32px;border-top:1px solid #e2e8f0;background:#fff;display:flex;align-items:center;gap:16px;\">" +
                   $"<div style=\"width:56px;height:56px;border-radius:16px;background:linear-gradient(135deg,#2563eb,#22c55e);display:flex;align-items:center;justify-content:center;\">" +
                   $"<span style=\"font-size:20px;font-weight:800;color:#ffffff;font-family:Segoe UI,Arial,sans-serif;\">M5</span>" +
                   "</div>" +
                   "<div>" +
                   $"<p style=\"margin:0;font-size:15px;font-weight:700;color:#0f172a;\">M5CS</p>" +
                   $"<p style=\"margin:4px 0 0;font-size:13px;color:#64748b;\">Secure document exchange for institutions.</p>" +
                   "</div>" +
                   "</div>" +
                   "</div>" +
                   "</body></html>";
        }

        private static string GetInlineLogoSvgBase64()
        {
            const string svg =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 120 120\">" +
                "<defs>" +
                "<linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">" +
                "<stop offset=\"0%\" stop-color=\"#2563eb\"/><stop offset=\"100%\" stop-color=\"#22c55e\"/></linearGradient>" +
                "</defs>" +
                "<rect x=\"12\" y=\"12\" width=\"96\" height=\"96\" rx=\"24\" fill=\"url(#g)\"/>" +
                "<path d=\"M36 84 L36 48 A12 12 0 0 1 60 48 L60 84\" fill=\"none\" stroke=\"#ffffff\" stroke-width=\"10\" stroke-linecap=\"round\"/>" +
                "<path d=\"M64 84 L64 36 A12 12 0 0 1 88 36 L88 84\" fill=\"none\" stroke=\"#ffffff\" stroke-width=\"10\" stroke-linecap=\"round\"/>" +
                "<text x=\"50%\" y=\"90%\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"24\" font-weight=\"700\" fill=\"#ffffff\" text-anchor=\"middle\">M5</text>" +
                "</svg>";

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        }
    }
}
