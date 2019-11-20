using System;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace MS
{
    public enum WebBrowserType
    {
        IE, //Internet Explorer
        FF, //Mozilla Firefox
        CH, //Chrome
        SF, //Mac Safari
    }

    public class WebBrowserLauncher
    {
        private const string ProgramFilesX86 = @"PROGRAMFILES(x86)";
        private const string ProgramFiles = @"PROGRAMFILES";
        private const string InternetExplorerDirectory = "Internet Explorer";
        private const string InternetExplorerProgram = "iexplore.exe";
        private const string FirefoxDirectory = "Mozilla Firefox";
        private const string FirefoxProgram = "firefox.exe";
        private const string ChromeDirectory = "Google\\Chrome\\Application";
        private const string ChromeProgram = "chrome.exe";

        private WebBrowserType browserType = WebBrowserType.IE;

        public WebBrowserLauncher(WebBrowserType browserType)
        {
            this.browserType = browserType;
        }

        public WebBrowserLauncher(string browserType)
        {
            this.browserType = GetWebBrowserType(browserType);
        }

        public void StartWebBrowser(string url)
        {
            switch (browserType)
            {
                case WebBrowserType.IE: StartInternetExplorer(url); break;
                case WebBrowserType.FF: StartFireFox(url); break;
                case WebBrowserType.CH: StartChrome(url); break;
                default: Debug.Assert(false); break;
            }
        }

        public void StopWebBrowser()
        {
        }

        static public WebBrowserType GetWebBrowserType(string browserType)
        {
            WebBrowserType browser = WebBrowserType.IE;

            if (string.Compare(browserType, "IE", true) == 0)
                browser = WebBrowserType.IE;
            else if (string.Compare(browserType, "FF", true) == 0)
                browser = WebBrowserType.FF;
            else if (string.Compare(browserType, "CH", true) == 0)
                browser = WebBrowserType.CH;
            else if (string.Compare(browserType, "SF", true) == 0)
                browser = WebBrowserType.SF;

            return browser;
        }

        private void StartInternetExplorer(string url)
        {
            StartWebBrowser(InternetExplorerDirectory, InternetExplorerProgram, url);
        }

        private void StartFireFox(string url)
        {
            StartWebBrowser(FirefoxDirectory, FirefoxProgram, url);
        }

        private void StartChrome(string url)
        {
            StartWebBrowser(ChromeDirectory, ChromeProgram, url);
        }

        private void StartWebBrowser(string directory, string program, string url)
        {
            string programFilesX86Path = GetProgramFilesX86Path();
            string fileName = Path.Combine(Path.Combine(programFilesX86Path, directory), program);

            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = url;

            process.Start();
        }

        /// <summary>
        /// Make sure to get the path of 32 bits web browser on 64 bits OS
        /// </summary>
        /// <returns></returns>
        static private string GetProgramFilesX86Path()
        {
            string programFilesX86Path = Environment.GetEnvironmentVariable(ProgramFilesX86);
            if (string.IsNullOrEmpty(programFilesX86Path)) // programFilesX86Path == null on x86
                programFilesX86Path = Environment.GetEnvironmentVariable(ProgramFiles);

            return programFilesX86Path;
        }
    }
}
