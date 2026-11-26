using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Notifications;

public class EmailService
{
    private readonly AmazonSimpleEmailServiceClient _ses;
    private readonly string _fromEmail;
    private readonly string _toEmail;
    private readonly string _dashboardUrl;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _logger = logger;

        var accessKey = config["AWS:SES:AccessKeyId"] ?? config["AWS:AccessKeyId"]!;
        var secretKey = config["AWS:SES:SecretAccessKey"] ?? config["AWS:SecretAccessKey"]!;
        var region = RegionEndpoint.GetBySystemName(config["AWS:Region"] ?? "us-east-2");

        _ses = new AmazonSimpleEmailServiceClient(accessKey, secretKey, region);
        _fromEmail = config["AWS:SES:FromEmail"] ?? "noreply@example.com";
        _toEmail = config["AWS:SES:ToEmail"] ?? "admin@example.com";
        _dashboardUrl = config["Pipeline:DashboardUrl"] ?? "https://forgeai.codetrend.com";
    }

    public record DigestItem(int Id, string Title, int Score, string? Type, string? UseCase);

    public async Task SendDigestAsync(List<DigestItem> items)
    {
        if (items.Count == 0) return;

        var sorted = items.OrderByDescending(i => i.Score).ToList();
        var top = sorted.Where(i => i.Score >= 80).ToList();
        var strong = sorted.Where(i => i.Score >= 70 && i.Score < 80).ToList();

        var subject = $"ForgeAI: {items.Count} ideas need your attention";
        var dashboardUrl = _dashboardUrl;

        string RenderSection(string label, string color, List<DigestItem> group)
        {
            if (group.Count == 0) return "";
            var rows = string.Join("", group.Select(i =>
            {
                var reviewUrl = $"{dashboardUrl}/#/detail/{i.Id}";
                var typeBadge = !string.IsNullOrEmpty(i.Type) && i.Type != "unknown"
                    ? $"<span style='background:#242836;color:#94a3b8;font-size:10px;padding:2px 6px;border-radius:3px;text-transform:uppercase;letter-spacing:0.5px;margin-right:6px;'>{i.Type}</span>"
                    : "";
                var useCase = !string.IsNullOrEmpty(i.UseCase)
                    ? $"<div style='color:#94a3b8;font-size:12px;line-height:1.4;margin-top:4px;'>{System.Net.WebUtility.HtmlEncode(i.UseCase)}</div>"
                    : "";
                return $@"
                <tr>
                  <td style='padding:14px 16px; border-bottom:1px solid #2e3347;'>
                    <div style='display:flex;justify-content:space-between;align-items:center;'>
                      <div>
                        <span style='color:#e2e8f0;font-size:14px;font-weight:600;'>{System.Net.WebUtility.HtmlEncode(i.Title)}</span>
                        <span style='color:{color};font-weight:700;font-size:14px;margin-left:8px;'>{i.Score}/100</span>
                      </div>
                      <a href='{reviewUrl}' style='color:#6366f1;text-decoration:none;font-size:12px;'>Review →</a>
                    </div>
                    <div style='margin-top:6px;'>{typeBadge}</div>
                    {useCase}
                  </td>
                </tr>";
            }));

            return $@"
            <tr>
              <td style='padding:16px 32px 8px 32px;'>
                <span style='color:{color};font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;'>{label} ({group.Count})</span>
              </td>
            </tr>
            <tr>
              <td style='padding:0 16px;'>
                <table width='100%' cellpadding='0' cellspacing='0' style='background:#242836;border-radius:8px;overflow:hidden;'>
                  {rows}
                </table>
              </td>
            </tr>";
        }

        var html = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background:#0f1117;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#0f1117;padding:40px 20px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background:#1a1d27;border-radius:12px;overflow:hidden;'>
        <tr>
          <td style='background:#6366f1;padding:24px 32px;'>
            <span style='color:white;font-size:14px;font-weight:600;letter-spacing:1px;'>FORGEAI</span>
            <span style='color:rgba(255,255,255,0.7);font-size:12px;margin-left:8px;'>Venture Operations</span>
          </td>
        </tr>
        <tr>
          <td style='padding:32px 32px 16px 32px;'>
            <h1 style='color:#e2e8f0;font-size:22px;margin:0 0 8px 0;'>{items.Count} ideas ready for review</h1>
            <p style='color:#94a3b8;font-size:14px;margin:0;'>The evaluator finished scoring this week's opportunities. Here's what needs your Gate 1 approval.</p>
          </td>
        </tr>
        {RenderSection("Top picks", "#22c55e", top)}
        {RenderSection("Strong candidates", "#10b981", strong)}
        <tr>
          <td style='padding:24px 32px;text-align:center;'>
            <a href='{dashboardUrl}' style='background:#6366f1;color:white;text-decoration:none;padding:12px 32px;border-radius:8px;font-size:14px;font-weight:600;display:inline-block;'>Open Dashboard</a>
          </td>
        </tr>
        <tr>
          <td style='padding:16px 32px;border-top:1px solid #2e3347;'>
            <span style='color:#4b5563;font-size:11px;'>ForgeAI Venture Operations &middot; Digest email</span>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body></html>";

        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = new Destination { ToAddresses = new List<string> { _toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Html = new Content(html) }
                }
            });
            _logger.LogInformation("Digest email sent: {Count} ideas to {Email}", items.Count, _toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send digest email");
        }
    }

    public async Task SendMagicLinkAsync(string toEmail, string verifyUrl)
    {
        var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f5f6f8;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.06);'>
        <tr><td style='padding:32px 32px 24px;text-align:center;'>
          <h1 style='color:#1a1d27;font-size:20px;margin:0 0 8px;'>Sign in to ForgeAI</h1>
          <p style='color:#6b7085;font-size:14px;margin:0 0 24px;line-height:1.5;'>Click the button below to sign in. This link expires in 15 minutes.</p>
          <a href='{verifyUrl}' style='display:inline-block;background:#7c3aed;color:white;text-decoration:none;padding:14px 40px;border-radius:8px;font-size:15px;font-weight:600;'>Sign In</a>
          <p style='color:#9ca3af;font-size:11px;margin:24px 0 0;'>If you didn't request this, you can safely ignore this email.</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

        await SendToAsync(toEmail, "Sign in to ForgeAI", html);
    }

    public async Task SendVerificationAsync(string toEmail, string verifyUrl)
    {
        var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f5f6f8;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.06);'>
        <tr><td style='padding:32px 32px 24px;text-align:center;'>
          <h1 style='color:#1a1d27;font-size:20px;margin:0 0 8px;'>Verify your email</h1>
          <p style='color:#6b7085;font-size:14px;margin:0 0 24px;line-height:1.5;'>Click the button below to verify your email and activate your ForgeAI account.</p>
          <a href='{verifyUrl}' style='display:inline-block;background:#7c3aed;color:white;text-decoration:none;padding:14px 40px;border-radius:8px;font-size:15px;font-weight:600;'>Verify Email</a>
          <p style='color:#9ca3af;font-size:11px;margin:24px 0 0;'>If you didn't create an account, you can safely ignore this email.</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

        await SendToAsync(toEmail, "Verify your ForgeAI email", html);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetUrl)
    {
        var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f5f6f8;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.06);'>
        <tr><td style='padding:32px 32px 24px;text-align:center;'>
          <h1 style='color:#1a1d27;font-size:20px;margin:0 0 8px;'>Reset your password</h1>
          <p style='color:#6b7085;font-size:14px;margin:0 0 24px;line-height:1.5;'>Click the button below to set a new password. This link expires in 15 minutes.</p>
          <a href='{resetUrl}' style='display:inline-block;background:#7c3aed;color:white;text-decoration:none;padding:14px 40px;border-radius:8px;font-size:15px;font-weight:600;'>Reset Password</a>
          <p style='color:#9ca3af;font-size:11px;margin:24px 0 0;'>If you didn't request this, you can safely ignore this email.</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

        await SendToAsync(toEmail, "Reset your ForgeAI password", html);
    }

    public async Task SendReportReadyAsync(string toEmail, int opportunityId, string title)
    {
        var reportUrl = $"{_dashboardUrl}/report/{opportunityId}";
        var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f5f6f8;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.06);'>
        <tr><td style='padding:32px 32px 24px;text-align:center;'>
          <h1 style='color:#1a1d27;font-size:20px;margin:0 0 8px;'>Your report is ready</h1>
          <p style='color:#6b7085;font-size:14px;margin:0 0 4px;line-height:1.5;'>We finished analyzing your idea:</p>
          <p style='color:#1a1d27;font-size:16px;font-weight:600;margin:0 0 24px;'>{System.Net.WebUtility.HtmlEncode(title)}</p>
          <a href='{reportUrl}' style='display:inline-block;background:#7c3aed;color:white;text-decoration:none;padding:14px 40px;border-radius:8px;font-size:15px;font-weight:600;'>View Report</a>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

        await SendToAsync(toEmail, $"Your report for \"{title}\" is ready", html);
    }

    private async Task SendToAsync(string toEmail, string subject, string html)
    {
        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = new Destination { ToAddresses = new List<string> { toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Html = new Content(html) }
                }
            });
            _logger.LogInformation("Email sent: '{Subject}' to {Email}", subject, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email '{Subject}' to {Email}", subject, toEmail);
        }
    }

    public async Task SendApprovalNeededAsync(int opportunityId, string title, string summary, int score, string reasoning)
    {
        var subject = $"ForgeAI Approval Needed: {title}";
        var reviewUrl = $"{_dashboardUrl}/#/detail/{opportunityId}";
        var dashboardUrl = _dashboardUrl;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; background:#0f1117; font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#0f1117; padding:40px 20px;'>
    <tr>
      <td align='center'>
        <table width='600' cellpadding='0' cellspacing='0' style='background:#1a1d27; border-radius:12px; overflow:hidden;'>
          <!-- Header -->
          <tr>
            <td style='background:#6366f1; padding:24px 32px;'>
              <span style='color:white; font-size:14px; font-weight:600; letter-spacing:1px;'>FORGEAI</span>
              <span style='color:rgba(255,255,255,0.7); font-size:12px; margin-left:8px;'>Venture Operations</span>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style='padding:32px;'>
              <h1 style='color:#e2e8f0; font-size:20px; margin:0 0 8px 0;'>{title}</h1>
              <span style='display:inline-block; background:#431407; color:#fb923c; font-size:12px; font-weight:600; padding:4px 12px; border-radius:4px; margin-bottom:24px;'>Pending Approval to Prototype</span>

              <table width='100%' cellpadding='0' cellspacing='0' style='margin:24px 0;'>
                <tr>
                  <td style='padding:12px 16px; background:#242836; border-radius:8px 8px 0 0; border-bottom:1px solid #2e3347;'>
                    <span style='color:#94a3b8; font-size:12px;'>Summary</span><br>
                    <span style='color:#e2e8f0; font-size:14px; line-height:1.5;'>{summary}</span>
                  </td>
                </tr>
                <tr>
                  <td style='padding:12px 16px; background:#242836; border-bottom:1px solid #2e3347;'>
                    <span style='color:#94a3b8; font-size:12px;'>Overall Score</span><br>
                    <span style='color:{(score >= 70 ? "#22c55e" : score >= 40 ? "#f59e0b" : "#ef4444")}; font-size:24px; font-weight:700;'>{score}/100</span>
                  </td>
                </tr>
                <tr>
                  <td style='padding:12px 16px; background:#242836; border-radius:0 0 8px 8px;'>
                    <span style='color:#94a3b8; font-size:12px;'>Why Selected</span><br>
                    <span style='color:#e2e8f0; font-size:13px; line-height:1.5;'>{reasoning}</span>
                  </td>
                </tr>
              </table>

              <p style='color:#f97316; font-size:14px; font-weight:600; margin:24px 0 16px 0;'>Action needed: Approve or reject prototype build</p>

              <!-- CTA Button -->
              <table cellpadding='0' cellspacing='0' style='margin:0 0 16px 0;'>
                <tr>
                  <td style='background:#6366f1; border-radius:8px; padding:14px 32px;'>
                    <a href='{reviewUrl}' style='color:white; text-decoration:none; font-size:15px; font-weight:600;'>Review Idea</a>
                  </td>
                </tr>
              </table>

              <!-- Secondary links -->
              <table cellpadding='0' cellspacing='0'>
                <tr>
                  <td style='padding-right:16px;'>
                    <a href='{dashboardUrl}' style='color:#94a3b8; font-size:12px; text-decoration:none;'>View Dashboard</a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style='padding:16px 32px; border-top:1px solid #2e3347;'>
              <span style='color:#4b5563; font-size:11px;'>ForgeAI Venture Operations &middot; Automated notification</span>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = new Destination { ToAddresses = new List<string> { _toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content(html)
                    }
                }
            });

            _logger.LogInformation("Approval email sent for '{Title}' to {Email}", title, _toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send approval email for '{Title}'", title);
        }
    }
}
