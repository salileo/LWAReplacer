using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace MS
{
    class Util
    {
        public static string GetLocalhostFQDN()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            return string.Format("{0}.{1}", properties.HostName, properties.DomainName);
        }

        public static string GetHostIpAddress()
        {
            IPAddress[] ipAddrsForHostName = Dns.GetHostAddresses(Dns.GetHostName());
            List<IPAddress> ipAddresses = new List<IPAddress>();

            foreach (IPAddress ipAddress in ipAddrsForHostName)
            {
                //only consider IPV4
                if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ipAddresses.Add(ipAddress);
            }

            if (ipAddresses.Count <= 0)
            {
                PrintError(String.Format("Cannot resolve specified host name {0}", Dns.GetHostName()));
                return string.Empty;
            }

            return ipAddresses[0].ToString();
        }

        public static void PrintRequest(string message)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        public static void PrintResponse(string message)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        public static void PrintCommandResponse(string message)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        public static void Print(string message, ConsoleColor foregroundColor, bool addCRLF)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = foregroundColor;
            if (addCRLF)
                Console.WriteLine(message);
            else
                Console.Write(message);
            Console.ForegroundColor = oldColor;
        }

        public static void PrintError(string message)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            PrintMessage(message);
            Console.ForegroundColor = oldColor;
        }

        public static void PrintMessage(string message)
        {
            Console.WriteLine(String.Format(
                   "[{0:HH:mm:ss.fff}] {1}",
                   DateTime.Now,
                   message
                   ));
        }

        public static void IsExisting(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                PrintError(string.Format("{0} doesn't exist", path));
                Environment.Exit(1);
            }
        }
    }
}
