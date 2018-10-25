using System;

namespace SharpyBot
{
    public class IrcConfiguration
    {
        public string Server { get; set; }

        public int Port { get; set; }

        public string Nick { get; set; }

        public static IrcConfiguration Initialize()
        {
            return new IrcConfiguration()
            {
                Server = "localhost",
                Port = 6667,
                Nick = "sharpy"
            };
        }
    }
}