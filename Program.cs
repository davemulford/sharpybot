using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace SharpyBot
{
    class Program
    {
        private static char[] ChanTypes;
        private static Dictionary<string, int> Karma = new Dictionary<string, int>();

        static async Task HandleMessageAsync(NetworkStream stream, string fromNick, string toNick, string message, CancellationToken cancelToken)
        {
            if (IrcUtils.IsChan(toNick, ChanTypes))
            {
                // Check for karma
                Dictionary<string, int> newKarma = IrcUtils.FindKarma(message);
                StringBuilder karmaString = new StringBuilder($"PRIVMSG {toNick} :Karma!");

                bool firstEntry = true;
                foreach(KeyValuePair<string, int> entry in newKarma)
                {
                    if (Karma.ContainsKey(entry.Key))
                    {
                        Karma[entry.Key] += entry.Value;
                    }
                    else
                    {
                        Karma.Add(entry.Key, entry.Value);
                    }

                    if (firstEntry)
                    {
                        firstEntry = false;
                        karmaString.Append($" {entry.Key} has {Karma[entry.Key]} points of karma!");
                    }
                    else
                    {
                        karmaString.Append($" {entry.Key} has {Karma[entry.Key]}!");
                    }
                }

                if (newKarma.Any())
                    await SendRawMessageAsync(stream, karmaString.ToString(), cancelToken);
            }
            else
            {
                // Assume PRIVMSG to self

                // TODO Authentication?
                // TODO Commands?
                //   - Like joining/leaving channels
                //   - Manual karma adjust
                //   - Disconnecting from server or recycling connection
            }
        }

        static async Task SendRawMessageAsync(NetworkStream stream, string message, CancellationToken cancelToken)
        {
            // IRC messages are CRLF terminated. Make it so!
            if (!message.EndsWith("\r\n"))
            {
                message += "\r\n";
            }

            byte[] sendBuffer = System.Text.ASCIIEncoding.ASCII.GetBytes(message);

            await stream.WriteAsync(
                buffer: sendBuffer,
                offset: 0,
                size: sendBuffer.Length,
                cancellationToken: cancelToken
            );
        }

        static async Task RunClientAsync()
        {
            // TODO Logging?

            IrcConfiguration config = IrcConfiguration.Initialize();
            TcpClient client = new TcpClient();

            try
            {
                await client.ConnectAsync(config.Server, config.Port);
            }
            catch (Exception exception)
            {
                // TODO Deal with exception
            }

            // Send a few of the basic IRC startup messages. This includes:
            //      USER {nick} {nick} {nick}: {phrase}
            //      NICK {nick}
            //      JOIN #channel
            var stream = client.GetStream();

            // TODO actually do something with cancelToken (e.g. if the bot is told to quit)
            CancellationTokenSource cancellationSource = new CancellationTokenSource();
            CancellationToken cancelToken = cancellationSource.Token;

            await SendRawMessageAsync(stream, $"NICK {config.Nick}", cancelToken);
            await SendRawMessageAsync(stream, $"USER {config.Nick} 0 * :SharpieBot", cancelToken);
            await SendRawMessageAsync(stream, $"JOIN #mychan", cancelToken);

            // Begin the infinite read loop
            byte[] readBuffer = new byte[1024];

            // Regex to parse capabilities message sent during registration. Has the form:
            // :<server-name> 005 <your-nick> <cap0> <cap1> ... <capN> :are supported by this server
            Regex capsMsgRegex = new Regex(@"^:[^\s]+\s005\s.+CHANTYPES=(?<chantypes>[^\s]+)", RegexOptions.Compiled);

            // Regex to parse private messages into their individual parts
            Regex privMsgRegex = new Regex(@"^:(?<from_nick>\w+)![^\s]+\sPRIVMSG\s(?<to_nick>[^\s]+)\s:(?<msg>.*)$", RegexOptions.Compiled);

            for (;;)
            {
                Array.Clear(readBuffer, 0, readBuffer.Length);

                // TODO Maybe handle this exception?
                int bytesRead = await stream.ReadAsync(
                    buffer: readBuffer,
                    offset: 0,
                    size: 1024,
                    cancellationToken: cancelToken
                );

                if (bytesRead > 0)
                {
                    string str = System.Text.ASCIIEncoding.ASCII.GetString(readBuffer, 0, bytesRead);

                    foreach (string message in str.Split("\r\n"))
                    {
                        Match capsMatch = capsMsgRegex.Match(message);
                        Match privMsgMatch = privMsgRegex.Match(message);

                        // As a bot we only care about private messages
                        // to channels and from other users
                        if (privMsgMatch.Success)
                        {
                            string fromNick = privMsgMatch.Groups["from_nick"].Value;
                            string toNick = privMsgMatch.Groups["to_nick"].Value;
                            string msgText = privMsgMatch.Groups["msg"].Value;

                            await HandleMessageAsync(stream, fromNick, toNick, msgText, cancelToken);
                        }
                        else if (message.StartsWith("PING"))
                        {
                            Console.WriteLine("PING? PONG!");
                            string pongServer = message.Split(' ')[1];
                            await SendRawMessageAsync(stream, $"PONG {pongServer}", cancelToken);
                        }
                        else if (capsMatch.Success)
                        {
                            string chanTypes = capsMatch.Groups["chantypes"].Value;
                            ChanTypes = chanTypes.ToCharArray();
                        }
                        else
                        {
                            Console.WriteLine($"UNKNOWN -- {message}");
                        }
                    }
                }
            }

            // TODO Maybe do something with cancelToken???
            // You can't reach this code dummy!
            await SendRawMessageAsync(stream, $"QUIT", cancelToken);
            client.Close();
        }

        static void Main(string[] args) => RunClientAsync().Wait();
    }
}
