using System;
using System.IO;
using System.Threading;
using Fiddler;
using System.Collections.Generic;

namespace MS
{
    public class ReplaceMapEntry
    {
        public string sourcePath;
        public string destinationPath;
    }

    public class LWAReplacer
    {
        static Proxy oSecureEndpoint;
        static string sSecureEndpointHostname = "localhost";

        static List<ReplaceMapEntry> replaceMap = new List<ReplaceMapEntry>();

        static public void Usage()
        {
            Console.WriteLine(@"Usage: LWAReplacer [/folderMap:<server folder to replace>@@<local build folder> /url:<start url> /browser:<IE/FF/CH>]
Options:
  /map  - server folder to replace & the local build folder
  /url        - <optional> Opens this URL in the specified browser.
  /browser    - <optional> Opens the URL if specified in this browser. Default = IE.

Note: The src & dest accept any level of path, and currently the replacer only supports replacing of JS/CSS/PNG/HTML files. 
      Other resources and aspx pages cannot be replaced. Also there should not be any query parameters to any of the mentioned content

Examples:
LwaReplacer /map:se-reachtest.reachtest.rtmp.selfhost.corp.microsoft.com/salillwa/scripts/@@d:\bin\dev\server\lwa\scripts\
LwaReplacer /map:se-reachtest.reachtest.rtmp.selfhost.corp.microsoft.com/salillwa/scripts/model/lync.client.conversation.js@@d:\bin\dev\server\lwa\scripts\model\lync.client.conversation.js
");

            Environment.Exit(1);
        }

        /// <summary>
        /// Report any unhandled exceptions that are going to cause Fiddler to die.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ue"></param>
        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs ue)
        {
            Exception unhandledException = (Exception)ue.ExceptionObject;
            Fiddler.FiddlerApplication.ReportException(unhandledException, "UnhandledException");
            Util.PrintCommandResponse("Got an unhandled exception, we're about to crash the tool, trying to do clear up work here ...");
            DoQuit();
        }

        static void Main(string[] args)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);

            string startUrl = string.Empty;
            WebBrowserType browser = WebBrowserType.IE;
            bool launchBrowser = false;

            foreach (String str in args)
            {
                string param = str.ToLower();
                if (param[0] == '-')
                {
                    param = "/" + param.Substring(1);
                }

                if (param.StartsWith("/map:"))
                {
                    string mapEntry = str.Substring("/map:".Length).ToLower();
                    ReplaceMapEntry entry = new ReplaceMapEntry();
                    int delim = mapEntry.IndexOf("@@");
                    if (delim >= 0)
                    {
                        string sourcePath = mapEntry.Substring(0, delim);
                        sourcePath = sourcePath.Replace("http://", "");
                        sourcePath = sourcePath.Replace("https://", "");
                        sourcePath = sourcePath.Replace("file://", "");
                        
                        string destinationPath = mapEntry.Substring(delim + 2);

                        //check replaced files
                        Util.IsExisting(destinationPath);

                        //check is dest is file or folder
                        if (File.Exists(destinationPath))
                        {
                            //replacement is a file
                            if (!sourcePath.EndsWith(".js") &&
                                !sourcePath.EndsWith(".css") &&
                                !sourcePath.EndsWith(".png") &&
                                !sourcePath.EndsWith(".html") &&
                                !sourcePath.EndsWith(".htm"))
                            {
                                Usage();
                            }
                        }
                        else
                        {
                            //replacement is a folder
                            if (!sourcePath.EndsWith("/"))
                                sourcePath += "/";

                            if (!destinationPath.EndsWith("\\"))
                                destinationPath += "\\";
                        }

                        entry.sourcePath = sourcePath;
                        entry.destinationPath = destinationPath;
                        replaceMap.Add(entry);
                    }
                }
                else if (param.StartsWith("/url:"))
                {
                    startUrl = str.Substring("/url:".Length);
                    launchBrowser = true;
                }
                else if (param.StartsWith("/browser:"))
                {
                    browser = WebBrowserLauncher.GetWebBrowserType(str.Substring("/browser:".Length));
                    launchBrowser = true;
                }
                else
                {
                    Usage();
                }
            }

            if (replaceMap.Count <= 0)
            {
                Usage();
            }

            CleanUpBrowserCache();

            try
            {
                InitializeFiddler();

                if (launchBrowser)
                    StartLWAClient(startUrl, browser);

                bool done = false;
                do
                {
                    Console.WriteLine("Enter a command [ctrl+c or q=quit]:");
                    Console.Write(">");

                    ConsoleKeyInfo cki = Console.ReadKey(true);
                    switch (cki.KeyChar)
                    {
                        case 'q':
                        case 'Q':
                            done = true;
                            DoQuit();
                            break;
                        default:
                            Console.WriteLine();
                            break;
                    }
                } while (!done);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                //in case an exception thrown, always do clear up work: close log, shutdown fiddler
                //FiddlerCore change IE proxy settings, make sure to revert it 
                DoQuit();
            }
        }

        /// <summary>
        /// When the user hits CTRL+C, this event fires.  We use this to shut down and unregister our FiddlerCore.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void OnCancelKeyPressed(object sender, ConsoleCancelEventArgs e)
        {
            DoQuit();
        }

        public static void DoQuit()
        {
            Util.PrintCommandResponse("WARNING: the connection with LWA will be terminated and LWA might not work.");
            Util.PrintCommandResponse("Shutting down...");

            //do the most important thing first
            //this will revert the system proxy settings
            if (null != oSecureEndpoint) oSecureEndpoint.Dispose();
            Fiddler.FiddlerApplication.Shutdown();
            Thread.Sleep(500);

            CleanUpBrowserCache();
        }

        private static void BeforeRequestCallback(Fiddler.Session oS)
        {
            // In order to enable response tampering, buffering mode must
            // be enabled; this allows FiddlerCore to permit modification of
            // the response in the BeforeResponse handler rather than streaming
            // the response to the client as the response comes in.
            oS.bBufferResponse = true;

            if ((oS.hostname == sSecureEndpointHostname) && (oS.port == 7777))
            {
                oS.utilCreateResponseAndBypassServer();
                oS.oResponse.headers.HTTPResponseStatus = "200 Ok";
                oS.oResponse["Content-Type"] = "text/html; charset=UTF-8";
                oS.oResponse["Cache-Control"] = "private, max-age=0";
                oS.utilSetResponseBody("<html><body>Request for https://" + sSecureEndpointHostname + ":7777 received. Your request was:<br /><plaintext>" + oS.oRequest.headers.ToString());
            }
        }

        private static void BeforeResponseCallback(Fiddler.Session oSession)
        {
            string url = oSession.url.ToLower();
            foreach (ReplaceMapEntry entry in replaceMap)
            {
                if (url.Contains(entry.sourcePath))
                {
                    string replacementFile = string.Empty;
                    if (!entry.sourcePath.EndsWith("/"))
                    {
                        replacementFile = entry.destinationPath;
                    }
                    else
                    {
                        if (url.Contains(".js") ||
                            url.Contains(".css") ||
                            url.Contains(".png") ||
                            url.Contains(".html") ||
                            url.Contains(".htm"))
                        {
                            int startIndex = url.IndexOf(entry.sourcePath);
                            startIndex += entry.sourcePath.Length;
                            replacementFile = url.Substring(startIndex, (url.Length - startIndex));

                            int queryParam = replacementFile.IndexOf("?");
                            if (queryParam > 0)
                                replacementFile = replacementFile.Substring(0, queryParam);

                            replacementFile = replacementFile.Replace("/", "\\");
                            replacementFile = entry.destinationPath + replacementFile;
                        }
                    }

                    if (!string.IsNullOrEmpty(replacementFile))
                    {
                        try
                        {
                            if (oSession.bHasResponse)
                            {
                                if (replacementFile.EndsWith(".png"))
                                {
                                    byte[] buffer = File.ReadAllBytes(replacementFile);
                                    if (buffer != null && buffer.Length > 0)
                                    {
                                        oSession.responseBodyBytes = buffer;
                                        oSession.oResponse["Content-Length"] = buffer.Length.ToString();
                                        oSession.oResponse["Content-Type"] = "image/png";

                                        Util.PrintMessage("Replaced " + replacementFile);
                                    }
                                    else
                                    {
                                        throw (new Exception());
                                    }
                                }
                                else //for text files
                                {
                                    string buffer = File.ReadAllText(replacementFile);
                                    if (buffer != null && buffer.Length > 0)
                                    {
                                        oSession.utilDecodeResponse();
                                        oSession.utilSetResponseBody(buffer);
                                        oSession.responseCode = 200;

                                        if (replacementFile.EndsWith(".js"))
                                            oSession.oResponse.headers.Add("Content-Type", "application/x-javascript");
                                        else if (replacementFile.EndsWith(".css"))
                                            oSession.oResponse.headers.Add("Content-Type", "text/css");
                                        else if (replacementFile.EndsWith(".html") || replacementFile.EndsWith(".htm"))
                                            oSession.oResponse.headers.Add("Content-Type", "text/html");

                                        Util.PrintMessage("Replaced " + replacementFile);
                                    }
                                    else
                                    {
                                        throw (new Exception());
                                    }
                                }
                            }
                            else
                            {
                                Util.PrintMessage("Waiting for response");
                            }
                        }
                        catch (Exception ex)
                        {
                            Util.PrintError("Could not replace file " + replacementFile + ". Error: " + ex.Message);
                        }
                    }

                    break;
                }
            }
        }

        private static void InitializeFiddler()
        {
            //Fiddler.FiddlerApplication.OnNotification += delegate(object sender, NotificationEventArgs oNEA) { Console.WriteLine("** NotifyUser: " + oNEA.NotifyString); };
            //Fiddler.FiddlerApplication.Log.OnLogString += delegate(object sender, LogEventArgs oLEA) { Console.WriteLine("** LogString: " + oLEA.LogString); };

            Fiddler.FiddlerApplication.BeforeRequest += new SessionStateHandler(BeforeRequestCallback);
            Fiddler.FiddlerApplication.BeforeResponse += new SessionStateHandler(BeforeResponseCallback);

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKeyPressed);
            Fiddler.CONFIG.IgnoreServerCertErrors = true;
            FiddlerApplication.Prefs.SetBoolPref("fiddler.network.streaming.abortifclientaborts", true);

            // For forward-compatibility with updated FiddlerCore libraries, it is strongly recommended that you 
            // start with the DEFAULT options and manually disable specific unwanted options.
            FiddlerCoreStartupFlags oFCSF = FiddlerCoreStartupFlags.Default;

            // E.g. uncomment the next line if you don't want FiddlerCore to act as the system proxy
            // oFCSF = (oFCSF & ~FiddlerCoreStartupFlags.RegisterAsSystemProxy);
            // or uncomment the next line if you don't want to decrypt SSL traffic.
            // oFCSF = (oFCSF & ~FiddlerCoreStartupFlags.DecryptSSL);
            //
            // NOTE: Because we haven't disabled the option to decrypt HTTPS traffic, makecert.exe 
            // must be present in this executable's folder.
            const int listenPort = 8877;
            Fiddler.FiddlerApplication.Startup(listenPort, oFCSF);

            string title = string.Format("Listening on port {0}@{1} [{2}]", listenPort, Util.GetHostIpAddress(), Util.GetLocalhostFQDN());
            Console.Title = title;

            Util.PrintMessage(title);
            Util.PrintMessage("Starting with settings: [" + oFCSF + "]");
            Util.PrintMessage("Using Gateway: " + ((CONFIG.bForwardToGateway) ? "TRUE" : "FALSE"));

            oSecureEndpoint = FiddlerApplication.CreateProxyEndpoint(7777, true, sSecureEndpointHostname);
            if (null != oSecureEndpoint)
            {
                Util.PrintMessage("Created secure end point listening on port 7777, using a HTTPS certificate for '" + sSecureEndpointHostname + "'");
            }
        }

        private static void StartLWAClient(string url, WebBrowserType browserType)
        {
            //make sure to launch LWA Client in case OC is installed
            //if (server_url.EndsWith("?sl=", StringComparison.CurrentCultureIgnoreCase) == false)
            //    server_url += "?sl=";

            Console.WriteLine("Start web browser...");
            WebBrowserLauncher browser = new WebBrowserLauncher(browserType);
            browser.StartWebBrowser(url);
        }

        private static void CleanUpBrowserCache()
        {
            try
            {
                string internetCachePath = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
                string[] fileNames = null;
                
                fileNames = System.IO.Directory.GetFiles(internetCachePath, "*.js", System.IO.SearchOption.AllDirectories);
                foreach (string fileName in fileNames)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception) { }
                }

                fileNames = System.IO.Directory.GetFiles(internetCachePath, "*.css", System.IO.SearchOption.AllDirectories);
                foreach (string fileName in fileNames)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception) { }
                }

                fileNames = System.IO.Directory.GetFiles(internetCachePath, "*.png", System.IO.SearchOption.AllDirectories);
                foreach (string fileName in fileNames)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception) { }
                }

                fileNames = System.IO.Directory.GetFiles(internetCachePath, "*.html", System.IO.SearchOption.AllDirectories);
                foreach (string fileName in fileNames)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception) { }
                }

                fileNames = System.IO.Directory.GetFiles(internetCachePath, "*.htm", System.IO.SearchOption.AllDirectories);
                foreach (string fileName in fileNames)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception) { }
                }

                Util.PrintMessage("Cleaned up cached files");
            }
            catch (UnauthorizedAccessException)
            {
                Util.PrintError("You need to run as administrator.");
            }
        }
    } // class Program
}
