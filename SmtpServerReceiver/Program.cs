using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer;

namespace SmtpServerReceiver
{
    class Program
    {
        private static readonly CancellationTokenSource _smtpServerCancellationToken = new CancellationTokenSource();
        private static Task _smtpServerTask;
        private static Stopwatch _stopwatch;

        static void Main(string[] args)
        {

            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(workerThreads * 3, completionPortThreads * 3);

            Console.CancelKeyPress += (sender, cancelEventArgs) =>
            {
                cancelEventArgs.Cancel = true;
                _smtpServerCancellationToken.Cancel();
            };

            var options = new SmtpServerOptionsBuilder()
                .ServerName("server.domain.com")
                .Port(25)
                .MaxMessageSize(1048576)
                .MessageStore(new MessageStore())
                .Build();

            var smtpServer = new SmtpServer.SmtpServer(options);

            _stopwatch = Stopwatch.StartNew();
            var monitorThread = new Thread(Monitor);
            monitorThread.Start();

            _smtpServerTask = smtpServer.StartAsync(_smtpServerCancellationToken.Token);

            _smtpServerCancellationToken.Token.WaitHandle.WaitOne();

            _stopwatch.Stop();
            PrintStatus();
        }

        private static void Monitor()
        {
            while (!_smtpServerCancellationToken.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100)))
            {
                while (Console.KeyAvailable)
                {
                    if (Console.ReadKey(true).Key != ConsoleKey.Spacebar)
                        continue;

                    Interlocked.Exchange(ref MessageStore.ReceivedMessages, 0);
                    _stopwatch.Restart();
                }
                PrintStatus();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrintStatus()
        {
            ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
            Console.Write($"{MessageStore.ReceivedMessages:D5} messages received in {_stopwatch.Elapsed:G} at a rate of {(MessageStore.ReceivedMessages / _stopwatch.Elapsed.TotalSeconds):N2}/sec. {workerThreads}/{completionPortThreads}\r");
        }
    }
}