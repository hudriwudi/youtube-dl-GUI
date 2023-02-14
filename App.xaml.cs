using MimeKit;
using System;
using System.Configuration;
using System.Reflection;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        // catch unhandled exception and send bug report to ytdlguibugreports@gmail.com
        public void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            bool programIsInDebugMode = false; // change when releasing/debugging (debug -> true; release -> false)

            if (!programIsInDebugMode)
            {
                Exception ex = (Exception)e.ExceptionObject;

                MessageBox.Show("An unhandled exception just occurred:\n" + ex.Message + "\n\n" + "A report has been automatically sent to the developer.\nThe program will now close. Sorry for the inconvenience.",
                                                         "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);


                string subject = "YouTube-dl GUI => Unhandled Exception Report";
                string body = "<pre>" +
                              "The following crash report has been sent:\n\n" +
                              "version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                              "\nUser: " + Environment.UserName +
                              "\n\nException Message:\n" + ex.Message +
                              "\n\nException Data:\n" + ex.Data.ToString() +
                              "\n\nException Source:\n" + ex.Source +
                              "\n\nException InnerException:\n" + ex.InnerException +
                              "\n\nException TargetSite:\n" + ex.TargetSite +
                              "\n\nException StackTrace:\n" + ex.StackTrace +
                              "</pre>";

                SendEmail(subject, body);
            }

        }

        // https://mailtrap.io/blog/csharp-send-email-gmail/
        private void SendEmail(string subject, string body)
        {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress("YouTube-dl GUI Bug reporting", "ytdlguibugreports@gmail.com"));
            email.To.Add(new MailboxAddress("YouTube-dl GUI Bug reporting", "ytdlguibugreports@gmail.com"));

            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = body
            };

            using (var smtp = new MailKit.Net.Smtp.SmtpClient())
            {
                smtp.Connect("smtp.gmail.com", 587, false);

                // Note: only needed if the SMTP server requires authentication
                smtp.Authenticate("ytdlguibugreports@gmail.com", Youtube.DecryptText(ConfigurationManager.AppSettings["smtp_pwd"]));

                smtp.Send(email);
                smtp.Disconnect(true);
            }
        }
    }
}
