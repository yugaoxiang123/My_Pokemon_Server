using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using MyPokemon.Utils;

namespace MyPokemon.Services;

public class EmailService
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromAddress;

    public EmailService(IConfiguration config)
    {
        var host = config["Email:SmtpHost"] ?? throw new Exception("未配置SMTP主机");
        var port = config.GetValue<int>("Email:SmtpPort");
        var username = config["Email:Username"] ?? throw new Exception("未配置邮箱用户名");
        var password = config["Email:Password"] ?? throw new Exception("未配置邮箱密码");
        _fromAddress = config["Email:FromAddress"] ?? username;

        _smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new System.Net.NetworkCredential(username, password)
        };
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var message = new MailMessage(_fromAddress, to, subject, body);
            await _smtpClient.SendMailAsync(message);
            ServerLogger.LogNetwork($"发送邮件成功 - 收件人: {to}");
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"发送邮件失败: {e.Message}");
            throw;
        }
    }
} 