using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Protocol;

namespace SmtpServer.IO
{
    public interface INetworkClient : IDisposable
    {
        /// <summary>
        /// Returns a series a buffer segments until the continuation predicate indicates that the method should complete.
        /// </summary>
        /// <param name="continue">The predicate to apply to the byte to determine if the function should continue reading.</param>
        /// <param name="count">The number of bytes to consume.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The list of buffers that contain the bytes matching while the predicate was true.</returns>
        IReadOnlyList<ArraySegment<byte>> ReadAsync(Func<byte, bool> @continue, long count = Int64.MaxValue);

        /// <summary>
        /// Write a list of byte array segments.
        /// </summary>
        /// <param name="buffers">The list of array segment buffers to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that asynchronously performs the operation.</returns>
        void WriteAsync(IReadOnlyList<ArraySegment<byte>> buffers);

        /// <summary>
        /// Flush the write buffers to the stream.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous flush operation.</returns>
        void FlushAsync();

        /// <summary>
        /// Upgrade to a secure stream.
        /// </summary>
        /// <param name="certificate">The X509Certificate used to authenticate the server.</param>
        /// <param name="protocols">The value that represents the protocol used for authentication.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that asynchronously performs the operation.</returns>
        void UpgradeAsync(X509Certificate certificate, SslProtocols protocols);

        /// <summary>
        /// Returns a value indicating whether or not the current client is secure.
        /// </summary>
        bool IsSecure { get; }
    }

    public static class NetworkClientExtensions
    {
        /// <summary>
        /// Returns a continuous segment of bytes until the given sequence is reached.
        /// </summary>
        /// <param name="client">The byte stream to perform the operation on.</param>
        /// <param name="sequence">The sequence to match to enable the read operation to complete.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The array segment that defines a continuous segment of characters that have matched the predicate.</returns>
        public static IReadOnlyList<ArraySegment<byte>> ReadUntilAsync(this INetworkClient client, byte[] sequence)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var found = 0;
            return client.ReadAsync(current =>
            {
                found = current == sequence[found]
                    ? found + 1
                    : current == sequence[0] ? 1 : 0;

                return found < sequence.Length;
            });
        }

        /// <summary>
        /// Read a line from the byte stream.
        /// </summary>
        /// <param name="client">The stream to read a line from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The string that was read from the stream.</returns>
        public static IReadOnlyList<ArraySegment<byte>> ReadLineAsync(this INetworkClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            return Trim(client.ReadUntilAsync(new byte[] { 13, 10 }), new byte[] { 13, 10 });
        }

        /// <summary>
        /// Read a line from the byte stream.
        /// </summary>
        /// <param name="client">The stream to read a line from.</param>
        /// <param name="encoding">The encoding to use when converting to a string representation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The string that was read from the stream.</returns>
        public static string ReadLineAsync(this INetworkClient client, Encoding encoding)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var blocks = client.ReadLineAsync();

            return blocks.Count == 0
                ? null
                : encoding.GetString(blocks.SelectMany(block => block).ToArray());
        }

        /// <summary>
        /// Writes a byte array to the underlying client stream.
        /// </summary>
        /// <param name="client">The stream to write the line to.</param>
        /// <param name="buffer">The byte array buffer to write to the client stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the operation.</returns>
        public static void WriteAsync(this INetworkClient client, byte[] buffer)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            client.WriteAsync(new [] { new ArraySegment<byte>(buffer) });
        }

        /// <summary>
        /// Writes a line to the client stream.
        /// </summary>
        /// <param name="client">The stream to write the line to.</param>
        /// <param name="text">The text to write to the client stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the operation.</returns>
        public static void WriteLineAsync(this INetworkClient client, string text)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            WriteLineAsync(client, text, Encoding.ASCII);
        }

        /// <summary>
        /// Read a line from the byte stream.
        /// </summary>
        /// <param name="client">The stream to write the line to.</param>
        /// <param name="text">The text to write to the client stream.</param>
        /// <param name="encoding">The encoding to use when converting the bytes to a text representation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the operation.</returns>
        public static void WriteLineAsync(this INetworkClient client, string text, Encoding encoding)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var newLine = new string(new[] {(char) 13, (char) 10});

            client.WriteAsync(encoding.GetBytes(text + newLine));
        }

        /// <summary>
        /// Read a blank-line delimited block.
        /// </summary>
        /// <param name="client">The stream to read a line from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The buffers that were read until the block was terminated.</returns>
        public static IReadOnlyList<ArraySegment<byte>> ReadBlockAsync(this INetworkClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var blocks = client.ReadUntilAsync(new byte[] { 13, 10, 13, 10 });

            return Unstuff(Trim(blocks, new byte[] { 13, 10, 13, 10 })).ToList();
        }

        /// <summary>
        /// Read a dot terminated block.
        /// </summary>
        /// <param name="client">The stream to read a line from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The buffers that were read until the block was terminated.</returns>
        public static IReadOnlyList<ArraySegment<byte>> ReadDotBlockAsync(this INetworkClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var blocks = client.ReadUntilAsync(new byte[] { 13, 10, 46, 13, 10 });

            return Unstuff(Trim(blocks, new byte[] { 13, 10, 46, 13, 10 })).ToList();
        }

        /// <summary>
        /// Trim a given number of bytes from the end.
        /// </summary>
        /// <param name="segments">The list of segments to trim the sequence from.</param>
        /// <param name="count">The number of bytes to remove from the end.</param>
        /// <returns>The list of segments that have been trimmed by the given number of bytes.</returns>
        static IReadOnlyList<ArraySegment<byte>> Trim(IReadOnlyList<ArraySegment<byte>> segments, int count)
        {
            var list = new List<ArraySegment<byte>>(segments);

            var remaining = count;
            for (var i = list.Count - 1; i >= 0 && remaining > 0; i--)
            {
                count = Math.Min(remaining, list[i].Count);

                if (count == list[i].Count)
                {
                    list.RemoveAt(i);
                }
                else
                {
                    list[i] = Trim(list[i], count);
                }

                remaining -= count;
            }

            return list;
        }

        /// <summary>
        /// Trim a given number of bytes from a segment.
        /// </summary>
        /// <param name="segment">The segment to truncate.</param>
        /// <param name="count">The number of bytes to trim.</param>
        /// <returns>The segment that represents the original segment with the given number of bytes trimmed.</returns>
        static ArraySegment<byte> Trim(ArraySegment<byte> segment, int count)
        {
            return new ArraySegment<byte>(segment.Array, segment.Offset, segment.Count - count);
        }

        /// <summary>
        /// Trim a given sequence from the end of the segment list.
        /// </summary>
        /// <param name="segments">The list of segments to trim the sequence from.</param>
        /// <param name="sequence">The sequence to trim from the end of the block.</param>
        /// <returns>The list of segments that have been trimmed and have the sequence removed.</returns>
        static IReadOnlyList<ArraySegment<byte>> Trim(IReadOnlyList<ArraySegment<byte>> segments, byte[] sequence)
        {
            if (EndsWith(segments, sequence))
            {
                return Trim(segments, sequence.Length);
            }

            return segments;
        }

        /// <summary>
        /// Returns a value indicating whether or not the list of segments end with the given sequence of bytes.
        /// </summary>
        /// <param name="segments">The segments to test the byte sequence against.</param>
        /// <param name="sequence">The sequence of bytes to test at the end of the segments.</param>
        /// <returns>true if the segments end with the given sequence, false if not.</returns>
        static bool EndsWith(IReadOnlyList<ArraySegment<byte>> segments, byte[] sequence)
        {
            var state = sequence.Length - 1;

            for (var i = segments.Count - 1; i >= 0 && state >= 0; i--)
            {
                for (var j = segments[i].Count - 1; j >= 0 && state >= 0; j--)
                {
                    if (segments[i].Array[segments[i].Offset + j] != sequence[state--])
                    {
                        return false;
                    }
                }
            }

            return state < 0;
        }

        /// <summary>
        /// Unstuff the Dot Stuffing that can appear within a stream.
        /// </summary>
        /// <param name="segments">The list of segments to remove the dot-stuffing from.</param>
        /// <returns>The list of segments that have the dot-stuffing removed.</returns>
        static IEnumerable<ArraySegment<byte>> Unstuff(IReadOnlyList<ArraySegment<byte>> segments)
        {
            var sequence = new byte[] { 13, 10, 46, 46 };
            var state = 0;

            foreach (var segment in segments)
            {
                var start = 0;
                for (var i = 0; i < segment.Count; i++)
                {
                    if (segment.Array[segment.Offset + i] != sequence[state++])
                    {
                        state = segment.Array[segment.Offset + i] == sequence[0] ? 1 : 0;
                        continue;
                    }

                    if (state >= sequence.Length)
                    {
                        yield return new ArraySegment<byte>(segment.Array, segment.Offset + start, i - start);

                        state = segment.Array[segment.Offset + i] == sequence[0] ? 1 : 0;
                        start = i + 1;
                    }
                }

                if (start < segment.Count)
                {
                    yield return new ArraySegment<byte>(segment.Array, segment.Offset + start, segment.Count - start);
                }
            }
        }

        /// <summary>
        /// Reply to the client.
        /// </summary>
        /// <param name="client">The text stream to perform the operation on.</param>
        /// <param name="response">The response.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which performs the operation.</returns>
        public static void ReplyAsync(this INetworkClient client, SmtpResponse response)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            client.WriteLineAsync($"{(int)response.ReplyCode} {response.Message}");
            client.FlushAsync();
        }
    }
}