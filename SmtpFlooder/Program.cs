using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Threading;
using MimeKit;
// using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace SmtpFlooder
{
    class Program
    {
        private static int _sentMessageCount = 0;
        private static int _exceptionCount = 0;
        private static readonly ManualResetEvent _cancelSignal = new ManualResetEvent(false);
        private static Stopwatch _stopwatch;

        static void Main(string[] args)
        {
            var hostname = "localhost";
            var threads = 25;

            if (args.Length == 2)
            {
                hostname = args[0];
                threads = Convert.ToInt32(args[1]);
            }

            Console.CancelKeyPress += (sender, cancelEventArgs) =>
            {
                cancelEventArgs.Cancel = true;
                _cancelSignal.Set();
            };

            Flood(hostname, threads);
        }

        private static void Flood(string hostname, int threadCount)
        {
            var threads = new List<Thread>(threadCount);

            for (var i = 0; i < threadCount; i++)
            {
                var thread = new Thread(() => SendSmtpMessage(hostname));
                threads.Add(thread);
            }

            var monitorThread = new Thread(Monitor);
            monitorThread.Start();

            _stopwatch = Stopwatch.StartNew();
            foreach (var thread in threads)
                thread.Start();

            foreach (var thread in threads)
                thread.Join();

            monitorThread.Join();

            _stopwatch.Stop();

            PrintStatus();
        }

        private static void Monitor()
        {
            while (!_cancelSignal.WaitOne(TimeSpan.FromMilliseconds(100)))
                PrintStatus();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrintStatus()
        {
            Console.Write($"{_sentMessageCount:D5} messages attempts in {_stopwatch.Elapsed:G} at a rate of {(_sentMessageCount / _stopwatch.Elapsed.TotalSeconds):N2}/sec. {_exceptionCount:D5} exceptions were thrown\r");
        }


        //private static void SendSmtpMessage(string hostname)
        //{
        //    do
        //    {
        //        try
        //        {
        //            using (var client = new SmtpClient())
        //            {
        //                client.Connect(hostname, 0);
        //                var mailMessage = new MimeMessage
        //                {
        //                    From = {new MailboxAddress("test@domain.com")},
        //                    To = {new MailboxAddress("999999999@domain.com")},
        //                    Subject = "Test",
        //                    Body = new TextPart("plain")
        //                    {
        //                        Text = $"Test message"
        //                    }
        //                };

        //                do
        //                {
        //                    var messageId = Interlocked.Increment(ref _sentMessageCount);
        //                    client.Send(mailMessage);
        //                } while (!_cancelSignal.WaitOne(0));
        //            }
        //        }
        //        catch
        //        {
        //            Interlocked.Increment(ref _exceptionCount);
        //            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        //        }
        //    } while (!_cancelSignal.WaitOne(0));
        //}

        private static void SendSmtpMessage(string hostname)
        {
            do
            {
                try
                {
                    using (var client = new SmtpClient(hostname))
                    {
                        do
                        {
                            var messageId = Interlocked.Increment(ref _sentMessageCount);
                            var mailMessage = new MailMessage(new MailAddress("test@domain.com"), new MailAddress("99999999@domain.com"))
                            {
                                Subject = "Test",
                                Body = $"Test message {messageId:N0}"
                            };
                            client.Send(mailMessage);
                            //client.Send(mailMessage);
                        } while (!_cancelSignal.WaitOne(0));
                    }
                }
                catch
                {
                    Interlocked.Increment(ref _exceptionCount);
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }
            } while (!_cancelSignal.WaitOne(0));
        }
    }
}